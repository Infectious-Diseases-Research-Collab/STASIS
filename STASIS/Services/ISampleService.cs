using STASIS.Models;

namespace STASIS.Services;

public interface ISampleService
{
    Task<(List<Specimen> Specimens, int TotalCount)> GetSpecimensAsync(string? searchString, int? studyId, int? sampleTypeId, string? participantId, int pageIndex, int pageSize);
    Task<List<Study>> GetAllStudies();
    Task<List<SampleType>> GetAllSampleTypes();
    Task<Specimen?> GetSpecimenByBarcode(string barcode);
    Task<bool> IsBarcodeTaken(string barcode);
    Task<List<(int Row, int Col)>> GetOccupiedPositions(int boxId);
    Task AddSpecimen(Specimen specimen);
    Task<List<VisitType>> GetAllVisitTypes();
    Task<(int Row, int? Col)?> GetNextAvailablePosition(int boxId, IEnumerable<(int Row, int? Col)>? claimedPositions = null);
    Task AddSpecimensBatch(IEnumerable<Specimen> specimens, string userId);
    Task AddVisitType(string name, string userId);
    Task UpdateVisitType(int id, string name, string userId);
    Task DeleteVisitType(int id, string userId);
    Task UpdateSpecimen(Specimen specimen);
    Task DeleteSpecimen(int id);
    Task<ImportResult> ImportSpecimensFromCsv(Stream csvStream);

    // Discard workflow
    Task<Approval> RequestDiscardAsync(List<int> specimenIds, string userId);
    Task<List<Approval>> GetPendingDiscardApprovalsAsync();
    Task<Approval?> GetDiscardApprovalByIdAsync(int approvalId);
    Task ApproveDiscardAsync(int approvalId, string approverUserId, string level, string status, string? comments);
    Task ExecuteDiscardAsync(int approvalId, string userId);
    Task<Specimen?> GetSpecimenByIdAsync(int specimenId);

    // Specimen detail with full navigation
    Task<Specimen?> GetSpecimenDetailAsync(int specimenId);

    // Filter paper usage history
    Task<List<FilterPaperUsage>> GetFilterPaperUsageAsync(int specimenId);

    // Batch barcode uniqueness check
    Task<List<string>> GetTakenBarcodesAsync(IEnumerable<string> barcodes);
}

public class ImportResult
{
    public List<ImportRow> SuccessRows { get; set; } = new();
    public List<ImportRow> ErrorRows { get; set; } = new();
    public int TotalRows { get; set; }
}

public class ImportRow
{
    public int LineNumber { get; set; }
    public string BarcodeID { get; set; } = string.Empty;
    public string? LegacyID { get; set; }
    public string? StudyCode { get; set; }
    public string? SampleType { get; set; }
    public string? CollectionDate { get; set; }
    public string? BoxLabel { get; set; }
    public string? PositionRow { get; set; }
    public string? PositionCol { get; set; }
    public string? ParticipantID { get; set; }
    public string? CellCount { get; set; }
    public string? Error { get; set; }
    public Specimen? Specimen { get; set; }
}
