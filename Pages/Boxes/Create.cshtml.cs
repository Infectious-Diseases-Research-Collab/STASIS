using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STASIS.Services;

namespace STASIS.Pages.Boxes
{
    [Authorize(Roles = "Write,Admin")]
    public class CreateModel : PageModel
    {
        private readonly IStorageService _storageService;

        public CreateModel(IStorageService storageService)
        {
            _storageService = storageService;
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

        public async Task OnGetAsync()
        {
            await LoadFreezersAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadFreezersAsync();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

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
            var compartments = await _storageService.GetCompartmentsByFreezer(freezerId);
            return new JsonResult(compartments.Select(c => new { c.CompartmentID, c.CompartmentName }));
        }

        public async Task<IActionResult> OnGetRacksAsync(int compartmentId)
        {
            var racks = await _storageService.GetRacksByCompartment(compartmentId);
            return new JsonResult(racks.Select(r => new { r.RackID, r.RackName }));
        }

        private async Task LoadFreezersAsync()
        {
            var freezers = await _storageService.GetAllFreezers();
            FreezerOptions = new SelectList(freezers, "FreezerID", "FreezerName");
        }
    }
}
