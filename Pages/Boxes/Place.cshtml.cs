using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Boxes
{
    public class PlaceModel : PageModel
    {
        private readonly IStorageService _storageService;
        private readonly ISampleService _sampleService;
        private readonly IStudyAccessService _studyAccessService;

        public PlaceModel(IStorageService storageService, ISampleService sampleService, IStudyAccessService studyAccessService)
        {
            _storageService = storageService;
            _sampleService = sampleService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty(SupportsGet = true)]
        public string? BoxLabel { get; set; }

        [BindProperty]
        public int BoxId { get; set; }

        [BindProperty]
        public int NewRackId { get; set; }

        public Box? FoundBox { get; set; }
        public SelectList FreezerOptions { get; set; } = new SelectList(Enumerable.Empty<object>());
        public List<Rack> AllRacks { get; set; } = new();

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrEmpty(BoxLabel))
            {
                // Study-scoped filtering of the box placement workflow is out of scope for this
                // task (box detail/placement write paths are a later task) -- see
                // STASIS_Deferred_Items.md item 9. Passing null here is the intentional,
                // explicit "unfiltered for now" call, not an accidental default.
                FoundBox = await _storageService.GetBoxByLabelAsync(BoxLabel, null);
            }
            await LoadOptionsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            if (!await IsEditorAnywhereAsync(userId))
            {
                return Forbid();
            }

            await _storageService.MoveBoxAsync(BoxId, NewRackId, userId);
            TempData["Success"] = "Box moved successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetCompartmentsAsync(int freezerId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            if (!await IsEditorAnywhereAsync(userId))
            {
                return Forbid();
            }

            var compartments = await _storageService.GetCompartmentsByFreezer(freezerId);
            return new JsonResult(compartments.Select(c => new { c.CompartmentID, c.CompartmentName }));
        }

        public async Task<IActionResult> OnGetRacksAsync(int compartmentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            if (!await IsEditorAnywhereAsync(userId))
            {
                return Forbid();
            }

            var racks = await _storageService.GetRacksByCompartment(compartmentId);
            return new JsonResult(racks.Select(r => new { r.RackID, r.RackName }));
        }

        private async Task LoadOptionsAsync()
        {
            var freezers = await _storageService.GetAllFreezers();
            FreezerOptions = new SelectList(freezers, "FreezerID", "FreezerName");
        }

        // Box relocation isn't tied to a specific study, so this is an "is this person an Editor
        // anywhere" check, not a per-study one -- mirrors Pages/Boxes/Create.cshtml.cs's
        // IsEditorAnywhereAsync exactly (there's no bulk "editable study ids" method on
        // IStudyAccessService, so this resolves the caller's own visible studies first and then
        // checks CanEditAsync per study, short-circuiting on the first match).
        private async Task<bool> IsEditorAnywhereAsync(string userId)
        {
            if (await _studyAccessService.IsSystemAdminAsync(userId))
            {
                return true;
            }

            var visibleStudyIds = await _studyAccessService.GetVisibleStudyIdsAsync(userId);
            var candidateStudies = await _sampleService.GetAllStudies(visibleStudyIds);

            foreach (var study in candidateStudies)
            {
                if (await _studyAccessService.CanEditAsync(userId, study.StudyID))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
