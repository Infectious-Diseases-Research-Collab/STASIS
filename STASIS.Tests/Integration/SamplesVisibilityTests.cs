using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;
using STASIS.Models;
using STASIS.Pages;
using STASIS.Pages.Samples;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies the enforcement phase of study-scoped access control for the Samples search page
/// (<see cref="SamplesModel"/>) and the specimen detail page (<see cref="DetailModel"/>):
/// non-System-Admin callers only ever see specimens/studies within their assigned
/// <see cref="IStudyAccessService.GetVisibleStudyIdsAsync"/> set, and a System Admin
/// (<c>visibleStudyIds == null</c>) is unrestricted.
///
/// Page models are instantiated directly with services resolved from the real DI container and a
/// hand-built <see cref="ClaimsPrincipal"/> standing in for the authenticated caller, mirroring
/// <c>EditUserAccessTests</c>/<c>CreateUserTests</c>. Requires STASIS_TEST_CONNECTION; see
/// <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class SamplesVisibilityTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public SamplesVisibilityTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static SamplesModel BuildSamplesModel(IServiceProvider services, string callerUserId)
    {
        var sampleService = services.GetRequiredService<ISampleService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new SamplesModel(sampleService, studyAccessService)
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

    private static DetailModel BuildDetailModel(IServiceProvider services, string callerUserId)
    {
        var sampleService = services.GetRequiredService<ISampleService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new DetailModel(sampleService, studyAccessService)
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

    private static async Task<Specimen> AddSpecimenAsync(StasisDbContext context, int studyId, string barcode)
    {
        var specimen = new Specimen
        {
            BarcodeID = barcode,
            StudyID = studyId,
            Status = "In-Stock"
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync();
        return specimen;
    }

    [Fact]
    public async Task Viewer_SearchingSamples_NeverSeesOtherStudySpecimen_EvenWithNoFiltersApplied()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-sv1@test.com");

        var studyA = new Study { StudyCode = "SV-A" };
        var studyB = new Study { StudyCode = "SV-B" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        var specimenA = await AddSpecimenAsync(context, studyA.StudyID, "SVBARCODEA1");
        var specimenB = await AddSpecimenAsync(context, studyB.StudyID, "SVBARCODEB1");

        var viewer = await CreateUserAsync(services, "viewer-sv1@test.com");
        await GrantAsync(context, viewer.Id, studyA.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildSamplesModel(services, viewer.Id);

        await model.OnGetAsync(null);

        Assert.Contains(model.Specimens, s => s.SpecimenID == specimenA.SpecimenID);
        Assert.DoesNotContain(model.Specimens, s => s.SpecimenID == specimenB.SpecimenID);

        // The Study dropdown must only list Study A -- not Study B.
        var studyIdsInDropdown = model.Studies.Cast<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>()
            .Select(i => int.Parse(i.Value!))
            .ToList();
        Assert.Contains(studyA.StudyID, studyIdsInDropdown);
        Assert.DoesNotContain(studyB.StudyID, studyIdsInDropdown);
    }

    [Fact]
    public async Task SystemAdmin_SearchingSamples_SeesSpecimensAndStudiesAcrossAllStudies()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-sv2@test.com");

        var studyA = new Study { StudyCode = "SV-C" };
        var studyB = new Study { StudyCode = "SV-D" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        var specimenA = await AddSpecimenAsync(context, studyA.StudyID, "SVBARCODEC1");
        var specimenB = await AddSpecimenAsync(context, studyB.StudyID, "SVBARCODED1");

        var model = BuildSamplesModel(services, sysAdmin.Id);

        await model.OnGetAsync(null);

        Assert.Contains(model.Specimens, s => s.SpecimenID == specimenA.SpecimenID);
        Assert.Contains(model.Specimens, s => s.SpecimenID == specimenB.SpecimenID);

        var studyIdsInDropdown = model.Studies.Cast<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>()
            .Select(i => int.Parse(i.Value!))
            .ToList();
        Assert.Contains(studyA.StudyID, studyIdsInDropdown);
        Assert.Contains(studyB.StudyID, studyIdsInDropdown);
    }

    [Fact]
    public async Task Viewer_RequestingDetailForOtherStudySpecimen_ReturnsNotFound()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-sv3@test.com");

        var studyA = new Study { StudyCode = "SV-E" };
        var studyB = new Study { StudyCode = "SV-F" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        var specimenB = await AddSpecimenAsync(context, studyB.StudyID, "SVBARCODEF1");

        var viewer = await CreateUserAsync(services, "viewer-sv3@test.com");
        await GrantAsync(context, viewer.Id, studyA.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildDetailModel(services, viewer.Id);
        model.Id = specimenB.SpecimenID;

        var result = await model.OnGetAsync();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SystemAdmin_RequestingDetailForAnySpecimen_ReturnsPage()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-sv4@test.com");

        var studyA = new Study { StudyCode = "SV-G" };
        context.Studies.Add(studyA);
        await context.SaveChangesAsync();

        var specimenA = await AddSpecimenAsync(context, studyA.StudyID, "SVBARCODEG1");

        var model = BuildDetailModel(services, sysAdmin.Id);
        model.Id = specimenA.SpecimenID;

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Specimen);
        Assert.Equal(specimenA.SpecimenID, model.Specimen!.SpecimenID);
    }
}
