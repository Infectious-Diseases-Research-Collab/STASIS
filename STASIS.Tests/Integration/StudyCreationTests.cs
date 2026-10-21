using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;
using STASIS.Models;
using STASIS.Pages.LabSetup;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies Pages/LabSetup/Studies.cshtml.cs's <c>OnPostAddAsync</c>: creating a study with a
/// chosen <c>Input.FirstStudyAdminUserId</c> creates the study and, in the same request, a
/// <c>UserStudyAccess</c> row with <c>Role = "StudyAdmin"</c> for that user -- granted by the
/// caller. The System Admin who created the study does NOT get an implicit access row of their
/// own (per the design doc: they stay in System Admin override status unless they separately
/// grant themselves access). Also verifies the page remains restricted to System Admins only via
/// its class-level <c>[Authorize(Roles = "Admin")]</c> attribute. Requires
/// STASIS_TEST_CONNECTION to be set; see <see cref="TestWebApplicationFactory"/>.
///
/// The page model is instantiated directly (rather than driven over HTTP) with its services
/// resolved from the real DI container and a hand-built <see cref="ClaimsPrincipal"/> standing in
/// for the authenticated caller, mirroring <c>CreateUserTests</c>/<c>EditUserAccessTests</c>.
/// </summary>
public class StudyCreationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public StudyCreationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static StudiesModel BuildPageModel(IServiceProvider services, string callerUserId)
    {
        var context = services.GetRequiredService<StasisDbContext>();
        var auditService = services.GetRequiredService<IAuditService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        var model = new StudiesModel(context, auditService, studyAccessService, userManager)
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

        return model;
    }

    [Fact]
    public async Task OnPostAddAsync_CreatesStudyAndGrantsFirstStudyAdmin_CallerGetsNoImplicitAccessRow()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<StasisDbContext>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var sysAdmin = new IdentityUser { UserName = "sysadmin-studies@test.com", Email = "sysadmin-studies@test.com" };
        await userManager.CreateAsync(sysAdmin);
        await userManager.AddToRoleAsync(sysAdmin, "Admin");

        var futureStudyAdmin = new IdentityUser { UserName = "futurestudyadmin@test.com", Email = "futurestudyadmin@test.com" };
        await userManager.CreateAsync(futureStudyAdmin);

        var model = BuildPageModel(services, sysAdmin.Id);
        model.Input = new StudiesModel.StudyInput
        {
            StudyCode = "STUDY-NEW",
            StudyName = "Newly Created Study",
            PrincipalInvestigator = "Dr. Someone",
            FirstStudyAdminUserId = futureStudyAdmin.Id
        };

        var result = await model.OnPostAddAsync();

        Assert.IsType<RedirectToPageResult>(result);

        var study = await context.Studies.AsNoTracking().SingleAsync(s => s.StudyCode == "STUDY-NEW");

        // The designated first Study Admin got a StudyAdmin access row, granted by the caller.
        var access = await context.UserStudyAccesses
            .AsNoTracking()
            .SingleAsync(a => a.StudyID == study.StudyID);
        Assert.Equal(futureStudyAdmin.Id, access.AspNetUserId);
        Assert.Equal("StudyAdmin", access.Role);
        Assert.Equal(sysAdmin.Id, access.GrantedByUserId);

        // The creating System Admin does NOT get an implicit access row of their own.
        Assert.False(await context.UserStudyAccesses.AsNoTracking()
            .AnyAsync(a => a.AspNetUserId == sysAdmin.Id));

        // Exactly one access row exists in total (the designated Study Admin's) for this study.
        Assert.Equal(1, await context.UserStudyAccesses.AsNoTracking()
            .CountAsync(a => a.StudyID == study.StudyID));
    }

    [Fact]
    public async Task OnPostAddAsync_MissingFirstStudyAdmin_RejectsWithoutCreatingStudy()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<StasisDbContext>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var sysAdmin = new IdentityUser { UserName = "sysadmin-studies2@test.com", Email = "sysadmin-studies2@test.com" };
        await userManager.CreateAsync(sysAdmin);
        await userManager.AddToRoleAsync(sysAdmin, "Admin");

        var model = BuildPageModel(services, sysAdmin.Id);
        model.Input = new StudiesModel.StudyInput
        {
            StudyCode = "STUDY-NOADMIN",
            FirstStudyAdminUserId = null
        };

        var result = await model.OnPostAddAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.False(await context.Studies.AsNoTracking().AnyAsync(s => s.StudyCode == "STUDY-NOADMIN"));
    }

    [Fact]
    public void StudiesModel_IsRestrictedToSystemAdmins_ViaAuthorizeRolesAttribute()
    {
        // This page has no per-handler authorization check of its own (unlike
        // Administration/Users, CreateUser, EditUser, which serve both System Admins and Study
        // Admins) -- only System Admins may create studies at all, enforced entirely by this
        // class-level attribute. "Admin" means System Admin post role-retirement (Task 1).
        var attribute = typeof(StudiesModel).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal("Admin", attribute!.Roles);
    }
}
