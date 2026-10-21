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
}
