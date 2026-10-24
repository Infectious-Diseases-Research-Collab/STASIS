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
using STASIS.Pages.Shipments;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies <see cref="CreateModel"/> (<c>Pages/Shipments/Create.cshtml.cs</c>)'s replacement for
/// its previous <c>[Authorize(Roles = "Admin")]</c> gate -- a regression from when it used to be
/// <c>[Authorize(Roles = "Write,Admin")]</c> before the app's global "Write" role was retired.
/// The page now uses an "editor anywhere" gate (same shape as <c>Boxes/Create.cshtml.cs</c>) plus
/// per-row study enforcement threaded into <see cref="IShipmentService.ImportBatchFromCsvAsync"/>
/// via its new <c>editableStudyIds</c> parameter -- see <c>ShipmentServiceStudyEnforcementTests</c>
/// for the service-level per-row coverage. This file only covers the page-level gate.
///
/// Page models are instantiated directly with services resolved from the real DI container and a
/// hand-built <see cref="ClaimsPrincipal"/> standing in for the authenticated caller, mirroring
/// <c>AddSampleAccessTests</c>/<c>RemainingWriteGatesTests</c>. Requires STASIS_TEST_CONNECTION; see
/// <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class ShipmentsCreateAccessTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public ShipmentsCreateAccessTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static CreateModel BuildCreateModel(IServiceProvider services, string callerUserId)
    {
        var shipmentService = services.GetRequiredService<IShipmentService>();
        var sampleService = services.GetRequiredService<ISampleService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new CreateModel(shipmentService, sampleService, studyAccessService)
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

    [Fact]
    public async Task OnPostAsync_CallerIsEditorOnAtLeastOneStudy_DoesNotReturnForbid()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-ship1@test.com");
        var study = new Study { StudyCode = "SHIP-PAGE-EDIT" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-ship1@test.com");
        await GrantAsync(context, caller.Id, study.StudyID, "Editor", sysAdmin.Id);

        var model = BuildCreateModel(services, caller.Id);
        var csvBytes = System.Text.Encoding.UTF8.GetBytes("Barcode\nSHIP-PAGE-001");
        model.CsvFile = new FormFile(new MemoryStream(csvBytes), 0, csvBytes.Length, "CsvFile", "shipments.csv");

        var result = await model.OnPostAsync();

        Assert.IsNotType<ForbidResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_CallerWithNoEditorAccessAnywhere_ReturnsForbid()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-ship2@test.com");
        var study = new Study { StudyCode = "SHIP-PAGE-VIEWONLY" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "viewer-ship2@test.com");
        await GrantAsync(context, caller.Id, study.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildCreateModel(services, caller.Id);
        var csvBytes = System.Text.Encoding.UTF8.GetBytes("Barcode\nSHIP-PAGE-002");
        model.CsvFile = new FormFile(new MemoryStream(csvBytes), 0, csvBytes.Length, "CsvFile", "shipments.csv");

        var result = await model.OnPostAsync();

        Assert.IsType<ForbidResult>(result);
    }
}
