using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
/// Verifies the Samples/Import page's (<see cref="ImportModel"/>) per-row access enforcement for
/// CSV import:
/// <list type="bullet">
/// <item>a caller with zero Editor-or-above study access gets <c>Forbid()</c> from the Preview
/// and Commit handlers (the <c>[Authorize(Roles = "Write,Admin")]</c> this page used to carry is
/// gone -- this per-row/per-caller check replaces it);</item>
/// <item>a file with rows spanning both an editable and a non-editable study imports the
/// editable rows and rejects the others individually, not the whole file;</item>
/// <item>a file entirely for non-editable studies produces zero <c>SuccessRows</c>, all
/// <c>ErrorRows</c>;</item>
/// <item>a commit batch where one row targets a box already holding a different study's
/// specimens is skipped (no specimen created, no audit log written for that row), while other
/// valid rows in the same batch still commit -- this is the box/study-mixing fix applied to the
/// CSV import write path (<c>IStorageService.ValidateAndAssignBoxStudyAsync</c>), extending the
/// same protection already enforced for Samples/Add.</item>
/// </list>
/// Page models are instantiated directly with services resolved from the real DI container and a
/// hand-built <see cref="ClaimsPrincipal"/> standing in for the authenticated caller, mirroring
/// <c>AddSampleAccessTests</c>/<c>SamplesVisibilityTests</c>. Requires STASIS_TEST_CONNECTION;
/// see <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class ImportAccessTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public ImportAccessTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static ImportModel BuildImportModel(IServiceProvider services, string callerUserId, IFormFile? csvFile = null)
    {
        var sampleService = services.GetRequiredService<ISampleService>();
        var auditService = services.GetRequiredService<IAuditService>();
        var storageService = services.GetRequiredService<IStorageService>();
        var studyAccessService = services.GetRequiredService<IStudyAccessService>();

        return new ImportModel(sampleService, auditService, storageService, studyAccessService)
        {
            CsvFile = csvFile,
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

    private static IFormFile BuildCsvFile(string csvContent)
    {
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "CsvFile", "import.csv");
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
    public async Task OnPostPreviewAsync_CallerWithNoStudyAccessAtAll_ReturnsForbid()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var caller = await CreateUserAsync(services, "noaccess-ia1@test.com");

        var csv = "BarcodeID,LegacyID,StudyCode,SampleType,CollectionDate,BoxLabel,PositionRow,PositionCol,ParticipantID,CellCount\n" +
                  "IABARCODE1,,IA-NONE,,,,,,,\n";
        var model = BuildImportModel(services, caller.Id, BuildCsvFile(csv));

        var result = await model.OnPostPreviewAsync();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task OnPostCommitAsync_CallerWithNoStudyAccessAtAll_ReturnsForbid()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var caller = await CreateUserAsync(services, "noaccess-ia2@test.com");

        var csv = "BarcodeID,LegacyID,StudyCode,SampleType,CollectionDate,BoxLabel,PositionRow,PositionCol,ParticipantID,CellCount\n" +
                  "IABARCODE2,,IA-NONE,,,,,,,\n";
        var model = BuildImportModel(services, caller.Id, BuildCsvFile(csv));

        var result = await model.OnPostCommitAsync();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task OnPostCommitAsync_MixOfEditableAndNonEditableStudyRows_ImportsEditableRows_RejectsOthersIndividually()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-ia3@test.com");

        var editableStudy = new Study { StudyCode = "IA-EDIT" };
        var nonEditableStudy = new Study { StudyCode = "IA-NOEDIT" };
        context.Studies.AddRange(editableStudy, nonEditableStudy);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-ia3@test.com");
        // Caller has Editor on editableStudy only -- no access at all to nonEditableStudy.
        await GrantAsync(context, caller.Id, editableStudy.StudyID, "Editor", sysAdmin.Id);

        var csv = "BarcodeID,LegacyID,StudyCode,SampleType,CollectionDate,BoxLabel,PositionRow,PositionCol,ParticipantID,CellCount\n" +
                  "IAOK1,,IA-EDIT,,,,,,,\n" +
                  "IABLOCKED1,,IA-NOEDIT,,,,,,,\n";
        var model = BuildImportModel(services, caller.Id, BuildCsvFile(csv));

        var result = await model.OnPostCommitAsync();

        Assert.IsType<RedirectToPageResult>(result);

        var savedOk = await context.Specimens.SingleOrDefaultAsync(s => s.BarcodeID == "IAOK1");
        Assert.NotNull(savedOk);
        Assert.Equal(editableStudy.StudyID, savedOk!.StudyID);

        var savedBlocked = await context.Specimens.SingleOrDefaultAsync(s => s.BarcodeID == "IABLOCKED1");
        Assert.Null(savedBlocked);
    }

    [Fact]
    public async Task OnPostPreviewAsync_FileEntirelyForNonEditableStudies_ProducesZeroSuccessRows_AllErrorRows()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-ia4@test.com");

        var editableStudy = new Study { StudyCode = "IA-EDIT2" };
        var nonEditableStudyA = new Study { StudyCode = "IA-NOEDIT-A" };
        var nonEditableStudyB = new Study { StudyCode = "IA-NOEDIT-B" };
        context.Studies.AddRange(editableStudy, nonEditableStudyA, nonEditableStudyB);
        await context.SaveChangesAsync();

        var caller = await CreateUserAsync(services, "editor-ia4@test.com");
        // Caller has Editor access to a study, but the CSV below never references it -- every
        // row targets a study the caller has zero access to.
        await GrantAsync(context, caller.Id, editableStudy.StudyID, "Editor", sysAdmin.Id);

        var csv = "BarcodeID,LegacyID,StudyCode,SampleType,CollectionDate,BoxLabel,PositionRow,PositionCol,ParticipantID,CellCount\n" +
                  "IABLOCKED2,,IA-NOEDIT-A,,,,,,,\n" +
                  "IABLOCKED3,,IA-NOEDIT-B,,,,,,,\n";
        var model = BuildImportModel(services, caller.Id, BuildCsvFile(csv));

        var result = await model.OnPostPreviewAsync();

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.PreviewResult);
        Assert.Equal(0, model.PreviewResult!.SuccessRows.Count);
        Assert.Equal(2, model.PreviewResult.ErrorRows.Count);
        Assert.All(model.PreviewResult.ErrorRows, row =>
            Assert.Contains("do not have edit access", row.Error));
    }

    [Fact]
    public async Task OnPostCommitAsync_RowTargetingBoxHeldByDifferentStudy_IsSkipped_OtherRowsStillCommit()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<StasisDbContext>();

        var sysAdmin = await CreateSystemAdminAsync(services, "sysadmin-ia5@test.com");

        var callerStudy = new Study { StudyCode = "IA-BOX-MINE" };
        var otherStudy = new Study { StudyCode = "IA-BOX-OTHER" };
        context.Studies.AddRange(callerStudy, otherStudy);
        await context.SaveChangesAsync();

        // Box already belongs to otherStudy and holds one of its specimens.
        var box = await AddBoxAsync(context, "IA-BOX-1", otherStudy.StudyID);
        await AddSpecimenAsync(context, "IA-EXISTING", otherStudy.StudyID, box.BoxID, row: 1, col: 1);

        var caller = await CreateUserAsync(services, "editor-ia5@test.com");
        await GrantAsync(context, caller.Id, callerStudy.StudyID, "Editor", sysAdmin.Id);

        var csv = "BarcodeID,LegacyID,StudyCode,SampleType,CollectionDate,BoxLabel,PositionRow,PositionCol,ParticipantID,CellCount\n" +
                  $"IABOXBLOCKED,,IA-BOX-MINE,,,{box.BoxLabel},1,2,,\n" +
                  "IABOXOK,,IA-BOX-MINE,,,,,,,\n";
        var model = BuildImportModel(services, caller.Id, BuildCsvFile(csv));

        var result = await model.OnPostCommitAsync();

        Assert.IsType<RedirectToPageResult>(result);

        // The row targeting the mismatched box was skipped entirely.
        var blockedSpecimen = await context.Specimens.SingleOrDefaultAsync(s => s.BarcodeID == "IABOXBLOCKED");
        Assert.Null(blockedSpecimen);
        var blockedAudit = await context.AuditLogs.AnyAsync(a => a.NewValue == "IABOXBLOCKED");
        Assert.False(blockedAudit);

        // The other valid row in the same batch still committed.
        var okSpecimen = await context.Specimens.SingleOrDefaultAsync(s => s.BarcodeID == "IABOXOK");
        Assert.NotNull(okSpecimen);
        var okAudit = await context.AuditLogs.AnyAsync(a => a.NewValue == "IABOXOK");
        Assert.True(okAudit);

        // The box's pre-existing specimen and study affiliation are untouched.
        var reloadedBox = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Equal(otherStudy.StudyID, reloadedBox.StudyID);
        var specimenCountInBox = await context.Specimens.CountAsync(s => s.BoxID == box.BoxID);
        Assert.Equal(1, specimenCountInBox);
    }
}
