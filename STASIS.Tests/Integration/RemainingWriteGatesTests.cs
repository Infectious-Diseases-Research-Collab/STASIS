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
using STASIS.Pages.Samples;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies the last four write-gated pages' per-study Editor access enforcement, replacing the
/// global <c>[Authorize(Roles = "Write,Admin")]</c> each used to carry:
/// <list type="bullet">
/// <item><c>Samples/Discard</c> (<see cref="DiscardModel.OnPostRequestAsync"/>): a mixed batch of
/// editable and non-editable-study barcodes discards only the editable ones (per-specimen skip,
/// not a whole-request reject), mirroring Task 13's per-row CSV import skip;</item>
/// <item><c>Boxes/Move</c> (<see cref="MoveModel.OnPostMoveAsync"/>): re-validates
/// <see cref="IStudyAccessService.CanEditAsync"/> against the specimen's current StudyID before
/// calling into <see cref="IStorageService"/> -- an IDOR guard, since the box/specimen are
/// resolved server-side from the posted ids, not trusted blindly;</item>
/// <item><c>Boxes/Rebox</c> (<see cref="ReboxModel.OnPostCommitAsync"/>): same per-item skip
/// philosophy as Discard, applied to a batch of scanned placements -- a mix of editable and
/// non-editable-study specimens reboxes only the editable ones;</item>
/// <item><c>Boxes/Create</c> (<see cref="CreateModel.OnGetAsync"/>/<see cref="CreateModel.OnPostAsync"/>):
/// box creation isn't tied to a specific study, so the gate is "does this caller hold
/// Editor-or-above access to at least one study" -- <c>Forbid()</c> for a caller with none.</item>
/// </list>
/// Page models are instantiated directly with services resolved from the real DI container and a
/// hand-built <see cref="ClaimsPrincipal"/> standing in for the authenticated caller, mirroring
/// <c>AddSampleAccessTests</c>/<c>ImportAccessTests</c>. Requires STASIS_TEST_CONNECTION; see
/// <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class RemainingWriteGatesTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public RemainingWriteGatesTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static ClaimsPrincipal BuildPrincipal(string callerUserId) =>
        new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, callerUserId) },
            "TestAuth"));

    private static DiscardModel BuildDiscardModel(IServiceProvider services, string callerUserId)
    {
        var sampleService = services.GetRequiredService<ISampleService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new DiscardModel(sampleService, studyAccessService)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services,
                    User = BuildPrincipal(callerUserId)
                }
            },
            TempData = new TempDataDictionary(
                new DefaultHttpContext { RequestServices = services },
                services.GetRequiredService<ITempDataProvider>())
        };
    }

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
                    User = BuildPrincipal(callerUserId)
                }
            },
            TempData = new TempDataDictionary(
                new DefaultHttpContext { RequestServices = services },
                services.GetRequiredService<ITempDataProvider>())
        };
    }

    private static ReboxModel BuildReboxModel(IServiceProvider services, string callerUserId)
    {
        var sampleService = services.GetRequiredService<ISampleService>();
        var storageService = services.GetRequiredService<IStorageService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new ReboxModel(sampleService, storageService, studyAccessService)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services,
                    User = BuildPrincipal(callerUserId)
                }
            },
            TempData = new TempDataDictionary(
                new DefaultHttpContext { RequestServices = services },
                services.GetRequiredService<ITempDataProvider>())
        };
    }

    private static CreateModel BuildCreateModel(IServiceProvider services, string callerUserId)
    {
        var storageService = services.GetRequiredService<IStorageService>();
        var sampleService = services.GetRequiredService<ISampleService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new CreateModel(storageService, sampleService, studyAccessService)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services,
                    User = BuildPrincipal(callerUserId)
                }
            },
            TempData = new TempDataDictionary(
                new DefaultHttpContext { RequestServices = services },
                services.GetRequiredService<ITempDataProvider>())
        };
    }

    private static PlaceModel BuildPlaceModel(IServiceProvider services, string callerUserId)
    {
        var storageService = services.GetRequiredService<IStorageService>();
        var sampleService = services.GetRequiredService<ISampleService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new PlaceModel(storageService, sampleService, studyAccessService)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services,
                    User = BuildPrincipal(callerUserId)
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

    private static async Task<Box> AddBoxAsync(StasisDbContext context, string label, int? studyId)
    {
        var box = new Box { BoxLabel = label, BoxType = "81-slot", BoxCategory = "Standard", StudyID = studyId };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();
        return box;
    }

    private static async Task<Specimen> AddSpecimenAsync(StasisDbContext context, string barcode, int studyId, int? boxId, int? row, int? col, string status = "In-Stock")
    {
        var specimen = new Specimen
        {
            BarcodeID = barcode,
            StudyID = studyId,
            BoxID = boxId,
            PositionRow = row,
            PositionCol = col,
            Status = status
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync();
        return specimen;
    }

    // ---------------------------------------------------------------------
    // Samples/Discard
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Discard_OnPostRequestAsync_MixOfEditableAndNonEditableStudySpecimens_DiscardsOnlyEditableOnes()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg1@test.com");

        var editableStudy = new Study { StudyCode = "RWG-D-EDIT" };
        var otherStudy = new Study { StudyCode = "RWG-D-OTHER" };
        context.Studies.AddRange(editableStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg1@test.com");
        await GrantAsync(context, caller.Id, editableStudy.StudyID, "Editor", sysAdmin.Id);

        var editableSpecimen = await AddSpecimenAsync(context, "RWGD-OK1", editableStudy.StudyID, null, null, null);
        var blockedSpecimen = await AddSpecimenAsync(context, "RWGD-BLOCKED1", otherStudy.StudyID, null, null, null);

        var model = BuildDiscardModel(services, caller.Id);
        model.Barcodes = "RWGD-OK1,RWGD-BLOCKED1";

        var result = await model.OnPostRequestAsync();

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.CreatedApproval);

        var approvalContents = await context.Specimens
            .Where(s => s.DiscardApprovalID == model.CreatedApproval!.ApprovalID)
            .ToListAsync();

        Assert.Contains(approvalContents, s => s.SpecimenID == editableSpecimen.SpecimenID);
        Assert.DoesNotContain(approvalContents, s => s.SpecimenID == blockedSpecimen.SpecimenID);

        // The blocked specimen's own fields are untouched.
        var reloadedBlocked = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == blockedSpecimen.SpecimenID);
        Assert.Null(reloadedBlocked.DiscardApprovalID);
    }

    [Fact]
    public async Task Discard_OnPostRequestAsync_AllSpecimensNonEditable_AddsModelStateError_NoApprovalCreated()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg2@test.com");

        var callerStudy = new Study { StudyCode = "RWG-D-MINE2" };
        var otherStudy = new Study { StudyCode = "RWG-D-OTHER2" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg2@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Editor", sysAdmin.Id);

        await AddSpecimenAsync(context, "RWGD-BLOCKED2", otherStudy.StudyID, null, null, null);

        var model = BuildDiscardModel(services, caller.Id);
        model.Barcodes = "RWGD-BLOCKED2";

        var result = await model.OnPostRequestAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Null(model.CreatedApproval);
    }

    [Fact]
    public async Task Discard_OnPostRequestAsync_MixedBatchWithHiddenStudySpecimen_HiddenOneNotRevealedInMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg-disc2@test.com");

        var hiddenStudy = new Study { StudyCode = "RWG-DISC-HIDDEN" };
        var visibleStudy = new Study { StudyCode = "RWG-DISC-VISIBLE" };
        context.Studies.AddRange(hiddenStudy, visibleStudy);
        await context.SaveChangesAsync();

        // Caller has zero access to hiddenStudy -- cannot even view it -- but is an Editor on
        // visibleStudy. Posting both barcodes together is the only way to distinguish the fixed
        // behavior (hidden one silently dropped, visible one discarded) from the unfixed one
        // (hidden one reaches the CanEditAsync-fails branch and gets named in the Skipped section
        // of the success message, revealing its existence and StudyCode).
        var caller = await CreateUserAsync(services, "noaccess-rwg-disc2@test.com");
        await GrantAsync(context, caller.Id, visibleStudy.StudyID, "Editor", sysAdmin.Id);

        await AddSpecimenAsync(context, "RWG-DISC-HIDDEN-001", hiddenStudy.StudyID, null, null, null);
        var visibleSpecimen = await AddSpecimenAsync(context, "RWG-DISC-VISIBLE-001", visibleStudy.StudyID, null, null, null);

        var model = BuildDiscardModel(services, caller.Id);
        model.Barcodes = "RWG-DISC-HIDDEN-001,RWG-DISC-VISIBLE-001";

        var result = await model.OnPostRequestAsync();

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.CreatedApproval);

        var approvalContents = await context.Specimens
            .Where(s => s.DiscardApprovalID == model.CreatedApproval!.ApprovalID)
            .ToListAsync();
        Assert.Contains(approvalContents, s => s.SpecimenID == visibleSpecimen.SpecimenID);

        // Only the visible specimen was discarded -- the hidden one was silently dropped by the
        // CanViewAsync gate, not routed into the Skipped-with-reason branch.
        Assert.Single(approvalContents);

        Assert.True(model.TempData.ContainsKey("Success"));
        var message = (string)model.TempData["Success"]!;

        // The visible, editable specimen's discard is reported...
        Assert.Contains("for 1 specimen(s)", message);

        // ...but the hidden study must not be named anywhere in the message, including any
        // "Skipped" section -- this is the actual leak OnPostLookupAsync's CanViewAsync gate
        // guards against. Unfixed code would reach the CanEditAsync-fails branch for the hidden
        // specimen and append it to the Skipped section, naming its barcode and StudyCode.
        Assert.DoesNotContain("Skipped", message);
        Assert.DoesNotContain("RWG-DISC-HIDDEN", message);
        Assert.DoesNotContain("RWG-DISC-HIDDEN-001", message);
    }

    // ---------------------------------------------------------------------
    // Boxes/Move
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Move_OnPostMoveAsync_SpecimenInNonEditableStudy_IsRejected_NotMoved()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg3@test.com");

        var callerStudy = new Study { StudyCode = "RWG-M-MINE" };
        var otherStudy = new Study { StudyCode = "RWG-M-OTHER" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg3@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Editor", sysAdmin.Id);

        var destBox = await AddBoxAsync(context, "RWG-M-BOX1", null);
        var specimen = await AddSpecimenAsync(context, "RWGM-BLOCKED1", otherStudy.StudyID, null, null, null);

        var model = BuildMoveModel(services, caller.Id);
        model.SpecimenId = specimen.SpecimenID;
        model.NewBoxId = destBox.BoxID;
        model.NewRow = 1;
        model.NewCol = 1;

        var result = await model.OnPostMoveAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);

        var reloaded = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen.SpecimenID);
        Assert.Null(reloaded.BoxID);
    }

    [Fact]
    public async Task Move_OnPostMoveAsync_SpecimenInEditableStudy_IsMoved()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg4@test.com");

        var callerStudy = new Study { StudyCode = "RWG-M-MINE2" };
        context.Studies.Add(callerStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg4@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Editor", sysAdmin.Id);

        var destBox = await AddBoxAsync(context, "RWG-M-BOX2", null);
        var specimen = await AddSpecimenAsync(context, "RWGM-OK1", callerStudy.StudyID, null, null, null);

        var model = BuildMoveModel(services, caller.Id);
        model.SpecimenId = specimen.SpecimenID;
        model.NewBoxId = destBox.BoxID;
        model.NewRow = 1;
        model.NewCol = 1;

        var result = await model.OnPostMoveAsync();

        Assert.IsType<RedirectToPageResult>(result);

        var reloaded = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen.SpecimenID);
        Assert.Equal(destBox.BoxID, reloaded.BoxID);
        Assert.Equal(1, reloaded.PositionRow);
        Assert.Equal(1, reloaded.PositionCol);
    }

    [Fact]
    public async Task Move_OnGetAsync_BoxDropdown_ExcludesBoxAssignedToStudyCallerCannotSee()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg5@test.com");

        var callerStudy = new Study { StudyCode = "RWG-M-DROPDOWN" };
        var otherStudy = new Study { StudyCode = "RWG-M-DROPDOWN-OTHER" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg5@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Editor", sysAdmin.Id);

        var myBox = await AddBoxAsync(context, "RWG-M-DROPDOWN-BOX1", callerStudy.StudyID);
        var otherBox = await AddBoxAsync(context, "RWG-M-DROPDOWN-BOX2", otherStudy.StudyID);
        var unassignedBox = await AddBoxAsync(context, "RWG-M-DROPDOWN-BOX3", null);

        var model = BuildMoveModel(services, caller.Id);

        await model.OnGetAsync();

        var boxIds = model.BoxOptions.Cast<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>()
            .Select(i => int.Parse(i.Value!))
            .ToList();

        Assert.Contains(myBox.BoxID, boxIds);
        Assert.Contains(unassignedBox.BoxID, boxIds);
        Assert.DoesNotContain(otherBox.BoxID, boxIds);
    }

    // ---------------------------------------------------------------------
    // Boxes/Rebox
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Rebox_OnPostCommitAsync_MixOfEditableAndNonEditableStudySpecimens_ReboxesOnlyEditableOnes()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg6@test.com");

        var editableStudy = new Study { StudyCode = "RWG-R-EDIT" };
        var otherStudy = new Study { StudyCode = "RWG-R-OTHER" };
        context.Studies.AddRange(editableStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg6@test.com");
        await GrantAsync(context, caller.Id, editableStudy.StudyID, "Editor", sysAdmin.Id);

        var destBox = await AddBoxAsync(context, "RWG-R-BOX1", null);
        var editableSpecimen = await AddSpecimenAsync(context, "RWGR-OK1", editableStudy.StudyID, null, null, null);
        var blockedSpecimen = await AddSpecimenAsync(context, "RWGR-BLOCKED1", otherStudy.StudyID, null, null, null);

        var model = BuildReboxModel(services, caller.Id);
        model.DestinationBoxId = destBox.BoxID;
        model.ScannedListJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { SpecimenId = editableSpecimen.SpecimenID, Row = 1, Col = 1 },
            new { SpecimenId = blockedSpecimen.SpecimenID, Row = 1, Col = 2 }
        });

        var result = await model.OnPostCommitAsync();

        Assert.IsType<RedirectToPageResult>(result);

        var reloadedEditable = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == editableSpecimen.SpecimenID);
        Assert.Equal(destBox.BoxID, reloadedEditable.BoxID);

        var reloadedBlocked = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == blockedSpecimen.SpecimenID);
        Assert.Null(reloadedBlocked.BoxID);
    }

    [Fact]
    public async Task Rebox_OnPostCommitAsync_AllSpecimensNonEditable_AddsModelStateError_NothingReboxed()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg7@test.com");

        var callerStudy = new Study { StudyCode = "RWG-R-MINE2" };
        var otherStudy = new Study { StudyCode = "RWG-R-OTHER2" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg7@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Editor", sysAdmin.Id);

        var destBox = await AddBoxAsync(context, "RWG-R-BOX2", null);
        var blockedSpecimen = await AddSpecimenAsync(context, "RWGR-BLOCKED2", otherStudy.StudyID, null, null, null);

        var model = BuildReboxModel(services, caller.Id);
        model.DestinationBoxId = destBox.BoxID;
        model.ScannedListJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { SpecimenId = blockedSpecimen.SpecimenID, Row = 1, Col = 1 }
        });

        var result = await model.OnPostCommitAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);

        var reloaded = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == blockedSpecimen.SpecimenID);
        Assert.Null(reloaded.BoxID);
    }

    // ---------------------------------------------------------------------
    // Boxes/Create
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_OnGetAsync_CallerWithNoEditorAccessAnywhere_ReturnsForbid()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg8@test.com");

        var study = new Study { StudyCode = "RWG-C-VIEWONLY" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        // Caller has Viewer access only -- no Editor access to any study.
        var caller = await CreateUserAsync(services, "viewer-rwg8@test.com");
        await GrantAsync(context, caller.Id, study.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildCreateModel(services, caller.Id);

        var result = await model.OnGetAsync();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_OnGetAsync_CallerWithZeroStudyAssignments_ReturnsForbid()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var caller = await CreateUserAsync(services, "noaccess-rwg9@test.com");

        var model = BuildCreateModel(services, caller.Id);

        var result = await model.OnGetAsync();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_OnPostAsync_CallerWithNoEditorAccessAnywhere_ReturnsForbid_NoBoxCreated()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg10@test.com");

        var study = new Study { StudyCode = "RWG-C-VIEWONLY2" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "viewer-rwg10@test.com");
        await GrantAsync(context, caller.Id, study.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildCreateModel(services, caller.Id);
        model.BoxLabel = "RWG-C-SHOULD-NOT-EXIST";
        model.BoxType = "81-slot";

        var result = await model.OnPostAsync();

        Assert.IsType<ForbidResult>(result);

        var boxExists = await context.Boxes.AnyAsync(b => b.BoxLabel == "RWG-C-SHOULD-NOT-EXIST");
        Assert.False(boxExists);
    }

    [Fact]
    public async Task Create_OnPostAsync_CallerIsEditorOnAtLeastOneStudy_CreatesBox()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg11@test.com");

        var study = new Study { StudyCode = "RWG-C-EDIT" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg11@test.com");
        await GrantAsync(context, caller.Id, study.StudyID, "Editor", sysAdmin.Id);

        var model = BuildCreateModel(services, caller.Id);
        model.BoxLabel = "RWG-C-CREATED";
        model.BoxType = "81-slot";

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);

        var box = await context.Boxes.SingleOrDefaultAsync(b => b.BoxLabel == "RWG-C-CREATED");
        Assert.NotNull(box);
    }

    // ---------------------------------------------------------------------
    // Review follow-up: lookup/helper handlers must not leak existence of specimens in
    // studies the caller cannot even view. These three mirror Pages/Samples/Detail.cshtml.cs's
    // CanViewAsync-then-"not found" guard, applied to the lookup paths that lost their
    // class-level [Authorize(Roles = "Write,Admin")] gate in this task.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Discard_OnPostLookupAsync_SpecimenInStudyCallerCannotView_IsTreatedAsNotFound()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg12@test.com");

        var callerStudy = new Study { StudyCode = "RWG-D-LOOKUP-MINE" };
        var otherStudy = new Study { StudyCode = "RWG-D-LOOKUP-OTHER" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        // Caller has zero access (not even Viewer) to otherStudy.
        var caller = await CreateUserAsync(services, "viewer-rwg12@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Viewer", sysAdmin.Id);

        await AddSpecimenAsync(context, "RWGD-HIDDEN1", otherStudy.StudyID, null, null, null);

        var model = BuildDiscardModel(services, caller.Id);
        model.Barcodes = "RWGD-HIDDEN1";

        var result = await model.OnPostLookupAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(model.FoundSpecimens);
        Assert.Empty(model.InvalidBarcodes);
        Assert.Contains("RWGD-HIDDEN1", model.NotFoundBarcodes);
    }

    [Fact]
    public async Task Move_OnGetAsync_SpecimenInStudyCallerCannotView_IsTreatedAsNotFound()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg13@test.com");

        var callerStudy = new Study { StudyCode = "RWG-M-LOOKUP-MINE" };
        var otherStudy = new Study { StudyCode = "RWG-M-LOOKUP-OTHER" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "viewer-rwg13@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Viewer", sysAdmin.Id);

        var hiddenSpecimen = await AddSpecimenAsync(context, "RWGM-HIDDEN1", otherStudy.StudyID, null, null, null);

        var modelBySpecimenId = BuildMoveModel(services, caller.Id);
        modelBySpecimenId.SpecimenId = hiddenSpecimen.SpecimenID;

        await modelBySpecimenId.OnGetAsync();

        Assert.Null(modelBySpecimenId.FoundSpecimen);

        var modelByBarcode = BuildMoveModel(services, caller.Id);
        modelByBarcode.Barcode = "RWGM-HIDDEN1";

        await modelByBarcode.OnGetAsync();

        Assert.Null(modelByBarcode.FoundSpecimen);
        Assert.Empty(modelByBarcode.AmbiguousSpecimens);
    }

    [Fact]
    public async Task Rebox_OnGetLookupAsync_SpecimenInStudyCallerCannotView_ReturnsFoundFalse()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg14@test.com");

        var callerStudy = new Study { StudyCode = "RWG-R-LOOKUP-MINE" };
        var otherStudy = new Study { StudyCode = "RWG-R-LOOKUP-OTHER" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "viewer-rwg14@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Viewer", sysAdmin.Id);

        var hiddenSpecimen = await AddSpecimenAsync(context, "RWGR-HIDDEN1", otherStudy.StudyID, null, null, null);

        var modelBySpecimenId = BuildReboxModel(services, caller.Id);
        var resultBySpecimenId = await modelBySpecimenId.OnGetLookupAsync("RWGR-HIDDEN1", hiddenSpecimen.SpecimenID);

        var jsonBySpecimenId = Assert.IsType<JsonResult>(resultBySpecimenId);
        Assert.False((bool)jsonBySpecimenId.Value!.GetType().GetProperty("found")!.GetValue(jsonBySpecimenId.Value)!);

        var modelByBarcode = BuildReboxModel(services, caller.Id);
        var resultByBarcode = await modelByBarcode.OnGetLookupAsync("RWGR-HIDDEN1");

        var jsonByBarcode = Assert.IsType<JsonResult>(resultByBarcode);
        Assert.False((bool)jsonByBarcode.Value!.GetType().GetProperty("found")!.GetValue(jsonByBarcode.Value)!);
    }

    // ---------------------------------------------------------------------
    // Review follow-up: cross-study box-mixing attempts must surface as a ModelState error,
    // not an unhandled InvalidOperationException (same pattern Samples/Add already uses for
    // this exact exception from ValidateAndAssignBoxStudyAsync).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Move_OnPostMoveAsync_DestinationBoxHeldByDifferentStudy_AddsModelStateError_DoesNotThrow()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg15@test.com");

        var callerStudy = new Study { StudyCode = "RWG-M-MIX-MINE" };
        var otherStudy = new Study { StudyCode = "RWG-M-MIX-OTHER" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg15@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Editor", sysAdmin.Id);

        // Destination box already belongs to otherStudy.
        var destBox = await AddBoxAsync(context, "RWG-M-MIX-BOX1", otherStudy.StudyID);
        var specimen = await AddSpecimenAsync(context, "RWGM-MIX1", callerStudy.StudyID, null, null, null);

        var model = BuildMoveModel(services, caller.Id);
        model.SpecimenId = specimen.SpecimenID;
        model.NewBoxId = destBox.BoxID;
        model.NewRow = 1;
        model.NewCol = 1;

        var result = await model.OnPostMoveAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);

        var reloaded = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen.SpecimenID);
        Assert.Null(reloaded.BoxID);
    }

    [Fact]
    public async Task Rebox_OnPostCommitAsync_DestinationBoxHeldByDifferentStudy_AddsModelStateError_DoesNotThrow()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg16@test.com");

        var callerStudy = new Study { StudyCode = "RWG-R-MIX-MINE" };
        var otherStudy = new Study { StudyCode = "RWG-R-MIX-OTHER" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg16@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Editor", sysAdmin.Id);

        var destBox = await AddBoxAsync(context, "RWG-R-MIX-BOX1", otherStudy.StudyID);
        var specimen = await AddSpecimenAsync(context, "RWGR-MIX1", callerStudy.StudyID, null, null, null);

        var model = BuildReboxModel(services, caller.Id);
        model.DestinationBoxId = destBox.BoxID;
        model.ScannedListJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { SpecimenId = specimen.SpecimenID, Row = 1, Col = 1 }
        });

        var result = await model.OnPostCommitAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);

        var reloaded = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen.SpecimenID);
        Assert.Null(reloaded.BoxID);
    }

    // ---------------------------------------------------------------------
    // Whole-branch review follow-up: Boxes/Place still carried the retired global
    // [Authorize(Roles = "Write,Admin")] attribute, which (since the "Write" Identity role is no
    // longer seeded/assignable) silently collapsed to System-Admin-only. Replaced with the same
    // "editor anywhere" gate Boxes/Create uses -- mirrors Create_OnPostAsync's equivalent test.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Place_OnPostAsync_CallerWithNoEditorAccessAnywhere_ReturnsForbid_BoxNotMoved()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg17@test.com");

        var study = new Study { StudyCode = "RWG-P-VIEWONLY" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        // Caller has Viewer access only -- no Editor access to any study.
        var caller = await CreateUserAsync(services, "viewer-rwg17@test.com");
        await GrantAsync(context, caller.Id, study.StudyID, "Viewer", sysAdmin.Id);

        var box = await AddBoxAsync(context, "RWG-P-BOX1", null);

        var model = BuildPlaceModel(services, caller.Id);
        model.BoxId = box.BoxID;
        model.NewRackId = 999;

        var result = await model.OnPostAsync();

        Assert.IsType<ForbidResult>(result);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Null(reloaded.RackID);
    }

    [Fact]
    public async Task Place_OnPostAsync_CallerIsEditorOnAtLeastOneStudy_MovesBox()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg18@test.com");

        var study = new Study { StudyCode = "RWG-P-EDIT" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg18@test.com");
        await GrantAsync(context, caller.Id, study.StudyID, "Editor", sysAdmin.Id);

        var box = await AddBoxAsync(context, "RWG-P-BOX2", null);

        var freezer = new Freezer { FreezerName = "RWG-P-FREEZER" };
        context.Freezers.Add(freezer);
        await context.SaveChangesAsync();
        var compartment = new Compartment { CompartmentName = "RWG-P-COMPARTMENT", FreezerID = freezer.FreezerID };
        context.Compartments.Add(compartment);
        await context.SaveChangesAsync();
        var rack = new Rack { RackName = "RWG-P-RACK", CompartmentID = compartment.CompartmentID };
        context.Racks.Add(rack);
        await context.SaveChangesAsync();

        var model = BuildPlaceModel(services, caller.Id);
        model.BoxId = box.BoxID;
        model.NewRackId = rack.RackID;

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Equal(rack.RackID, reloaded.RackID);
    }

    [Fact]
    public async Task Place_OnGetAsync_CallerWithNoAccessToBoxStudy_DoesNotReturnBox()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg19@test.com");
        var study = new Study { StudyCode = "RWG-P-GET-OTHER" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        // Caller has zero UserStudyAccess rows at all.
        var caller = await CreateUserAsync(services, "noaccess-rwg19@test.com");

        var box = await AddBoxAsync(context, "RWG-P-GETBOX1", study.StudyID);

        var model = BuildPlaceModel(services, caller.Id);
        model.BoxLabel = box.BoxLabel;

        await model.OnGetAsync();

        Assert.Null(model.FoundBox);
    }

    [Fact]
    public async Task Place_OnGetAsync_CallerWithEditAccessToBoxStudy_ReturnsBox()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg20@test.com");
        var study = new Study { StudyCode = "RWG-P-GET-MINE" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg20@test.com");
        await GrantAsync(context, caller.Id, study.StudyID, "Editor", sysAdmin.Id);

        var box = await AddBoxAsync(context, "RWG-P-GETBOX2", study.StudyID);

        var model = BuildPlaceModel(services, caller.Id);
        model.BoxLabel = box.BoxLabel;

        await model.OnGetAsync();

        Assert.NotNull(model.FoundBox);
        Assert.Equal(box.BoxID, model.FoundBox!.BoxID);
    }

    [Fact]
    public async Task Place_OnPostAsync_CallerIsEditorOnDifferentStudyThanBox_ReturnsForbid_BoxNotMoved()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-rwg21@test.com");

        var ownStudy = new Study { StudyCode = "RWG-P-OWN" };
        var otherStudy = new Study { StudyCode = "RWG-P-OTHER" };
        context.Studies.AddRange(ownStudy, otherStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-rwg21@test.com");
        await GrantAsync(context, caller.Id, ownStudy.StudyID, "Editor", sysAdmin.Id);

        // Box belongs to a study the caller has no access to.
        var box = await AddBoxAsync(context, "RWG-P-BOX3", otherStudy.StudyID);

        var model = BuildPlaceModel(services, caller.Id);
        model.BoxId = box.BoxID;
        model.NewRackId = 999;

        var result = await model.OnPostAsync();

        Assert.IsType<ForbidResult>(result);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Null(reloaded.RackID);
    }
}
