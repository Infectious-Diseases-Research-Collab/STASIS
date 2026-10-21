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
/// Verifies Pages/Administration/EditUser.cshtml.cs's three study-access handlers --
/// <c>OnPostGrantAccessAsync</c>, <c>OnPostRevokeAccessAsync</c>, <c>OnPostChangeRoleAsync</c>:
/// each re-validates <see cref="IStudyAccessService.CanManageStudyAsync"/> server-side (an IDOR
/// guard, mirroring <see cref="CreateUserModel"/>), and revoke/change-role additionally enforce
/// the hard guardrail that a study can never be left with zero StudyAdmin rows -- even when the
/// caller is a System Admin. Every successful grant/revoke/role-change writes a
/// "tbl_UserStudyAccess" audit log entry (written inside <see cref="StudyAccessService"/> itself,
/// per its existing GrantAccessAsync precedent). Requires STASIS_TEST_CONNECTION to be set; see
/// <see cref="TestWebApplicationFactory"/>.
///
/// The page model is instantiated directly (rather than driven over HTTP) with its services
/// resolved from the real DI container and a hand-built <see cref="ClaimsPrincipal"/> standing in
/// for the authenticated caller, mirroring <c>CreateUserTests</c>/<c>UsersPageTests</c>.
/// <see cref="DefaultHttpContext.RequestServices"/> is wired to the same scope so the handlers'
/// use of <c>TempData</c> resolves <c>ITempDataDictionaryFactory</c> correctly.
/// </summary>
public class EditUserAccessTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public EditUserAccessTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static EditUserModel BuildPageModel(IServiceProvider services, string callerUserId)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var context = services.GetRequiredService<StasisDbContext>();
        var auditService = services.GetRequiredService<IAuditService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        var model = new EditUserModel(userManager, context, auditService, studyAccessService)
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
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new DefaultHttpContext { RequestServices = services },
                services.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>())
        };

        return model;
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
    public async Task GrantAccess_CallerDoesNotManageStudy_IsRejectedServerSide_NoRowCreated()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin1@test.com");

        var managedStudy = new Study { StudyCode = "EU-MANAGED" };
        var unmanagedStudy = new Study { StudyCode = "EU-UNMANAGED" };
        context.Studies.AddRange(managedStudy, unmanagedStudy);
        await context.SaveChangesAsync();

        var studyAdmin = await CreateUserAsync(services, "studyadmin1@test.com");
        await GrantAsync(context, studyAdmin.Id, managedStudy.StudyID, "StudyAdmin", sysAdmin.Id);

        var targetUser = await CreateUserAsync(services, "target1@test.com");

        var model = BuildPageModel(services, studyAdmin.Id);

        var result = await model.OnPostGrantAccessAsync(targetUser.Id, unmanagedStudy.StudyID, "Viewer");

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(model.TempData["Error"]);

        var created = await context.UserStudyAccesses.AsNoTracking()
            .AnyAsync(a => a.AspNetUserId == targetUser.Id && a.StudyID == unmanagedStudy.StudyID);
        Assert.False(created);
    }

    [Fact]
    public async Task GrantAccess_CallerManagesStudy_Succeeds_AndWritesAuditEntry()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin2@test.com");

        var study = new Study { StudyCode = "EU-GRANTOK" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var targetUser = await CreateUserAsync(services, "target2@test.com");

        var model = BuildPageModel(services, sysAdmin.Id);

        var result = await model.OnPostGrantAccessAsync(targetUser.Id, study.StudyID, "Editor");

        Assert.IsType<RedirectToPageResult>(result);

        var access = await context.UserStudyAccesses.AsNoTracking()
            .SingleAsync(a => a.AspNetUserId == targetUser.Id && a.StudyID == study.StudyID);
        Assert.Equal("Editor", access.Role);

        var auditEntry = await context.AuditLogs.AsNoTracking()
            .SingleOrDefaultAsync(a => a.TableName == "tbl_UserStudyAccess" && a.RecordID == access.UserStudyAccessID);
        Assert.NotNull(auditEntry);
        Assert.Equal(sysAdmin.Id, auditEntry!.ChangedByUserId);
    }

    [Fact]
    public async Task RevokeAccess_LastStudyAdmin_RejectedBySystemAdminCaller_RowUnchanged()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin3@test.com");

        var study = new Study { StudyCode = "EU-SOLEADMIN" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var soleAdmin = await CreateUserAsync(services, "soleadmin3@test.com");
        await GrantAsync(context, soleAdmin.Id, study.StudyID, "StudyAdmin", sysAdmin.Id);

        // Caller is a System Admin, not even the target -- the guardrail must still apply.
        var model = BuildPageModel(services, sysAdmin.Id);

        var result = await model.OnPostRevokeAccessAsync(soleAdmin.Id, study.StudyID);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("A study must have at least one Study Admin.", model.TempData["Error"]);

        var access = await context.UserStudyAccesses.AsNoTracking()
            .SingleAsync(a => a.AspNetUserId == soleAdmin.Id && a.StudyID == study.StudyID);
        Assert.Equal("StudyAdmin", access.Role);

        var auditEntryExists = await context.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.TableName == "tbl_UserStudyAccess" && a.RecordID == access.UserStudyAccessID);
        Assert.False(auditEntryExists);
    }

    [Fact]
    public async Task RevokeAccess_LastStudyAdmin_RejectedWhenCallerIsTheSoleAdminThemselves()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin4@test.com");

        var study = new Study { StudyCode = "EU-SELFADMIN" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var soleAdmin = await CreateUserAsync(services, "soleadmin4@test.com");
        await GrantAsync(context, soleAdmin.Id, study.StudyID, "StudyAdmin", sysAdmin.Id);

        var model = BuildPageModel(services, soleAdmin.Id);

        var result = await model.OnPostRevokeAccessAsync(soleAdmin.Id, study.StudyID);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("A study must have at least one Study Admin.", model.TempData["Error"]);

        var access = await context.UserStudyAccesses.AsNoTracking()
            .SingleAsync(a => a.AspNetUserId == soleAdmin.Id && a.StudyID == study.StudyID);
        Assert.Equal("StudyAdmin", access.Role);
    }

    [Fact]
    public async Task RevokeAccess_AnotherStudyAdminRemains_Succeeds_AndWritesAuditEntry()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin5@test.com");

        var study = new Study { StudyCode = "EU-TWOADMINS" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var adminA = await CreateUserAsync(services, "admina5@test.com");
        var adminB = await CreateUserAsync(services, "adminb5@test.com");
        await GrantAsync(context, adminA.Id, study.StudyID, "StudyAdmin", sysAdmin.Id);
        await GrantAsync(context, adminB.Id, study.StudyID, "StudyAdmin", sysAdmin.Id);

        var model = BuildPageModel(services, adminA.Id);

        var result = await model.OnPostRevokeAccessAsync(adminB.Id, study.StudyID);

        Assert.IsType<RedirectToPageResult>(result);

        var stillExists = await context.UserStudyAccesses.AsNoTracking()
            .AnyAsync(a => a.AspNetUserId == adminB.Id && a.StudyID == study.StudyID);
        Assert.False(stillExists);

        var auditEntryExists = await context.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.TableName == "tbl_UserStudyAccess" && a.NewValue == null && a.OldValue == "StudyAdmin");
        Assert.True(auditEntryExists);
    }

    [Fact]
    public async Task ChangeRole_DemotingLastStudyAdmin_RejectedEvenBySystemAdminCaller_RowUnchanged()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin6@test.com");

        var study = new Study { StudyCode = "EU-DEMOTELAST" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var soleAdmin = await CreateUserAsync(services, "soleadmin6@test.com");
        await GrantAsync(context, soleAdmin.Id, study.StudyID, "StudyAdmin", sysAdmin.Id);

        var model = BuildPageModel(services, sysAdmin.Id);

        var result = await model.OnPostChangeRoleAsync(soleAdmin.Id, study.StudyID, "Viewer");

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("A study must have at least one Study Admin.", model.TempData["Error"]);

        var access = await context.UserStudyAccesses.AsNoTracking()
            .SingleAsync(a => a.AspNetUserId == soleAdmin.Id && a.StudyID == study.StudyID);
        Assert.Equal("StudyAdmin", access.Role);
    }

    [Fact]
    public async Task ChangeRole_DemotingWithAnotherStudyAdminRemaining_Succeeds_AndWritesAuditEntry()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin7@test.com");

        var study = new Study { StudyCode = "EU-DEMOTEOK" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var adminA = await CreateUserAsync(services, "admina7@test.com");
        var adminB = await CreateUserAsync(services, "adminb7@test.com");
        await GrantAsync(context, adminA.Id, study.StudyID, "StudyAdmin", sysAdmin.Id);
        await GrantAsync(context, adminB.Id, study.StudyID, "StudyAdmin", sysAdmin.Id);

        var model = BuildPageModel(services, adminA.Id);

        var result = await model.OnPostChangeRoleAsync(adminB.Id, study.StudyID, "Viewer");

        Assert.IsType<RedirectToPageResult>(result);

        var access = await context.UserStudyAccesses.AsNoTracking()
            .SingleAsync(a => a.AspNetUserId == adminB.Id && a.StudyID == study.StudyID);
        Assert.Equal("Viewer", access.Role);

        var auditEntry = await context.AuditLogs.AsNoTracking()
            .SingleOrDefaultAsync(a => a.TableName == "tbl_UserStudyAccess" && a.RecordID == access.UserStudyAccessID
                && a.OldValue == "StudyAdmin" && a.NewValue == "Viewer");
        Assert.NotNull(auditEntry);
        Assert.Equal(adminA.Id, auditEntry!.ChangedByUserId);
    }

    [Fact]
    public async Task ChangeRole_CallerDoesNotManageStudy_IsRejectedServerSide_RowUnchanged()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin8@test.com");

        var managedStudy = new Study { StudyCode = "EU-CRMANAGED" };
        var unmanagedStudy = new Study { StudyCode = "EU-CRUNMANAGED" };
        context.Studies.AddRange(managedStudy, unmanagedStudy);
        await context.SaveChangesAsync();

        var studyAdmin = await CreateUserAsync(services, "studyadmin8@test.com");
        await GrantAsync(context, studyAdmin.Id, managedStudy.StudyID, "StudyAdmin", sysAdmin.Id);

        var viewerOnOtherStudy = await CreateUserAsync(services, "viewer8@test.com");
        await GrantAsync(context, viewerOnOtherStudy.Id, unmanagedStudy.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildPageModel(services, studyAdmin.Id);

        var result = await model.OnPostChangeRoleAsync(viewerOnOtherStudy.Id, unmanagedStudy.StudyID, "Editor");

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(model.TempData["Error"]);

        var access = await context.UserStudyAccesses.AsNoTracking()
            .SingleAsync(a => a.AspNetUserId == viewerOnOtherStudy.Id && a.StudyID == unmanagedStudy.StudyID);
        Assert.Equal("Viewer", access.Role);
    }
}
