using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Boxes
{
    public class MoveModel : PageModel
    {
        private readonly ISampleService _sampleService;
        private readonly IStorageService _storageService;
        private readonly IStudyAccessService _studyAccessService;

        public MoveModel(ISampleService sampleService, IStorageService storageService, IStudyAccessService studyAccessService)
        {
            _sampleService = sampleService;
            _storageService = storageService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty(SupportsGet = true)]
        public string? Barcode { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SpecimenId { get; set; }

        [BindProperty]
        public int NewBoxId { get; set; }

        [BindProperty]
        public int NewRow { get; set; }

        [BindProperty]
        public int NewCol { get; set; }

        [BindProperty]
        public bool MoveToTemp { get; set; }

        public Specimen? FoundSpecimen { get; set; }
        public List<Specimen> AmbiguousSpecimens { get; set; } = new();
        public SelectList BoxOptions { get; set; } = new SelectList(Enumerable.Empty<object>());

        public async Task OnGetAsync()
        {
            if (SpecimenId.HasValue)
            {
                FoundSpecimen = await _sampleService.GetSpecimenDetailAsync(SpecimenId.Value);
            }
            else if (!string.IsNullOrEmpty(Barcode))
            {
                var matches = await _sampleService.GetSpecimensByBarcode(Barcode);
                if (matches.Count == 1)
                    FoundSpecimen = matches[0];
                else if (matches.Count > 1)
                    AmbiguousSpecimens = matches;
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            await LoadBoxOptionsAsync(userId);
        }

        public async Task<IActionResult> OnPostMoveAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            if (SpecimenId == null)
            {
                ModelState.AddModelError(string.Empty, "No specimen selected.");
                await LoadBoxOptionsAsync(userId);
                return Page();
            }

            var specimen = await _sampleService.GetSpecimenDetailAsync(SpecimenId.Value);
            if (specimen == null)
            {
                ModelState.AddModelError(string.Empty, "Specimen not found.");
                await LoadBoxOptionsAsync(userId);
                return Page();
            }

            // Re-validate CanEditAsync against the specimen's current StudyID before calling
            // into IStorageService -- this is the access check; the box/study-mixing check
            // itself is already enforced inside MoveSpecimenAsync via
            // ValidateAndAssignBoxStudyAsync.
            if (!await _studyAccessService.CanEditAsync(userId, specimen.StudyID))
            {
                ModelState.AddModelError(string.Empty, "You do not have permission to move specimens in this study.");
                FoundSpecimen = specimen;
                await LoadBoxOptionsAsync(userId);
                return Page();
            }

            if (MoveToTemp)
            {
                await _storageService.MoveToTempAsync(SpecimenId.Value, userId);
                TempData["Success"] = "Specimen moved to temporary storage.";
                return RedirectToPage();
            }

            try
            {
                await _storageService.MoveSpecimenAsync(SpecimenId.Value, NewBoxId, NewRow, NewCol, userId);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "That position is already occupied.");
                FoundSpecimen = specimen;
                await LoadBoxOptionsAsync(userId);
                return Page();
            }

            TempData["Success"] = "Specimen moved successfully.";
            return RedirectToPage();
        }

        // Box dropdown: filtered via the caller's own GetVisibleStudyIdsAsync set (not a
        // separately-built "editable only" set -- there's no bulk editable-study-ids method on
        // IStudyAccessService; see Pages/Samples/Add.cshtml.cs/Import.cshtml.cs for the
        // established per-study-CanEditAsync-loop pattern used where that distinction matters).
        // This list is a UX convenience, not the security boundary: the real edit gate is the
        // CanEditAsync check in OnPostMoveAsync against the specimen's own study, and
        // ValidateAndAssignBoxStudyAsync (inside MoveSpecimenAsync) independently enforces that a
        // box never mixes studies. Replaces the previous unfiltered manual
        // freezer/compartment/rack/box nested loop with Task 10's filtered
        // IStorageService.GetAllBoxesAsync.
        private async Task LoadBoxOptionsAsync(string userId)
        {
            var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(userId);
            var visibleStudyIds = isSystemAdmin ? null : await _studyAccessService.GetVisibleStudyIdsAsync(userId);
            var allBoxes = await _storageService.GetAllBoxesAsync(visibleStudyIds);
            BoxOptions = new SelectList(
                allBoxes.Select(b => new {
                    b.BoxID,
                    Display = b.Rack != null
                        ? $"{b.BoxLabel} ({b.Rack.Compartment?.Freezer?.FreezerName} > {b.Rack.Compartment?.CompartmentName} > {b.Rack.RackName})"
                        : $"{b.BoxLabel} (unassigned)"
                }), "BoxID", "Display");
        }
    }
}
