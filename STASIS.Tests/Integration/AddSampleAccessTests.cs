using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STASIS.Data;
using STASIS.Models;
using STASIS.Pages.Samples;
using STASIS.Services;
using Xunit;

namespace STASIS.Tests.Integration;

/// <summary>
/// Verifies the Samples/Add page's (<see cref="AddModel"/>) per-study Editor access enforcement
/// and its box/study-mixing protection:
/// <list type="bullet">
/// <item>the Study dropdown only lists studies the caller can <see cref="IStudyAccessService.CanEditAsync"/>
/// (not merely View);</item>
/// <item>a caller with zero study assignments gets <c>Forbid()</c> from <c>OnGetAsync</c>;</item>
/// <item>posting a <c>StudyID</c> the caller cannot edit is rejected server-side even though the
/// dropdown would never have offered it (an IDOR guard -- don't trust the posted value);</item>
/// <item>a batch targeting a box already holding a different study's specimens fails with a
/// <c>ModelState</c> error, and the existing transaction in
/// <see cref="ISampleService.AddSpecimensBatch"/> rolls back so no specimen from the batch is
/// saved.</item>
/// </list>
/// Page models are instantiated directly with services resolved from the real DI container and a
/// hand-built <see cref="ClaimsPrincipal"/> standing in for the authenticated caller, mirroring
/// <c>EditUserAccessTests</c>/<c>SamplesVisibilityTests</c>. Requires STASIS_TEST_CONNECTION; see
/// <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class AddSampleAccessTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public AddSampleAccessTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

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

    private static async Task<SampleType> AddSampleTypeAsync(StasisDbContext context, string typeName)
    {
        var sampleType = new SampleType { TypeName = typeName };
        context.SampleTypes.Add(sampleType);
        await context.SaveChangesAsync();
        return sampleType;
    }

    private static async Task<Box> AddBoxAsync(StasisDbContext context, string label, int? studyId)
    {
        var box = new Box { BoxLabel = label, BoxType = "81-slot", BoxCategory = "Standard", StudyID = studyId };
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
    public async Task OnGetAsync_StudyDropdown_OnlyListsStudiesCallerCanEdit()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-asa1@test.com");

        var editableStudy = new Study { StudyCode = "ASA-EDIT" };
        var viewOnlyStudy = new Study { StudyCode = "ASA-VIEW" };
        context.Studies.AddRange(editableStudy, viewOnlyStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-asa1@test.com");
        await GrantAsync(context, caller.Id, editableStudy.StudyID, "Editor", sysAdmin.Id);
        await GrantAsync(context, caller.Id, viewOnlyStudy.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildAddModel(services, caller.Id);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        var studyIdsInDropdown = model.StudyOptions.Cast<SelectListItem>()
            .Select(i => int.Parse(i.Value!))
            .ToList();
        Assert.Contains(editableStudy.StudyID, studyIdsInDropdown);
        Assert.DoesNotContain(viewOnlyStudy.StudyID, studyIdsInDropdown);
    }

    [Fact]
    public async Task OnGetAsync_CallerWithNoStudyAccessAtAll_ReturnsForbid()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var caller = await CreateUserAsync(services, "noaccess-asa1@test.com");

        var model = BuildAddModel(services, caller.Id);

        var result = await model.OnGetAsync();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_SystemAdmin_StudyDropdownListsAllStudies()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-asa2@test.com");

        var studyA = new Study { StudyCode = "ASA-SA1" };
        var studyB = new Study { StudyCode = "ASA-SA2" };
        context.Studies.AddRange(studyA, studyB);
        await context.SaveChangesAsync();

        var model = BuildAddModel(services, sysAdmin.Id);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        var studyIdsInDropdown = model.StudyOptions.Cast<SelectListItem>()
            .Select(i => int.Parse(i.Value!))
            .ToList();
        Assert.Contains(studyA.StudyID, studyIdsInDropdown);
        Assert.Contains(studyB.StudyID, studyIdsInDropdown);
    }

    [Fact]
    public async Task OnPostAsync_PostingStudyCallerCannotEdit_IsRejected_AndNoSpecimenSaved()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-asa3@test.com");

        var editableStudy = new Study { StudyCode = "ASA-POST-EDIT" };
        var otherStudy = new Study { StudyCode = "ASA-POST-OTHER" };
        context.Studies.AddRange(editableStudy, otherStudy);
        await context.SaveChangesAsync();

        var sampleType = await AddSampleTypeAsync(context, "ASA-Type-1");

        var caller = await CreateUserAsync(services, "editor-asa3@test.com");
        // Caller has Editor on editableStudy only -- no access at all to otherStudy.
        await GrantAsync(context, caller.Id, editableStudy.StudyID, "Editor", sysAdmin.Id);

        var model = BuildAddModel(services, caller.Id);
        model.ParticipantID = "ASA-P1";
        // Posting otherStudy directly -- the dropdown would never have offered it.
        model.StudyID = otherStudy.StudyID;
        model.Samples = new List<AddModel.SampleInput>
        {
            new AddModel.SampleInput { BarcodeID = "ASABARCODE1", SampleTypeID = sampleType.SampleTypeID }
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);

        var saved = await context.Specimens.AnyAsync(s => s.BarcodeID == "ASABARCODE1");
        Assert.False(saved);
    }

    [Fact]
    public async Task OnPostAsync_BatchTargetingBoxHeldByDifferentStudy_FailsWithModelStateError_AndNoSpecimenSaved()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-asa4@test.com");

        var callerStudy = new Study { StudyCode = "ASA-BOX-MINE" };
        var otherStudy = new Study { StudyCode = "ASA-BOX-OTHER" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        var sampleType = await AddSampleTypeAsync(context, "ASA-Type-2");

        // Box already belongs to otherStudy and holds one of its specimens.
        var box = await AddBoxAsync(context, "ASA-BOX-1", otherStudy.StudyID);
        await AddSpecimenAsync(context, "ASA-EXISTING", otherStudy.StudyID, box.BoxID, row: 1, col: 1);

        var caller = await CreateUserAsync(services, "editor-asa4@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Editor", sysAdmin.Id);

        var model = BuildAddModel(services, caller.Id);
        model.ParticipantID = "ASA-P2";
        model.StudyID = callerStudy.StudyID;
        model.Samples = new List<AddModel.SampleInput>
        {
            new AddModel.SampleInput
            {
                BarcodeID = "ASABARCODE2",
                SampleTypeID = sampleType.SampleTypeID,
                BoxID = box.BoxID,
                PositionRow = 1,
                PositionCol = 2
            }
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);

        // No specimen from the rejected batch was saved -- the existing transaction in
        // AddSpecimensBatch rolled back.
        var newSpecimenSaved = await context.Specimens.AnyAsync(s => s.BarcodeID == "ASABARCODE2");
        Assert.False(newSpecimenSaved);

        // The box's pre-existing specimen and study affiliation are untouched.
        var reloadedBox = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Equal(otherStudy.StudyID, reloadedBox.StudyID);
        var specimenCountInBox = await context.Specimens.CountAsync(s => s.BoxID == box.BoxID);
        Assert.Equal(1, specimenCountInBox);
    }

    [Fact]
    public async Task OnPostAsync_EditorPostingOwnEditableStudy_SavesSpecimen()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-asa5@test.com");

        var study = new Study { StudyCode = "ASA-OK" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var sampleType = await AddSampleTypeAsync(context, "ASA-Type-3");

        var caller = await CreateUserAsync(services, "editor-asa5@test.com");
        await GrantAsync(context, caller.Id, study.StudyID, "Editor", sysAdmin.Id);

        var model = BuildAddModel(services, caller.Id);
        model.ParticipantID = "ASA-P3";
        model.StudyID = study.StudyID;
        model.Samples = new List<AddModel.SampleInput>
        {
            new AddModel.SampleInput { BarcodeID = "ASABARCODE3", SampleTypeID = sampleType.SampleTypeID }
        };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var saved = await context.Specimens.SingleOrDefaultAsync(s => s.BarcodeID == "ASABARCODE3");
        Assert.NotNull(saved);
        Assert.Equal(study.StudyID, saved!.StudyID);
    }

    // ---------------------------------------------------------------------
    // Whole-branch review follow-up: OnGetCheckBarcodeAsync, OnGetNextPositionAsync, and
    // OnGetOccupiedPositionsAsync carried zero access check at all -- reachable by any
    // authenticated user via URL regardless of study access. Each must now reject a caller who
    // only holds Viewer (not Editor) access, returning the same safe/denied shape as their
    // existing "invalid input" branches rather than leaking real data.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task OnGetCheckBarcodeAsync_ViewerOnlyCaller_ReturnsSafeFalseResponse_NotRealBarcodeStatus()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-asa6@test.com");

        var study = new Study { StudyCode = "ASA-CB-STUDY" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        // The barcode genuinely IS taken in this study.
        await AddSpecimenAsync(context, "ASA-CB-TAKEN", study.StudyID, null, null, null);

        var viewer = await CreateUserAsync(services, "viewer-asa6@test.com");
        await GrantAsync(context, viewer.Id, study.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildAddModel(services, viewer.Id);

        var result = await model.OnGetCheckBarcodeAsync("ASA-CB-TAKEN", study.StudyID);

        var json = Assert.IsType<JsonResult>(result);
        var taken = (bool)json.Value!.GetType().GetProperty("taken")!.GetValue(json.Value)!;
        Assert.False(taken);
    }

    [Fact]
    public async Task OnGetNextPositionAsync_ViewerOnlyCallerOnStudyAssignedBox_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-asa7@test.com");

        var study = new Study { StudyCode = "ASA-NP-STUDY" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var box = await AddBoxAsync(context, "ASA-NP-BOX", study.StudyID);

        var viewer = await CreateUserAsync(services, "viewer-asa7@test.com");
        await GrantAsync(context, viewer.Id, study.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildAddModel(services, viewer.Id);

        var result = await model.OnGetNextPositionAsync(box.BoxID);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task OnGetOccupiedPositionsAsync_ViewerOnlyCallerOnStudyAssignedBox_ReturnsEmptyArray_NotRealPositions()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-asa8@test.com");

        var study = new Study { StudyCode = "ASA-OP-STUDY" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();

        var box = await AddBoxAsync(context, "ASA-OP-BOX", study.StudyID);
        await AddSpecimenAsync(context, "ASA-OP-SPEC", study.StudyID, box.BoxID, row: 1, col: 1);

        var viewer = await CreateUserAsync(services, "viewer-asa8@test.com");
        await GrantAsync(context, viewer.Id, study.StudyID, "Viewer", sysAdmin.Id);

        var model = BuildAddModel(services, viewer.Id);

        var result = await model.OnGetOccupiedPositionsAsync(box.BoxID);

        var json = Assert.IsType<JsonResult>(result);
        var positions = ((System.Collections.IEnumerable)json.Value!).Cast<object>();
        Assert.Empty(positions);
    }
}
