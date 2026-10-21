using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;
using STASIS.Services;
using STASIS.Tests.TestHelpers;
using Xunit;

namespace STASIS.Tests.Services;

/// <summary>
/// Verifies the write-side enforcement that a single physical box never holds specimens from
/// more than one study: <see cref="IStorageService.ValidateAndAssignBoxStudyAsync"/> and its
/// integration into <see cref="IStorageService.MoveSpecimenAsync"/>,
/// <see cref="IStorageService.ReboxSpecimensAsync"/>, and
/// <see cref="IStorageService.CheckAndUnassignEmptyBoxAsync"/>.
///
/// Uses an in-memory <see cref="StasisDbContext"/> via <see cref="TestDbContextFactory"/> and
/// constructs <see cref="StorageService"/> directly (no live HTTP round-trip), mirroring
/// <c>StudyAccessServiceTests</c> -- the established pattern for this test directory.
/// </summary>
public class BoxStudyEnforcementTests
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

    private static async Task<Box> AddBoxAsync(StasisDbContext context, string label, int? studyId, string category = "Standard")
    {
        var box = new Box { BoxLabel = label, BoxType = "81-slot", BoxCategory = category, StudyID = studyId };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();
        return box;
    }

    private static async Task<Specimen> AddSpecimenAsync(StasisDbContext context, string barcode, int studyId, int? boxId, int? row = null, int? col = null)
    {
        var specimen = new Specimen
        {
            BarcodeID = barcode,
            StudyID = studyId,
            BoxID = boxId,
            PositionRow = row,
            PositionCol = col
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync();
        return specimen;
    }

    // --- ValidateAndAssignBoxStudyAsync ---

    [Fact]
    public async Task ValidateAndAssignBoxStudyAsync_OnEmptyUnassignedBox_SetsStudyID()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "VB-A");
        var box = await AddBoxAsync(context, "VB-BOX-1", studyId: null);

        var service = CreateService(context);

        await service.ValidateAndAssignBoxStudyAsync(box.BoxID, studyA.StudyID);
        await context.SaveChangesAsync();

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Equal(studyA.StudyID, reloaded.StudyID);
    }

    [Fact]
    public async Task ValidateAndAssignBoxStudyAsync_OnBoxHoldingDifferentStudy_Throws()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "VB-B1");
        var studyB = await AddStudyAsync(context, "VB-B2");
        var box = await AddBoxAsync(context, "VB-BOX-2", studyId: studyA.StudyID);

        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ValidateAndAssignBoxStudyAsync(box.BoxID, studyB.StudyID));

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Equal(studyA.StudyID, reloaded.StudyID);
    }

    [Fact]
    public async Task ValidateAndAssignBoxStudyAsync_OnBoxAlreadyMatchingStudy_IsNoOpAndDoesNotThrow()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "VB-C");
        var box = await AddBoxAsync(context, "VB-BOX-3", studyId: studyA.StudyID);

        var service = CreateService(context);

        await service.ValidateAndAssignBoxStudyAsync(box.BoxID, studyA.StudyID);
        await context.SaveChangesAsync();

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Equal(studyA.StudyID, reloaded.StudyID);
    }

    // --- MoveSpecimenAsync ---

    [Fact]
    public async Task MoveSpecimenAsync_IntoBoxHoldingDifferentStudy_ThrowsAndDoesNotMoveSpecimen()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "MS-A");
        var studyB = await AddStudyAsync(context, "MS-B");

        var sourceBox = await AddBoxAsync(context, "MS-SRC", studyId: studyB.StudyID);
        var destBox = await AddBoxAsync(context, "MS-DEST", studyId: studyA.StudyID);

        // destBox already holds a Study A specimen.
        await AddSpecimenAsync(context, "MS-EXISTING", studyA.StudyID, destBox.BoxID, row: 1, col: 1);

        var specimenB = await AddSpecimenAsync(context, "MS-MOVING", studyB.StudyID, sourceBox.BoxID, row: 1, col: 1);

        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.MoveSpecimenAsync(specimenB.SpecimenID, destBox.BoxID, row: 2, col: 2, userId: "tester"));

        var reloaded = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimenB.SpecimenID);
        Assert.Equal(sourceBox.BoxID, reloaded.BoxID);
        Assert.Equal(1, reloaded.PositionRow);
        Assert.Equal(1, reloaded.PositionCol);
    }

    [Fact]
    public async Task MoveSpecimenAsync_IntoUnassignedEmptyBox_MovesSpecimenAndClaimsBoxForItsStudy()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "MS-C");

        var sourceBox = await AddBoxAsync(context, "MS-SRC2", studyId: studyA.StudyID);
        var destBox = await AddBoxAsync(context, "MS-DEST2", studyId: null);

        var specimen = await AddSpecimenAsync(context, "MS-MOVING2", studyA.StudyID, sourceBox.BoxID, row: 1, col: 1);

        var service = CreateService(context);

        await service.MoveSpecimenAsync(specimen.SpecimenID, destBox.BoxID, row: 3, col: 3, userId: "tester");

        var reloadedSpecimen = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen.SpecimenID);
        Assert.Equal(destBox.BoxID, reloadedSpecimen.BoxID);
        Assert.Equal(3, reloadedSpecimen.PositionRow);
        Assert.Equal(3, reloadedSpecimen.PositionCol);

        var reloadedBox = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == destBox.BoxID);
        Assert.Equal(studyA.StudyID, reloadedBox.StudyID);
    }

    // --- ReboxSpecimensAsync ---

    [Fact]
    public async Task ReboxSpecimensAsync_BatchContainingMismatchedStudy_ThrowsBeforeAnyRowWritten()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "RB-A");
        var studyB = await AddStudyAsync(context, "RB-B");

        var sourceBox = await AddBoxAsync(context, "RB-SRC", studyId: studyA.StudyID);
        var destBox = await AddBoxAsync(context, "RB-DEST", studyId: studyB.StudyID);

        var specimen1 = await AddSpecimenAsync(context, "RB-S1", studyB.StudyID, sourceBox.BoxID, row: 1, col: 1);
        // specimen2 belongs to Study A -- mismatched against destBox's Study B.
        var specimen2 = await AddSpecimenAsync(context, "RB-S2", studyA.StudyID, sourceBox.BoxID, row: 1, col: 2);

        var service = CreateService(context);

        var placements = new List<(int SpecimenId, int Row, int Col)>
        {
            (specimen1.SpecimenID, 5, 5),
            (specimen2.SpecimenID, 5, 6)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReboxSpecimensAsync(placements, destBox.BoxID, userId: "tester"));

        // Neither specimen's box/position should have changed -- the whole batch aborts.
        var reloaded1 = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen1.SpecimenID);
        var reloaded2 = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen2.SpecimenID);
        Assert.Equal(sourceBox.BoxID, reloaded1.BoxID);
        Assert.Equal(1, reloaded1.PositionRow);
        Assert.Equal(1, reloaded1.PositionCol);
        Assert.Equal(sourceBox.BoxID, reloaded2.BoxID);
        Assert.Equal(1, reloaded2.PositionRow);
        Assert.Equal(2, reloaded2.PositionCol);
    }

    [Fact]
    public async Task ReboxSpecimensAsync_BatchAllSameStudy_IntoUnassignedBox_WritesAllAndClaimsBox()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "RB-C");

        var sourceBox = await AddBoxAsync(context, "RB-SRC2", studyId: studyA.StudyID);
        var destBox = await AddBoxAsync(context, "RB-DEST2", studyId: null);

        var specimen1 = await AddSpecimenAsync(context, "RB-S3", studyA.StudyID, sourceBox.BoxID, row: 1, col: 1);
        var specimen2 = await AddSpecimenAsync(context, "RB-S4", studyA.StudyID, sourceBox.BoxID, row: 1, col: 2);

        var service = CreateService(context);

        var placements = new List<(int SpecimenId, int Row, int Col)>
        {
            (specimen1.SpecimenID, 5, 5),
            (specimen2.SpecimenID, 5, 6)
        };

        await service.ReboxSpecimensAsync(placements, destBox.BoxID, userId: "tester");

        var reloaded1 = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen1.SpecimenID);
        var reloaded2 = await context.Specimens.AsNoTracking().SingleAsync(s => s.SpecimenID == specimen2.SpecimenID);
        Assert.Equal(destBox.BoxID, reloaded1.BoxID);
        Assert.Equal(destBox.BoxID, reloaded2.BoxID);

        var reloadedBox = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == destBox.BoxID);
        Assert.Equal(studyA.StudyID, reloadedBox.StudyID);
    }

    // --- CheckAndUnassignEmptyBoxAsync ---

    [Fact]
    public async Task CheckAndUnassignEmptyBoxAsync_StandardBoxLastSpecimenLeaves_ClearsRackIdAndStudyId()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "CU-A");

        var box = new Box { BoxLabel = "CU-BOX-1", BoxType = "81-slot", BoxCategory = "Standard", StudyID = studyA.StudyID, RackID = 999 };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await service.CheckAndUnassignEmptyBoxAsync(box.BoxID);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Null(reloaded.RackID);
        Assert.Null(reloaded.StudyID);
    }

    [Theory]
    [InlineData("Temp")]
    [InlineData("Trash")]
    [InlineData("Shipping")]
    public async Task CheckAndUnassignEmptyBoxAsync_NonStandardBoxLastSpecimenLeaves_ClearsStudyIdRegardlessOfCategory(string category)
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, $"CU-{category}");

        var box = new Box { BoxLabel = $"CU-BOX-{category}", BoxType = "81-slot", BoxCategory = category, StudyID = studyA.StudyID };
        context.Boxes.Add(box);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await service.CheckAndUnassignEmptyBoxAsync(box.BoxID);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Null(reloaded.StudyID);
    }

    [Fact]
    public async Task CheckAndUnassignEmptyBoxAsync_BoxStillHoldingSpecimens_DoesNotClearStudyId()
    {
        var context = TestDbContextFactory.Create();
        var studyA = await AddStudyAsync(context, "CU-B");

        var box = await AddBoxAsync(context, "CU-BOX-2", studyId: studyA.StudyID);
        await AddSpecimenAsync(context, "CU-S1", studyA.StudyID, box.BoxID, row: 1, col: 1);

        var service = CreateService(context);

        await service.CheckAndUnassignEmptyBoxAsync(box.BoxID);

        var reloaded = await context.Boxes.AsNoTracking().SingleAsync(b => b.BoxID == box.BoxID);
        Assert.Equal(studyA.StudyID, reloaded.StudyID);
    }
}
