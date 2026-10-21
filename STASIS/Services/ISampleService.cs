using STASIS.Models;

namespace STASIS.Services;

public interface ISampleService
{
    // visibleStudyIds: null = unrestricted (System Admin), otherwise the caller's exact set of
    // assigned study ids (per IStudyAccessService.GetVisibleStudyIdsAsync) -- results are
    // filtered to that set. Pass once-resolved sets here rather than calling CanViewAsync per
    // row, to avoid an N+1 DB+Identity round-trip per specimen/study.
    Task<(List<Specimen> Specimens, int TotalCount)> GetSpecimensAsync(string? searchString, int? studyId, int? sampleTypeId, string? participantId, int pageIndex, int pageSize, IReadOnlyCollection<int>? visibleStudyIds);
    Task<List<Study>> GetAllStudies(IReadOnlyCollection<int>? visibleStudyIds);
    Task<List<SampleType>> GetAllSampleTypes();
    Task<List<Specimen>> GetSpecimensByBarcode(string barcode);
    Task<bool> IsBarcodeTaken(string barcode, int studyId);
    Task<List<(int Row, int Col)>> GetOccupiedPositions(int boxId);
    Task AddSpecimen(Specimen specimen);
    Task<List<VisitType>> GetAllVisitTypes();
    Task<(int Row, int? Col)?> GetNextAvailablePosition(int boxId, IEnumerable<(int Row, int? Col)>? claimedPositions = null);
    // For each specimen with a BoxID, validates (and, if the box is unclaimed, assigns) the
    // box's study affiliation via IStorageService.ValidateAndAssignBoxStudyAsync before writing
    // it -- a box already holding a different study's specimens throws InvalidOperationException,
    // which rolls back the whole batch (same transaction as the rest of this method).
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

    // Batch barcode uniqueness check (scoped to a study)
    Task<List<string>> GetTakenBarcodesAsync(IEnumerable<string> barcodes, int studyId);
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
