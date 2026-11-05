using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STASIS.Services;

namespace STASIS.Pages.Boxes
{
    public class CreateModel : PageModel
    {
        private readonly IStorageService _storageService;
        private readonly ISampleService _sampleService;
        private readonly IStudyAccessService _studyAccessService;

        public CreateModel(IStorageService storageService, ISampleService sampleService, IStudyAccessService studyAccessService)
        {
            _storageService = storageService;
            _sampleService = sampleService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty]
        [Required(ErrorMessage = "Box label is required.")]
        [StringLength(100)]
        public string BoxLabel { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Box type is required.")]
        public string BoxType { get; set; } = string.Empty;

        [BindProperty]
        public int? RackId { get; set; }

        public SelectList FreezerOptions { get; set; } = new SelectList(Enumerable.Empty<object>());

        public static readonly List<string> BoxTypes = new() { "81-slot", "100-slot", "Filter Paper Binder" };

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            // Box creation isn't tied to a specific study yet, so this is an "is this person an
            // Editor anywhere" check, not a per-study one.
            if (!await IsEditorAnywhereAsync(userId))
            {
                return Forbid();
            }

            await LoadFreezersAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            if (!await IsEditorAnywhereAsync(userId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                await LoadFreezersAsync();
                return Page();
            }

            try
            {
                var box = await _storageService.CreateBoxAsync(BoxLabel.Trim(), BoxType, RackId, userId);
                TempData["Success"] = $"Box \"{box.BoxLabel}\" created successfully.";
                return RedirectToPage("/Boxes/Search", new { boxLabel = box.BoxLabel });
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(BoxLabel), "A box with that label already exists.");
                await LoadFreezersAsync();
                return Page();
            }
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

        private async Task LoadFreezersAsync()
        {
            var freezers = await _storageService.GetAllFreezers();
            FreezerOptions = new SelectList(freezers, "FreezerID", "FreezerName");
        }

        // Box creation isn't tied to a specific study, so the check here is "does this caller
        // hold Editor-or-above access to at least one study" rather than a per-study check.
        // There's no bulk "editable study ids" method on IStudyAccessService, so this resolves
        // the caller's own visible studies first (bounded by their own small assigned-study
        // count, not the whole Studies table) and then checks CanEditAsync per study, short-
        // circuiting on the first match -- same bounded-loop pattern as Samples/Add's Study
        // dropdown and Samples/Import's ResolveEditableStudyIdsAsync.
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
