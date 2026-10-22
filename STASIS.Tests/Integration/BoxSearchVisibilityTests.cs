using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;
using STASIS.Models;
using STASIS.Pages.Boxes;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies the read-side enforcement of study-scoped access control for the Box search page
/// (<see cref="SearchModel"/>): non-System-Admin callers only ever see boxes within their
/// assigned <see cref="IStudyAccessService.GetVisibleStudyIdsAsync"/> set, plus any box not yet
/// claimed by a study (<c>StudyID == null</c>) -- unless the caller has zero study assignments,
/// in which case they see no boxes at all, including unassigned ones. A System Admin
/// (<c>visibleStudyIds == null</c>) is unrestricted.
///
/// Page models are instantiated directly with services resolved from the real DI container and a
/// hand-built <see cref="ClaimsPrincipal"/> standing in for the authenticated caller, mirroring
/// <c>SamplesVisibilityTests</c>/<c>EditUserAccessTests</c>. Requires STASIS_TEST_CONNECTION; see
/// <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class BoxSearchVisibilityTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public BoxSearchVisibilityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static SearchModel BuildSearchModel(IServiceProvider services, string callerUserId)
    {
        var storageService = services.GetRequiredService<IStorageService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new SearchModel(storageService, studyAccessService)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services,
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, callerUserId) },
                        "TestAuth"))
                }
            }
        };
    }

    private static async Task<IdentityUser> CreateSystemAdminAsync(IServiceProvider services, string email)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        var user = new IdentityUser { UserName = email, Email = email };
        await userManager.CreateAsync(user);
        await userManager.AddToRoleAsync(user, "Admin");
        return user;
    }

    private static async Task<IdentityUser> CreateUserAsync(IServiceProvider services, string email)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = email, Email = email };
        await userManager.CreateAsync(user);
        return user;
    }

    private static async Task GrantAsync(StasisDbContext context, string userId, int studyId, string role, string grantedBy)
    {
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = userId,
            StudyID = studyId,
            Role = role,
            GrantedByUserId = grantedBy,
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Box> AddBoxAsync(StasisDbContext context, string label, int? studyId)
    {
        var box = new Box
        {
            BoxLabel = label,
            BoxType = "81-slot",
            StudyID = studyId
        };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();
        return box;
    }

    [Fact]
    public async Task Viewer_SearchingBoxes_NeverSeesOtherStudyBox_EvenWithNoFiltersApplied()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-bs1@test.com");

        var studyA = new Study { StudyCode = "BS-A" };
        var studyB = new Study { StudyCode = "BS-B" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        var boxA = await AddBoxAsync(context, "BS-BOX-A1", studyA.StudyID);
        var boxB = await AddBoxAsync(context, "BS-BOX-B1", studyB.StudyID);

        var viewer = await CreateUserAsync(services, "viewer-bs1@test.com");
        await GrantAsync(context, viewer.Id, studyA.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildSearchModel(services, viewer.Id);
        model.BoxLabel = "BS-BOX-";

        await model.OnGetAsync();

        Assert.Contains(model.Boxes, b => b.BoxID == boxA.BoxID);
        Assert.DoesNotContain(model.Boxes, b => b.BoxID == boxB.BoxID);
        Assert.Contains(boxA.BoxLabel, model.AllBoxLabels);
        Assert.DoesNotContain(boxB.BoxLabel, model.AllBoxLabels);
    }

    private static async Task<Specimen> AddSpecimenAsync(StasisDbContext context, string barcode, int studyId, int? boxId)
    {
        var specimen = new Specimen
        {
            BarcodeID = barcode,
            StudyID = studyId,
            BoxID = boxId,
            PositionRow = 1,
            PositionCol = 1,
            Status = "In-Stock"
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync();
        return specimen;
    }

    [Fact]
    public async Task Viewer_WithAtLeastOneStudy_DoesNotSeeUnbackfilledLegacyBoxWithSpecimens()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-bs6@test.com");

        var studyA = new Study { StudyCode = "BS-I" };
        context.Studies.Add(studyA);
        await context.SaveChangesAsync();

        // Simulates legacy pre-existing data: Box.StudyID was added after specimens already
        // existed, and this box was never backfilled and never touched by a write path, so its
        // StudyID is still null even though it already holds a specimen.
        var legacyBox = await AddBoxAsync(context, "BS-BOX-I1", null);
        await AddSpecimenAsync(context, "BS-SPEC-I1", studyA.StudyID, legacyBox.BoxID);

        var viewer = await CreateUserAsync(services, "viewer-bs6@test.com");
        await GrantAsync(context, viewer.Id, studyA.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildSearchModel(services, viewer.Id);
        model.BoxLabel = "BS-BOX-";
        model.SelectedBoxId = legacyBox.BoxID;

        await model.OnGetAsync();

        Assert.DoesNotContain(model.Boxes, b => b.BoxID == legacyBox.BoxID);
        Assert.DoesNotContain(legacyBox.BoxLabel, model.AllBoxLabels);
        Assert.Null(model.SelectedBox);
    }

    [Fact]
    public async Task Viewer_WithAtLeastOneStudy_SeesUnassignedEmptyBox()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-bs2@test.com");

        var studyA = new Study { StudyCode = "BS-C" };
        context.Studies.Add(studyA);
        await context.SaveChangesAsync();

        var unassignedBox = await AddBoxAsync(context, "BS-BOX-C1", null);

        var viewer = await CreateUserAsync(services, "viewer-bs2@test.com");
        await GrantAsync(context, viewer.Id, studyA.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildSearchModel(services, viewer.Id);
        model.BoxLabel = "BS-BOX-";
        model.SelectedBoxId = unassignedBox.BoxID;

        await model.OnGetAsync();

        Assert.Contains(model.Boxes, b => b.BoxID == unassignedBox.BoxID);
        Assert.Contains(unassignedBox.BoxLabel, model.AllBoxLabels);
        Assert.NotNull(model.SelectedBox);
    }

    [Fact]
    public async Task Viewer_WithZeroStudyAssignments_SeesNoBoxesAtAll_IncludingUnassigned()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var studyA = new Study { StudyCode = "BS-D" };
        context.Studies.Add(studyA);
        await context.SaveChangesAsync();

        var assignedBox = await AddBoxAsync(context, "BS-BOX-D1", studyA.StudyID);
        var unassignedBox = await AddBoxAsync(context, "BS-BOX-D2", null);

        // No grants at all for this user -- zero study assignments.
        var noAccessUser = await CreateUserAsync(services, "noaccess-bs3@test.com");

        var model = BuildSearchModel(services, noAccessUser.Id);
        model.BoxLabel = "BS-BOX-";

        await model.OnGetAsync();

        Assert.DoesNotContain(model.Boxes, b => b.BoxID == assignedBox.BoxID);
        Assert.DoesNotContain(model.Boxes, b => b.BoxID == unassignedBox.BoxID);
        Assert.DoesNotContain(assignedBox.BoxLabel, model.AllBoxLabels);
        Assert.DoesNotContain(unassignedBox.BoxLabel, model.AllBoxLabels);
    }

    [Fact]
    public async Task SystemAdmin_SearchingBoxes_SeesBoxesAcrossAllStudiesAndUnassigned()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-bs4@test.com");

        var studyA = new Study { StudyCode = "BS-E" };
        var studyB = new Study { StudyCode = "BS-F" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        var boxA = await AddBoxAsync(context, "BS-BOX-E1", studyA.StudyID);
        var boxB = await AddBoxAsync(context, "BS-BOX-F1", studyB.StudyID);
        var unassignedBox = await AddBoxAsync(context, "BS-BOX-E2", null);

        var model = BuildSearchModel(services, sysAdmin.Id);
        model.BoxLabel = "BS-BOX-";

        await model.OnGetAsync();

        Assert.Contains(model.Boxes, b => b.BoxID == boxA.BoxID);
        Assert.Contains(model.Boxes, b => b.BoxID == boxB.BoxID);
        Assert.Contains(model.Boxes, b => b.BoxID == unassignedBox.BoxID);
    }

    [Fact]
    public async Task Viewer_SelectingOtherStudyBoxDetail_SeesNoSelectedBox()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-bs5@test.com");

        var studyA = new Study { StudyCode = "BS-G" };
        var studyB = new Study { StudyCode = "BS-H" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        var boxB = await AddBoxAsync(context, "BS-BOX-H1", studyB.StudyID);

        var viewer = await CreateUserAsync(services, "viewer-bs5@test.com");
        await GrantAsync(context, viewer.Id, studyA.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildSearchModel(services, viewer.Id);
        model.SelectedBoxId = boxB.BoxID;

        await model.OnGetAsync();

        Assert.Null(model.SelectedBox);
    }
}
