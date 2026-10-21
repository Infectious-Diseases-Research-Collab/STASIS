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
/// Verifies Pages/Administration/CreateUser.cshtml.cs's <c>OnPostAsync</c>: a System Admin can
/// flag a new user as System Admin and/or assign them to a study with a role; a Study Admin
/// cannot grant System Admin (it's silently ignored) and can only assign a study they personally
/// manage — including a server-side IDOR check that rejects a posted StudyID the caller doesn't
/// manage even though the UI would never have offered it. Requires STASIS_TEST_CONNECTION to be
/// set; see <see cref="TestWebApplicationFactory"/>.
///
/// The page model is instantiated directly (rather than driven over HTTP) with its services
/// resolved from the real DI container and a hand-built <see cref="ClaimsPrincipal"/> standing in
/// for the authenticated caller, mirroring <c>UsersPageTests</c>.
/// </summary>
public class CreateUserTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public CreateUserTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static CreateUserModel BuildPageModel(IServiceProvider services, string callerUserId)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var context = services.GetRequiredService<StasisDbContext>();
        var auditService = services.GetRequiredService<IAuditService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        var model = new CreateUserModel(userManager, context, auditService, studyAccessService)
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
    public async Task SystemAdmin_CanFlagNewUserAsSystemAdmin()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var caller = new IdentityUser { UserName = "sysadmin1@test.com", Email = "sysadmin1@test.com" };
        await userManager.CreateAsync(caller);
        await userManager.AddToRoleAsync(caller, "Admin");

        var model = BuildPageModel(services, caller.Id);
        model.Input = new CreateUserModel.InputModel
        {
            Email = "newadmin@test.com",
            Password = "Passw0rd!",
            Department = "IT",
            IsSystemAdmin = true
        };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var newUser = await userManager.FindByEmailAsync("newadmin@test.com");
        Assert.NotNull(newUser);
        Assert.True(await userManager.IsInRoleAsync(newUser!, "Admin"));
    }

    [Fact]
    public async Task SystemAdmin_CanAssignNewUserToAnyStudyWithRole_GrantedByIsCaller()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<StasisDbContext>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var caller = new IdentityUser { UserName = "sysadmin2@test.com", Email = "sysadmin2@test.com" };
        await userManager.CreateAsync(caller);
        await userManager.AddToRoleAsync(caller, "Admin");

        var study = new Study { StudyCode = "STUDY-X" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var model = BuildPageModel(services, caller.Id);
        model.Input = new CreateUserModel.InputModel
        {
            Email = "studystaff@test.com",
            Password = "Passw0rd!",
            StudyID = study.StudyID,
            StudyRole = "Editor"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var newUser = await userManager.FindByEmailAsync("studystaff@test.com");
        Assert.NotNull(newUser);

        var access = await context.UserStudyAccesses
            .AsNoTracking()
            .SingleAsync(a => a.AspNetUserId == newUser!.Id);
        Assert.Equal(study.StudyID, access.StudyID);
        Assert.Equal("Editor", access.Role);
        Assert.Equal(caller.Id, access.GrantedByUserId);
    }

    [Fact]
    public async Task StudyAdmin_CannotSetSystemAdmin_FlagIsIgnored()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<StasisDbContext>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var granter = new IdentityUser { UserName = "granter3@test.com", Email = "granter3@test.com" };
        await userManager.CreateAsync(granter);
        await userManager.AddToRoleAsync(granter, "Admin");

        var study = new Study { StudyCode = "STUDY-Y" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var studyAdmin = new IdentityUser { UserName = "studyadmin3@test.com", Email = "studyadmin3@test.com" };
        await userManager.CreateAsync(studyAdmin);
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = studyAdmin.Id,
            StudyID = study.StudyID,
            Role = "StudyAdmin",
            GrantedByUserId = granter.Id,
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var model = BuildPageModel(services, studyAdmin.Id);
        model.Input = new CreateUserModel.InputModel
        {
            Email = "wannabeadmin@test.com",
            Password = "Passw0rd!",
            IsSystemAdmin = true // attempted privilege escalation
        };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result); // user creation itself still succeeds
        var newUser = await userManager.FindByEmailAsync("wannabeadmin@test.com");
        Assert.NotNull(newUser);
        Assert.False(await userManager.IsInRoleAsync(newUser!, "Admin"));
    }

    [Fact]
    public async Task StudyAdmin_CanAssignNewUserToStudyTheyManage()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<StasisDbContext>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var granter = new IdentityUser { UserName = "granter4@test.com", Email = "granter4@test.com" };
        await userManager.CreateAsync(granter);
        await userManager.AddToRoleAsync(granter, "Admin");

        var managedStudy = new Study { StudyCode = "STUDY-MANAGED" };
        context.Studies.Add(managedStudy);
        await context.SaveChangesAsync();

        var studyAdmin = new IdentityUser { UserName = "studyadmin4@test.com", Email = "studyadmin4@test.com" };
        await userManager.CreateAsync(studyAdmin);
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = studyAdmin.Id,
            StudyID = managedStudy.StudyID,
            Role = "StudyAdmin",
            GrantedByUserId = granter.Id,
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var model = BuildPageModel(services, studyAdmin.Id);
        model.Input = new CreateUserModel.InputModel
        {
            Email = "newstaff@test.com",
            Password = "Passw0rd!",
            StudyID = managedStudy.StudyID,
            StudyRole = "Viewer"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var newUser = await userManager.FindByEmailAsync("newstaff@test.com");
        Assert.NotNull(newUser);

        var access = await context.UserStudyAccesses
            .AsNoTracking()
            .SingleAsync(a => a.AspNetUserId == newUser!.Id);
        Assert.Equal(managedStudy.StudyID, access.StudyID);
        Assert.Equal("Viewer", access.Role);
        Assert.Equal(studyAdmin.Id, access.GrantedByUserId);
    }

    [Fact]
    public async Task StudyAdmin_PostingStudyIdTheyDoNotManage_IsRejectedServerSide_IDORGuard()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<StasisDbContext>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        var granter = new IdentityUser { UserName = "granter5@test.com", Email = "granter5@test.com" };
        await userManager.CreateAsync(granter);
        await userManager.AddToRoleAsync(granter, "Admin");

        var managedStudy = new Study { StudyCode = "STUDY-MANAGED2" };
        var unmanagedStudy = new Study { StudyCode = "STUDY-UNMANAGED" };
        context.Studies.AddRange(managedStudy, unmanagedStudy);
        await context.SaveChangesAsync();

        var studyAdmin = new IdentityUser { UserName = "studyadmin5@test.com", Email = "studyadmin5@test.com" };
        await userManager.CreateAsync(studyAdmin);
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = studyAdmin.Id,
            StudyID = managedStudy.StudyID,
            Role = "StudyAdmin",
            GrantedByUserId = granter.Id,
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var model = BuildPageModel(services, studyAdmin.Id);
        // Simulates a direct POST with a StudyID the dropdown would never have offered.
        model.Input = new CreateUserModel.InputModel
        {
            Email = "idorattempt@test.com",
            Password = "Passw0rd!",
            StudyID = unmanagedStudy.StudyID,
            StudyRole = "Editor"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);

        // The whole request is rejected server-side -- no user, no access row.
        var newUser = await userManager.FindByEmailAsync("idorattempt@test.com");
        Assert.Null(newUser);
        Assert.False(await context.UserStudyAccesses.AsNoTracking()
            .AnyAsync(a => a.StudyID == unmanagedStudy.StudyID));
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
        var granter = new IdentityUser { UserName = "granter6@test.com", Email = "granter6@test.com" };
        await userManager.CreateAsync(granter);
        await userManager.AddToRoleAsync(granter, "Admin");

        var study = new Study { StudyCode = "STUDY-Z" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var viewer = new IdentityUser { UserName = "viewer2@test.com", Email = "viewer2@test.com" };
        await userManager.CreateAsync(viewer);
        context.UserStudyAccesses.Add(new UserStudyAccess
        {
            AspNetUserId = viewer.Id,
            StudyID = study.StudyID,
            Role = "Viewer",
            GrantedByUserId = granter.Id,
            GrantedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var model = BuildPageModel(services, viewer.Id);
        model.Input = new CreateUserModel.InputModel
        {
            Email = "shouldnotexist@test.com",
            Password = "Passw0rd!"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.Null(await userManager.FindByEmailAsync("shouldnotexist@test.com"));
    }
}
