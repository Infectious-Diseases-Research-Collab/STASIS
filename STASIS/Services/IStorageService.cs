using STASIS.Models;

namespace STASIS.Services;

public interface IStorageService
{
    Task<List<Freezer>> GetAllFreezers();
    Task<List<Compartment>> GetCompartmentsByFreezer(int freezerId);
    Task<List<Rack>> GetRacksByCompartment(int compartmentId);
    Task<List<Box>> GetBoxesByRack(int rackId);

    // visibleStudyIds: null = unrestricted (System Admin). Non-null = the caller's assigned-study
    // set (per IStudyAccessService.GetVisibleStudyIdsAsync); an empty collection means the caller
    // has zero study assignments, and must see zero boxes -- including unassigned ones -- per the
    // "zero access means an empty state, never a fallback to see everything" rule. When non-null
    // and non-empty, a box is visible if its StudyID is in the set OR its StudyID is null
    // (an unassigned box isn't yet claimed by any study, so it must stay discoverable to anyone
    // with at least one study assignment).
    Task<Box?> GetBoxWithSpecimens(int boxId, IReadOnlyCollection<int>? visibleStudyIds);

    // Phase 3: Search and movement
    Task<List<string>> GetAllBoxLabelsAsync(IReadOnlyCollection<int>? visibleStudyIds);
    Task<List<Box>> GetAllBoxesAsync(IReadOnlyCollection<int>? visibleStudyIds);
    Task<List<Box>> SearchBoxesAsync(string? label, int? freezerId, int? rackId, IReadOnlyCollection<int>? visibleStudyIds);
    Task<Box?> GetBoxByLabelAsync(string label, IReadOnlyCollection<int>? visibleStudyIds);
    Task MoveSpecimenAsync(int specimenId, int newBoxId, int row, int col, string userId);
    Task MoveBoxAsync(int boxId, int newRackId, string userId);
    Task ReboxSpecimensAsync(List<(int SpecimenId, int Row, int Col)> placements, int newBoxId, string userId);
    Task MoveToTempAsync(int specimenId, string userId);
    Task CheckAndUnassignEmptyBoxAsync(int boxId);
    Task<Box> CreateBoxAsync(string label, string boxType, int? rackId, string userId);
}
