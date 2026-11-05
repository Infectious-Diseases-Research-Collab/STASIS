using Microsoft.EntityFrameworkCore;
using STASIS.Models;
using STASIS.Services;
using STASIS.Tests.TestHelpers;
using Xunit;

namespace STASIS.Tests.Services;

/// <summary>
/// Verifies the per-row study-access enforcement in
/// <see cref="IShipmentService.ImportBatchFromCsvAsync"/>: a CSV row that resolves to a real
/// specimen in a study the caller cannot edit is skipped as "No Access" rather than exposing the
/// specimen's real status (Shipped/Discarded/Not Yet Received/Pending), mirroring
/// <see cref="ISampleService.ImportSpecimensFromCsv"/>'s existing <c>editableStudyIds</c>
/// parameter for new-specimen creation.
///
/// Uses an in-memory <see cref="STASIS.Data.StasisDbContext"/> via <see cref="TestDbContextFactory"/>,
/// mirroring <c>BoxStudyEnforcementTests</c> -- no STASIS_TEST_CONNECTION needed.
/// </summary>
public class ShipmentServiceStudyEnforcementTests
{
    [Fact]
    public async Task ImportBatchFromCsvAsync_BarcodeInNonEditableStudy_SkippedAsNoAccess_NotMatched()
    {
        var context = TestDbContextFactory.Create();
        var service = new ShipmentService(context, new AuditService(context));

        var editableStudy = new Study { StudyCode = "SHIP-EDITABLE" };
        var otherStudy = new Study { StudyCode = "SHIP-OTHER" };
        context.Studies.AddRange(editableStudy, otherStudy);
        await context.SaveChangesAsync();

        context.Specimens.Add(new Specimen { BarcodeID = "SHIP-S1", StudyID = otherStudy.StudyID, Status = "In-Stock" });
        await context.SaveChangesAsync();

        var csv = "Barcode\nSHIP-S1";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var batch = await service.ImportBatchFromCsvAsync(stream, "test-user", null, null,
            new[] { editableStudy.StudyID });

        Assert.Equal(1, batch.TotalNoAccess);
        Assert.Equal(0, batch.TotalAvailable);

        var request = await context.ShipmentRequests.AsNoTracking()
            .SingleAsync(r => r.BatchID == batch.BatchID && r.RequestedBarcode == "SHIP-S1");
        Assert.Equal("No Access", request.Status);
        Assert.Null(request.MatchedSpecimenID);
    }

    [Fact]
    public async Task ImportBatchFromCsvAsync_EditableStudyIdsNull_AllowsEveryStudy()
    {
        var context = TestDbContextFactory.Create();
        var service = new ShipmentService(context, new AuditService(context));

        var study = new Study { StudyCode = "SHIP-ANY" };
        context.Studies.Add(study);
        await context.SaveChangesAsync();
        context.Specimens.Add(new Specimen { BarcodeID = "SHIP-S2", StudyID = study.StudyID, Status = "In-Stock" });
        await context.SaveChangesAsync();

        var csv = "Barcode\nSHIP-S2";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var batch = await service.ImportBatchFromCsvAsync(stream, "test-user", null, null, null);

        Assert.Equal(1, batch.TotalAvailable);
        Assert.Equal(0, batch.TotalNoAccess);
    }
}
