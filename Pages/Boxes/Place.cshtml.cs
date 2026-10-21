using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Boxes
{
    [Authorize(Roles = "Write,Admin")]
    public class PlaceModel : PageModel
    {
        private readonly IStorageService _storageService;

        public PlaceModel(IStorageService storageService)
        {
            _storageService = storageService;
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
            await _storageService.MoveBoxAsync(BoxId, NewRackId, userId);
            TempData["Success"] = "Box moved successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetCompartmentsAsync(int freezerId)
        {
            var compartments = await _storageService.GetCompartmentsByFreezer(freezerId);
            return new JsonResult(compartments.Select(c => new { c.CompartmentID, c.CompartmentName }));
        }

        public async Task<IActionResult> OnGetRacksAsync(int compartmentId)
        {
            var racks = await _storageService.GetRacksByCompartment(compartmentId);
            return new JsonResult(racks.Select(r => new { r.RackID, r.RackName }));
        }

        private async Task LoadOptionsAsync()
        {
            var freezers = await _storageService.GetAllFreezers();
            FreezerOptions = new SelectList(freezers, "FreezerID", "FreezerName");
        }
    }
}
