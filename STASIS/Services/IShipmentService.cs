using STASIS.Models;

namespace STASIS.Services;

public interface IShipmentService
{
    // Batch import
    Task<ShipmentBatch> ImportBatchFromCsvAsync(Stream csvStream, string userId, string? requestorName, string? requestorEmail);

    // Queries
    Task<List<ShipmentBatch>> GetAllBatchesAsync();
    Task<ShipmentBatch?> GetBatchByIdAsync(int batchId);
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
