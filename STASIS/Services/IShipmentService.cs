using STASIS.Models;

namespace STASIS.Services;

public interface IShipmentService
{
    // Batch import
    Task<ShipmentBatch> ImportBatchFromCsvAsync(Stream csvStream, string userId, string? requestorName, string? requestorEmail, IReadOnlyCollection<int>? editableStudyIds);

    // Queries
    // visibleStudyIds == null means unrestricted (System Admin). Otherwise returns only batches
    // whose matched specimens are all in visibleStudyIds; a batch with no matched specimens is
    // returned only if importedByUserId imported it.
    Task<List<ShipmentBatch>> GetAllBatchesAsync(IReadOnlyCollection<int>? visibleStudyIds = null, string? importedByUserId = null);
    Task<ShipmentBatch?> GetBatchByIdAsync(int batchId);

    // Distinct StudyIDs of the batch's matched specimens -- the studies a caller needs access to
    // in order to view, approve or ship it.
    Task<List<int>> GetBatchStudyIdsAsync(int batchId);
    Task<List<Shipment>> GetShipmentsForBatchAsync(int batchId);
    Task<Shipment?> GetShipmentByIdAsync(int shipmentId);

    // Approval
    Task ApproveBatchAsync(int batchId, string approverUserId, string level, string status, string? comments);

    // Validation
    Task<List<string>> ValidateShipmentAsync(int batchId, bool isInternational, int filterPaperSpotsPerSpecimen);

    // Ship
    Task<Shipment> ShipBatchAsync(int batchId, string courier, string? trackingNumber, string? destination,
        string userId, bool isInternational = false, int filterPaperSpotsPerSpecimen = 0);
}
