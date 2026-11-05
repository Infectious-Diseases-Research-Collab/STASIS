using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;
using STASIS.Models;
using STASIS.Pages;
using STASIS.Pages.Administration;
using STASIS.Pages.Samples;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Security;

/// <summary>
/// End-to-end IDOR/security regression pass for the whole study-scoped-access-control stack
/// (Tasks 1-14): tampered form fields, zero-access users, mid-session revocation, and System
/// Admin override traceability. Requires STASIS_TEST_CONNECTION; see
/// <see cref="TestWebApplicationFactory"/>.
///
/// Most scenarios instantiate page models directly (mirroring every other Integration test in
/// this project), since the behavior under test -- server-side re-validation of a posted/queried
/// value against <see cref="IStudyAccessService"/> -- does not depend on the HTTP/cookie
/// pipeline. The two "mid-session revocation" scenarios are the exception: they drive a real
/// <see cref="TestWebApplicationFactory.CreateClient"/> HTTP round trip, authenticating via a
/// real POST to the Identity login page rather than a hand-built <see cref="ClaimsPrincipal"/>,
/// because what they're testing -- whether ASP.NET Core Identity's cookie authentication
/// re-checks state on every request -- has no meaning against a page model instantiated
/// directly (there is no cookie to go stale).
///
/// There are two such tests, not one, because empirically only one of them actually depends on
/// the Program.cs fix (Step 3 below):
/// <list type="bullet">
/// <item><see cref="RevokedMidSession_LosesStudyAccessOnVeryNextRequest_NotJustAfterFreshLogin"/>
/// revokes a <c>UserStudyAccess</c> row mid-session (the scenario this task's brief describes).
/// Every <see cref="IStudyAccessService"/> check (<c>CanViewAsync</c>, <c>GetVisibleStudyIdsAsync</c>,
/// etc.) queries the database fresh on every request -- none of it is cached in the auth cookie's
/// claims -- so this test passes identically whether or not the Program.cs fix is applied. Kept
/// as a real regression test of that already-correct behavior, with its passing status
/// independent of the fix documented inline.</item>
/// <item><see cref="SystemAdminRoleRevokedMidSession_LosesAccessOnVeryNextRequest_NotJustAfterFreshLogin"/>
/// is the test that actually exercises the gap: several pages (e.g.
/// <c>Pages/LabSetup/Studies.cshtml.cs</c>) still gate access with
/// <c>[Authorize(Roles = "Admin")]</c>, which checks role claims embedded in the auth cookie at
/// sign-in time -- not a live database query. Revoking the Identity "Admin" role mid-session and
/// confirmed empirically (see task-15-report.md) to leave the stale cookie granting access on
/// the very next request until Program.cs's <c>SecurityStampValidatorOptions.ValidationInterval</c>
/// fix is applied, at which point the same request correctly redirects to AccessDenied instead.</item>
/// </list>
/// </summary>
public class StudyAccessIdorTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public StudyAccessIdorTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // Satisfies Program.cs's AddDefaultIdentity password policy (digit, lower, upper,
    // non-alphanumeric, length >= 6) for the one scenario that logs in over real HTTP.
    private const string TestPassword = "Test-Pw9x!";

    // ---- shared test helpers (mirror AddSampleAccessTests/SamplesVisibilityTests/EditUserAccessTests) ----

    private static AddModel BuildAddModel(IServiceProvider services, string callerUserId)
    {
        var sampleService = services.GetRequiredService<ISampleService>();
        var storageService = services.GetRequiredService<IStorageService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new AddModel(sampleService, storageService, studyAccessService)
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

    private static EditUserModel BuildEditUserModel(IServiceProvider services, string callerUserId)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var context = services.GetRequiredService<StasisDbContext>();
        var auditService = services.GetRequiredService<IAuditService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new EditUserModel(userManager, context, auditService, studyAccessService)
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

    // Creates a user with a real password and a confirmed email, so it can actually complete the
    // real HTTP login flow (Program.cs sets RequireConfirmedAccount = true).
    private static async Task<IdentityUser> CreateLoginableUserAsync(IServiceProvider services, string email)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, TestPassword);
        Assert.True(result.Succeeded, string.Join(";", result.Errors.Select(e => e.Description)));
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

    private static async Task<SampleType> AddSampleTypeAsync(StasisDbContext context, string typeName)
    {
        var sampleType = new SampleType { TypeName = typeName };
        context.SampleTypes.Add(sampleType);
        await context.SaveChangesAsync();
        return sampleType;
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

    // Extracts the Razor Pages antiforgery token embedded by the FormTagHelper so a raw
    // HttpClient POST (no browser, no tag helpers) can satisfy the AutoValidateAntiforgeryToken
    // filter that Razor Pages applies to every POST by default.
    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            // Attribute order can vary; try value-before-name too.
            match = Regex.Match(
                html,
                "value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"",
                RegexOptions.IgnoreCase);
        }
        Assert.True(match.Success, "Could not find __RequestVerificationToken in login page HTML.");
        return match.Groups[1].Value;
    }

    // Drives the real Identity login page over HTTP (GET for the antiforgery token, then POST
    // credentials) so the returned client carries a genuine ASP.NET Core Identity auth cookie --
    // not a hand-built ClaimsPrincipal. TestWebApplicationFactory.CreateClient() handles cookies
    // and redirects automatically, so the same HttpClient instance can be reused for subsequent
    // authenticated requests.
    private static async Task<HttpClient> LoginAsync(TestWebApplicationFactory factory, string email, string password)
    {
        var client = factory.CreateClient();

        var loginPageResponse = await client.GetAsync("/Identity/Account/Login");
        loginPageResponse.EnsureSuccessStatusCode();
        var loginPageHtml = await loginPageResponse.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(loginPageHtml);

        var form = new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "false",
            ["__RequestVerificationToken"] = token
        };

        var postResponse = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(form));
        Assert.True(
            postResponse.StatusCode == HttpStatusCode.OK,
            $"Login POST did not complete successfully (status {postResponse.StatusCode}). " +
            "A non-OK, non-redirect-followed-to-OK result usually means the login failed " +
            "(invalid credentials, unconfirmed email, or antiforgery mismatch).");

        return client;
    }

    [Fact]
    public async Task SamplesAdd_ViewerPostingTamperedStudyIdForStudyTheyCannotEdit_RejectedWithModelStateError_NoSpecimenCreated()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-idor1@test.com");

        var targetStudy = new Study { StudyCode = "IDOR-TARGET" };
        context.Studies.Add(targetStudy);
        await context.SaveChangesAsync();

        var sampleType = await AddSampleTypeAsync(context, "IDOR-Type-1");

        // Caller is a Viewer -- not Editor -- on the exact study they are about to post. Viewer
        // access alone must never be enough to write a specimen; only CanEditAsync (Editor or
        // above) should allow it.
        var viewer = await CreateUserAsync(services, "viewer-idor1@test.com");
        await GrantAsync(context, viewer.Id, targetStudy.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildAddModel(services, viewer.Id);
        model.ParticipantID = "IDOR-P1";
        model.StudyID = targetStudy.StudyID;
        model.Samples = new List<AddModel.SampleInput>
        {
            new AddModel.SampleInput { BarcodeID = "IDORBARCODE1", SampleTypeID = sampleType.SampleTypeID }
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);

        var saved = await context.Specimens.AnyAsync(s => s.BarcodeID == "IDORBARCODE1");
        Assert.False(saved);
    }

    [Fact]
    public async Task Samples_UserWithZeroStudyAssignments_SeesEmptyResultsAndEmptyStudyDropdown()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-idor2@test.com");

        var study = new Study { StudyCode = "IDOR-UNSEEN" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        await AddSpecimenAsync(context, study.StudyID, "IDORBARCODE2");

        // Caller exists but has no UserStudyAccess row for any study -- a brand-new user before
        // ever being granted access to anything.
        var noAccessUser = await CreateUserAsync(services, "noaccess-idor2@test.com");

        var model = BuildSamplesModel(services, noAccessUser.Id);

        await model.OnGetAsync(null);

        Assert.Empty(model.Specimens);
        Assert.Empty(model.Studies.Cast<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>());
        _ = sysAdmin; // sysAdmin is only needed to seed the GrantedByUserId FK elsewhere; unused directly here.
    }

    [Fact]
    public async Task RevokedMidSession_LosesStudyAccessOnVeryNextRequest_NotJustAfterFreshLogin()
    {
        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedServices = seedScope.ServiceProvider;
            var context = seedServices.GetRequiredService<StasisDbContext>();

            var sysAdmin = await CreateSystemAdminAsync(seedServices, "sysadmin-idor3@test.com");

            var study = new Study { StudyCode = "IDOR-REVOKE" };
            context.Studies.Add(study);
            await context.SaveChangesAsync();

            await AddSpecimenAsync(context, study.StudyID, "IDORBARCODE3");

            var viewer = await CreateLoginableUserAsync(seedServices, "viewer-idor3@test.com");
            await GrantAsync(context, viewer.Id, study.StudyID, "Viewer", sysAdmin.Id);

            // Real HTTP login -- a genuine ASP.NET Core Identity auth cookie, not a hand-built
            // ClaimsPrincipal, because this scenario specifically tests whether the cookie's
            // own re-validation picks up the revocation without a fresh login.
            var client = await LoginAsync(_factory, "viewer-idor3@test.com", TestPassword);

            // Confirm access is actually granted before revoking anything.
            var beforeRevoke = await client.GetAsync("/Samples");
            beforeRevoke.EnsureSuccessStatusCode();
            var beforeHtml = await beforeRevoke.Content.ReadAsStringAsync();
            Assert.Contains("IDORBARCODE3", beforeHtml);

            // Simulate EditUser's OnPostRevokeAccessAsync handler (Task 7) revoking the viewer's
            // study access mid-session -- called directly against the service layer here (rather
            // than over HTTP as a second authenticated admin) since the caller's own identity is
            // not what this test is verifying; RevokeAccessAsync is exactly what that handler
            // calls.
            var studyAccessService = seedServices.GetRequiredService<IStudyAccessService>();
            await studyAccessService.RevokeAccessAsync(viewer.Id, study.StudyID, sysAdmin.Id);

            // Same HttpClient, same auth cookie, no logout/login in between -- the very next
            // request must no longer show the revoked study's specimen. This passes regardless
            // of the Program.cs SecurityStampValidator fix (Step 3): IStudyAccessService queries
            // tbl_UserStudyAccess fresh from the database on every request -- nothing about study
            // access is cached in the auth cookie's claims, so there is no staleness window here
            // to begin with. See this class's doc comment and
            // SystemAdminRoleRevokedMidSession_LosesAccessOnVeryNextRequest_NotJustAfterFreshLogin
            // below for the scenario that actually depends on that fix.
            var afterRevoke = await client.GetAsync("/Samples");
            afterRevoke.EnsureSuccessStatusCode();
            var afterHtml = await afterRevoke.Content.ReadAsStringAsync();
            Assert.DoesNotContain("IDORBARCODE3", afterHtml);
        }
    }

    [Fact]
    public async Task SystemAdminRoleRevokedMidSession_LosesAccessOnVeryNextRequest_NotJustAfterFreshLogin()
    {
        using var seedScope = _factory.Services.CreateScope();
        var seedServices = seedScope.ServiceProvider;
        var userManager = seedServices.GetRequiredService<UserManager<IdentityUser>>();

        var admin = await CreateSystemAdminAsync(seedServices, "sysadmin-idor5@test.com");
        admin.EmailConfirmed = true;
        await userManager.UpdateAsync(admin);
        await userManager.AddPasswordAsync(admin, TestPassword);

        // Real HTTP login -- a genuine ASP.NET Core Identity auth cookie. The System Admin role
        // claim is baked into this cookie's serialized ClaimsPrincipal at sign-in time.
        var client = await LoginAsync(_factory, "sysadmin-idor5@test.com", TestPassword);

        // Pages/LabSetup/Studies.cshtml.cs is still gated by [Authorize(Roles = "Admin")], which
        // checks the cookie's role claim directly -- not a live database lookup like
        // IStudyAccessService. Confirm access is granted before revoking anything.
        var beforeRevoke = await client.GetAsync("/LabSetup/Studies");
        beforeRevoke.EnsureSuccessStatusCode();
        Assert.EndsWith("/LabSetup/Studies", beforeRevoke.RequestMessage!.RequestUri!.AbsolutePath);

        // Revoke the System Admin role mid-session -- directly against UserManager, standing in
        // for whatever future admin-revocation feature exercises this (the design doc calls for
        // System Admin grant/revoke on other users; no page in Tasks 1-14 implements the revoke
        // side yet, so there is no page-model handler to call here).
        await userManager.RemoveFromRoleAsync(admin, "Admin");

        // Same HttpClient, same auth cookie, no logout/login in between. Before the
        // SecurityStampValidatorOptions.ValidationInterval fix (Program.cs Step 3), ASP.NET Core
        // Identity's cookie middleware only re-validates -- and so only refreshes the stale role
        // claim -- on its default 30-minute interval, so the very next request here would still
        // carry the old "Admin" claim and land back on /LabSetup/Studies. Confirmed empirically
        // against this exact app without the fix (see task-15-report.md). With the fix, the
        // cookie is re-validated on this request, the refreshed principal no longer has the
        // "Admin" claim, and [Authorize(Roles = "Admin")] redirects to Identity's AccessDenied
        // page instead -- auto-followed here since the HttpClient allows redirects by default.
        var afterRevoke = await client.GetAsync("/LabSetup/Studies");
        afterRevoke.EnsureSuccessStatusCode();
        Assert.Contains("AccessDenied", afterRevoke.RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SystemAdmin_GrantingAccessToStudyWithNoPersonalAccessRow_Succeeds_AndAuditEntryIsAttributable()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        // sysAdmin deliberately gets NO UserStudyAccess row of their own on orphanStudy -- a
        // System Admin's global role, not a personal assignment, is what lets them act here.
        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-idor4@test.com");

        var orphanStudy = new Study { StudyCode = "IDOR-ORPHAN" };
        context.Studies.Add(orphanStudy);
        await context.SaveChangesAsync();

        Assert.Equal(StudyRole.None, await services.GetRequiredService<IStudyAccessService>()
            .GetRoleAsync(sysAdmin.Id, orphanStudy.StudyID));

        var targetUser = await CreateUserAsync(services, "target-idor4@test.com");

        var model = BuildEditUserModel(services, sysAdmin.Id);

        var result = await model.OnPostGrantAccessAsync(targetUser.Id, orphanStudy.StudyID, "Viewer");

        Assert.IsType<RedirectToPageResult>(result);

        var grantedRole = await services.GetRequiredService<IStudyAccessService>()
            .GetRoleAsync(targetUser.Id, orphanStudy.StudyID);
        Assert.Equal(StudyRole.Viewer, grantedRole);

        // IStudyAccessService.GrantAccessAsync writes its own "tbl_UserStudyAccess" audit entry.
        // The design doc (docs/superpowers/specs/2026-10-21-study-scoped-access-control-design.md,
        // "Decided design" -> System Admin) calls for System Admin actions on a study they are
        // not personally assigned to to be logged with a distinct "SystemAdminOverride" action
        // tag. No task in this plan (1-14) actually added that distinct tag anywhere in the
        // codebase -- verified by exhaustive search across StudyAccessService, every page model
        // that calls it, and SampleService/StorageService's own LogChangeAsync call sites, plus
        // the plan's own progress ledger, none of which mention "SystemAdminOverride". Per this
        // task's brief, adding that new tagging is out of scope here ("not to add new logging").
        // So this test instead asserts the weaker, already-true property the brief actually asks
        // to confirm: the resulting audit entry exists and is attributable to the acting System
        // Admin and the target study via the fields LogChangeAsync already writes
        // (ChangedByUserId, NewValue containing the StudyID). See task-15-report.md for the full
        // writeup of this gap.
        var auditEntry = await context.AuditLogs
            .Where(a => a.TableName == "tbl_UserStudyAccess" && a.FieldName == "Created")
            .OrderByDescending(a => a.AuditLogID)
            .FirstOrDefaultAsync(a => a.NewValue != null && a.NewValue.Contains($"StudyID: {orphanStudy.StudyID}"));

        Assert.NotNull(auditEntry);
        Assert.Equal(sysAdmin.Id, auditEntry!.ChangedByUserId);
        Assert.Contains($"UserId: {targetUser.Id}", auditEntry.NewValue);
        Assert.Contains("Role: Viewer", auditEntry.NewValue);
    }
}
