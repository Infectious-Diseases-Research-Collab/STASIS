using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;
using STASIS.Models;
using STASIS.Pages.Boxes;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies that Pages/Boxes/Move.cshtml.cs's <c>OnPostMoveAsync</c> handler for the
/// "move to Temp" path converts the <see cref="InvalidOperationException"/> thrown by
/// <see cref="IStorageService.MoveToTempAsync"/> (via
/// <see cref="IStorageService.ValidateAndAssignBoxStudyAsync"/>, when the shared Temp box
/// already holds a different study's specimens) into a friendly <c>ModelState</c> error instead
/// of letting the exception propagate -- same shape as the existing
/// <see cref="IStorageService.MoveSpecimenAsync"/> catch a few lines above it in the same
/// handler. Page models are instantiated directly with services resolved from the real DI
/// container and a hand-built <see cref="ClaimsPrincipal"/> standing in for the authenticated
/// caller, mirroring <c>AddSampleAccessTests</c>. Requires STASIS_TEST_CONNECTION; see
/// <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class MoveToTempErrorHandlingTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public MoveToTempErrorHandlingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static MoveModel BuildMoveModel(IServiceProvider services, string callerUserId)
    {
        var sampleService = services.GetRequiredService<ISampleService>();
        var storageService = services.GetRequiredService<IStorageService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new MoveModel(sampleService, storageService, studyAccessService)
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
            },
            TempData = new TempDataDictionary(
                new DefaultHttpContext { RequestServices = services },
                services.GetRequiredService<ITempDataProvider>())
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

    private static async Task<Box> AddBoxAsync(StasisDbContext context, string label, int? studyId, string category)
    {
        var box = new Box { BoxLabel = label, BoxType = "81-slot", BoxCategory = category, StudyID = studyId };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();
        return box;
    }

    private static async Task<Specimen> AddSpecimenAsync(StasisDbContext context, string barcode, int studyId, int? boxId, int? row, int? col)
    {
        var specimen = new Specimen
        {
            BarcodeID = barcode,
            StudyID = studyId,
            BoxID = boxId,
            PositionRow = row,
            PositionCol = col,
            Status = "In-Stock"
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync();
        return specimen;
    }

    [Fact]
    public async Task OnPostMoveAsync_MoveToTemp_TempBoxHeldByDifferentStudy_ReturnsPageWithModelError_AndDoesNotMoveSpecimen()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-mtt1@test.com");

        var studyA = new Study { StudyCode = "MTT-ERR-A" };
        var studyB = new Study { StudyCode = "MTT-ERR-B" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        // The shared Temp box already holds a Study A specimen.
        var tempBox = await AddBoxAsync(context, "MTT-ERR-TEMP", studyA.StudyID, "Temp");
        await AddSpecimenAsync(context, "MTT-ERR-EXISTING", studyA.StudyID, tempBox.BoxID, row: null, col: null);

        var sourceBox = await AddBoxAsync(context, "MTT-ERR-SOURCE", studyB.StudyID, "Standard");
        var specimenB = await AddSpecimenAsync(context, "MTT-ERR-MOVING", studyB.StudyID, sourceBox.BoxID, row: 1, col: 1);

        var caller = await CreateUserAsync(services, "editor-mtt1@test.com");
        await GrantAsync(context, caller.Id, studyB.StudyID, "Editor", sysAdmin.Id);

        var model = BuildMoveModel(services, caller.Id);
        model.SpecimenId = specimenB.SpecimenID;
        model.MoveToTemp = true;

        var result = await model.OnPostMoveAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);

        // The Study-B specimen must not have been moved into the Temp box.
        var reloadedSpecimen = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimenB.SpecimenID);
        Assert.Equal(sourceBox.BoxID, reloadedSpecimen.BoxID);
        Assert.NotEqual("Temp", reloadedSpecimen.Status);

        // The Temp box's study affiliation and existing specimen are untouched.
        var reloadedTempBox = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == tempBox.BoxID);
        Assert.Equal(studyA.StudyID, reloadedTempBox.StudyID);
        var tempBoxSpecimenCount = await context.Specimens.CountAsync(s => s.BoxID == tempBox.BoxID);
        Assert.Equal(1, tempBoxSpecimenCount);
    }
}
