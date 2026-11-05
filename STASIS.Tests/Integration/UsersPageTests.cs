using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;
using STASIS.Models;
using STASIS.Pages.Administration;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies Pages/Administration/Users.cshtml.cs's <c>OnGetAsync</c> scopes the user list by
/// caller: System Admins see every user; Study Admins see only users who share an access row on
/// a study they manage (plus themselves); everyone else (plain Viewer/Editor, no admin role
/// anywhere) gets <see cref="ForbidResult"/>. Requires STASIS_TEST_CONNECTION to be set; see
/// <see cref="TestWebApplicationFactory"/>.
///
/// The page model is instantiated directly (rather than driven over HTTP) with its services
/// resolved from the real DI container and a hand-built <see cref="ClaimsPrincipal"/> standing
/// in for the authenticated caller. This exercises the real Postgres-backed
/// <see cref="StasisDbContext"/>, <see cref="UserManager{TUser}"/>, and
/// <see cref="StudyAccessService"/> wiring without needing a full authenticated HTTP round trip.
/// </summary>
public class UsersPageTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public UsersPageTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static UsersModel BuildPageModel(
        IServiceProvider services,
        string callerUserId)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var context = services.GetRequiredService<StasisDbContext>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        var model = new UsersModel(userManager, context, studyAccessService)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, callerUserId) },
                        "TestAuth"))
                }
            }
        };

        return model;
    }

    [Fact]
    public async Task SystemAdmin_SeesEveryUser()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<StasisDbContext>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));

        var admin = new IdentityUser { UserName = "sysadmin@test.com", Email = "sysadmin@test.com" };
        await userManager.CreateAsync(admin);
        await userManager.AddToRoleAsync(admin, "Admin");

        var studyA = new Study { StudyCode = "STUDY-A" };
        context.Studies.Add(studyA);
        await context.SaveChangesAsync();

        var userWithAccess = new IdentityUser { UserName = "withaccess@test.com", Email = "withaccess@test.com" };
        var userWithNoAccess = new IdentityUser { UserName = "noaccess@test.com", Email = "noaccess@test.com" };
        await userManager.CreateAsync(userWithAccess);
        await userManager.CreateAsync(userWithNoAccess);

        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = userWithAccess.Id,
            StudyID = studyA.StudyID,
            Role = "Viewer",
            GrantedByUserId = admin.Id,
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var model = BuildPageModel(services, admin.Id);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        var emails = model.Users.Select(u => u.Email).ToList();
        Assert.Contains(admin.Email, emails);
        Assert.Contains(userWithAccess.Email, emails);
        Assert.Contains(userWithNoAccess.Email, emails); // System Admin sees everyone, access or not

        // System Admin sees every user in the system, including ones not created by this test
        // (e.g. Program.cs's startup-seeded admin@stasis.com, triggered by the CreateScope()
        // call above) — so assert against the live count rather than a hardcoded number.
        var totalUserCount = await userManager.Users.CountAsync();
        Assert.Equal(totalUserCount, model.Users.Count);
    }

    [Fact]
    public async Task StudyAdmin_SeesOnlyUsersWithAccessOnManagedStudy_PlusThemselves()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<StasisDbContext>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var granter = new IdentityUser { UserName = "granter@test.com", Email = "granter@test.com" };
        await userManager.CreateAsync(granter);
        await userManager.AddToRoleAsync(granter, "Admin");

        var studyA = new Study { StudyCode = "STUDY-A" };
        var studyB = new Study { StudyCode = "STUDY-B" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        var studyAdmin = new IdentityUser { UserName = "studyadmin@test.com", Email = "studyadmin@test.com" };
        var userOnStudyA = new IdentityUser { UserName = "onstudya@test.com", Email = "onstudya@test.com" };
        var userOnlyOnStudyB = new IdentityUser { UserName = "onstudyb@test.com", Email = "onstudyb@test.com" };
        var userWithNoAccess = new IdentityUser { UserName = "noaccess2@test.com", Email = "noaccess2@test.com" };
        await userManager.CreateAsync(studyAdmin);
        await userManager.CreateAsync(userOnStudyA);
        await userManager.CreateAsync(userOnlyOnStudyB);
        await userManager.CreateAsync(userWithNoAccess);

        context.UserStudyAccesses.AddRange(
            new UserStudyAccess
            {
                AspNetUserId = studyAdmin.Id,
                StudyID = studyA.StudyID,
                Role = "StudyAdmin",
                GrantedByUserId = granter.Id,
                GrantedDate = DateTime.UtcNow
            },
            new UserStudyAccess
            {
                AspNetUserId = userOnStudyA.Id,
                StudyID = studyA.StudyID,
                Role = "Viewer",
                GrantedByUserId = granter.Id,
                GrantedDate = DateTime.UtcNow
            },
            new UserStudyAccess
            {
                AspNetUserId = userOnlyOnStudyB.Id,
                StudyID = studyB.StudyID,
                Role = "Editor",
                GrantedByUserId = granter.Id,
                GrantedDate = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var model = BuildPageModel(services, studyAdmin.Id);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        var emails = model.Users.Select(u => u.Email).ToList();
        Assert.Contains(studyAdmin.Email, emails); // sees themselves
        Assert.Contains(userOnStudyA.Email, emails); // shares the managed study
        Assert.DoesNotContain(userOnlyOnStudyB.Email, emails); // only on an unmanaged study
        Assert.DoesNotContain(userWithNoAccess.Email, emails); // no access anywhere
        Assert.Equal(2, model.Users.Count);

        var viewModel = model.Users.Single(u => u.Email == userOnStudyA.Email);
        Assert.Equal(new[] { ("STUDY-A", "Viewer") }, viewModel.StudyAccess);
    }

    [Fact]
    public async Task PlainViewer_WithNoAdminRoleAnywhere_GetsForbid()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<StasisDbContext>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var granter = new IdentityUser { UserName = "granter2@test.com", Email = "granter2@test.com" };
        await userManager.CreateAsync(granter);
        await userManager.AddToRoleAsync(granter, "Admin");

        var studyC = new Study { StudyCode = "STUDY-C" };
        context.Studies.Add(studyC);
        await context.SaveChangesAsync();

        var viewer = new IdentityUser { UserName = "viewer@test.com", Email = "viewer@test.com" };
        await userManager.CreateAsync(viewer);

        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = viewer.Id,
            StudyID = studyC.StudyID,
            Role = "Viewer",
            GrantedByUserId = granter.Id,
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var model = BuildPageModel(services, viewer.Id);

        var result = await model.OnGetAsync();

        Assert.IsType<ForbidResult>(result);
    }
}
