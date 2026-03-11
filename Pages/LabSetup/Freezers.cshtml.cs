using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STASIS.Models;
using STASIS.Services;
using System.ComponentModel.DataAnnotations;

namespace STASIS.Pages.LabSetup
{
    [Authorize(Roles = "Admin")]
    public class FreezersModel : PageModel
    {
        private readonly ILabSetupService _labSetupService;
        private readonly IAuditService _auditService;

        public FreezersModel(ILabSetupService labSetupService, IAuditService auditService)
        {
            _labSetupService = labSetupService;
            _auditService = auditService;
        }

        public List<Freezer> Freezers { get; set; } = new();

        [BindProperty]
        public FreezerInputModel Input { get; set; } = new();

        public class FreezerInputModel
        {
            public int? FreezerID { get; set; }

            [Required]
            [StringLength(100)]
            public string FreezerName { get; set; } = string.Empty;

            public int? Temperature { get; set; }

            [StringLength(200)]
            public string? LocationInBuilding { get; set; }
        }

        public async Task OnGetAsync()
        {
            Freezers = await _labSetupService.GetAllFreezersAsync();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (!ModelState.IsValid)
            {
                Freezers = await _labSetupService.GetAllFreezersAsync();
                return Page();
            }

            var freezer = new Freezer
            {
                FreezerName = Input.FreezerName,
                Temperature = Input.Temperature,
                LocationInBuilding = Input.LocationInBuilding
            };

            await _labSetupService.AddFreezerAsync(freezer);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            await _auditService.LogChangeAsync("tbl_Freezers", freezer.FreezerID.ToString(),
                "Created", null, freezer.FreezerName, userId);

            TempData["Success"] = $"Freezer \"{freezer.FreezerName}\" added.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            if (!ModelState.IsValid || Input.FreezerID == null)
            {
                Freezers = await _labSetupService.GetAllFreezersAsync();
                return Page();
            }

            var existing = await _labSetupService.GetFreezerByIdAsync(Input.FreezerID.Value);
            if (existing == null) return NotFound();

            var oldName = existing.FreezerName;
            existing.FreezerName = Input.FreezerName;
            existing.Temperature = Input.Temperature;
            existing.LocationInBuilding = Input.LocationInBuilding;

            await _labSetupService.UpdateFreezerAsync(existing);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            if (oldName != existing.FreezerName)
                await _auditService.LogChangeAsync("tbl_Freezers", existing.FreezerID.ToString(),
                    "FreezerName", oldName, existing.FreezerName, userId);

            TempData["Success"] = $"Freezer \"{existing.FreezerName}\" updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var deleted = await _labSetupService.DeleteFreezerAsync(id);
            if (!deleted)
                TempData["Error"] = "Cannot delete freezer — it still has racks assigned to it.";
            else
                TempData["Success"] = "Freezer deleted.";

            return RedirectToPage();
        }
    }
}
