using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;
using STASIS.Services;
using STASIS.Tests.TestHelpers;
using Xunit;

namespace STASIS.Tests.Services;

/// <summary>
/// Verifies the whole-branch-review fix to <c>StorageService.ApplyStudyVisibility</c>'s
/// "StudyID == null is visible to anyone" branch: a box with a null StudyID is only treated as
/// a genuinely-unclaimed, available-to-anyone box if it also has zero specimens. A box whose
/// StudyID was never backfilled (legacy data from before the column existed) but which already
/// holds specimens must NOT be treated the same way -- it must be invisible to a non-System-Admin
/// caller, exactly like a box assigned to a study the caller cannot see, until it is actually
/// backfilled or re-touched by a write path.
///
/// Uses an in-memory <see cref="StasisDbContext"/> via <see cref="TestDbContextFactory"/> and
/// constructs <see cref="StorageService"/> directly (no live HTTP round-trip, no
/// IStudyAccessService needed since StorageService takes a pre-resolved visibleStudyIds
/// collection), mirroring <c>BoxStudyEnforcementTests</c> -- the established pattern for this
/// test directory.
/// </summary>
public class StorageServiceStudyVisibilityTests
{
    private static StorageService CreateService(StasisDbContext context) =>
        new StorageService(context, new AuditService(context));

    private static async Task<Study> AddStudyAsync(StasisDbContext context, string code)
    {
        var study = new Study { StudyCode = code };
        context.Studies.Add(study);
        await context.SaveChangesAsync();
        return study;
    }

    private static async Task<Box> AddBoxAsync(StasisDbContext context, string label, int? studyId)
    {
        var box = new Box { BoxLabel = label, BoxType = "81-slot", BoxCategory = "Standard", StudyID = studyId };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();
        return box;
    }

    private static async Task<Specimen> AddSpecimenAsync(StasisDbContext context, string barcode, int studyId, int? boxId)
    {
        var specimen = new Specimen
        {
            BarcodeID = barcode,
            StudyID = studyId,
            BoxID = boxId,
            PositionRow = 1,
            PositionCol = 1,
            Status = "In-Stock"
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync();
        return specimen;
    }

    [Fact]
    public async Task GetAllBoxesAsync_UnbackfilledLegacyBoxWithSpecimens_IsExcludedForNonAdminWithStudyAccess()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "SV-A1");

        // Simulates legacy pre-existing data: Box.StudyID was added after specimens already
        // existed, and this box was never backfilled and never touched by a write path, so its
        // StudyID is still null even though it already holds a specimen.
        var legacyBox = await AddBoxAsync(context, "SV-BOX-1", studyId: null);
        await AddSpecimenAsync(context, "SV-SPEC-1", studyA.StudyID, legacyBox.BoxID);

        var service = CreateService(context);

        var boxes = await service.GetAllBoxesAsync(new List<int> { studyA.StudyID });

        Assert.DoesNotContain(boxes, b => b.BoxID == legacyBox.BoxID);
    }

    [Fact]
    public async Task GetBoxWithSpecimens_UnbackfilledLegacyBoxWithSpecimens_ReturnsNullForNonAdminWithStudyAccess()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "SV-A2");

        var legacyBox = await AddBoxAsync(context, "SV-BOX-2", studyId: null);
        await AddSpecimenAsync(context, "SV-SPEC-2", studyA.StudyID, legacyBox.BoxID);

        var service = CreateService(context);

        var result = await service.GetBoxWithSpecimens(legacyBox.BoxID, new List<int> { studyA.StudyID });

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllBoxesAsync_GenuinelyEmptyUnclaimedBox_IsVisibleToNonAdminWithStudyAccess()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "SV-B1");

        var emptyBox = await AddBoxAsync(context, "SV-BOX-3", studyId: null);

        var service = CreateService(context);

        var boxes = await service.GetAllBoxesAsync(new List<int> { studyA.StudyID });

        Assert.Contains(boxes, b => b.BoxID == emptyBox.BoxID);
    }

    [Fact]
    public async Task GetBoxWithSpecimens_GenuinelyEmptyUnclaimedBox_IsVisibleToNonAdminWithStudyAccess()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "SV-B2");

        var emptyBox = await AddBoxAsync(context, "SV-BOX-4", studyId: null);

        var service = CreateService(context);

        var result = await service.GetBoxWithSpecimens(emptyBox.BoxID, new List<int> { studyA.StudyID });

        Assert.NotNull(result);
        Assert.Equal(emptyBox.BoxID, result!.BoxID);
    }

    [Fact]
    public async Task GetAllBoxesAsync_SystemAdmin_StillSeesUnbackfilledLegacyBoxWithSpecimens()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "SV-C1");

        var legacyBox = await AddBoxAsync(context, "SV-BOX-5", studyId: null);
        await AddSpecimenAsync(context, "SV-SPEC-5", studyA.StudyID, legacyBox.BoxID);

        var service = CreateService(context);

        // visibleStudyIds == null means unrestricted (System Admin) -- the whole point of
        // hardening ApplyStudyVisibility is to make the legacy box invisible to everyone else,
        // not to make it disappear entirely.
        var boxes = await service.GetAllBoxesAsync(null);

        Assert.Contains(boxes, b => b.BoxID == legacyBox.BoxID);
    }
}
