using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using STASIS.Models;
using STASIS.Services;
using System.ComponentModel.DataAnnotations;

namespace STASIS.Pages.LabSetup
{
    [Authorize(Roles = "Admin")]
    public class CompartmentsModel : PageModel
    {
        private readonly ILabSetupService _labSetupService;
        private readonly IAuditService _auditService;

        public CompartmentsModel(ILabSetupService labSetupService, IAuditService auditService)
        {
            _labSetupService = labSetupService;
            _auditService = auditService;
        }

        public List<Compartment> Compartments { get; set; } = new();
        public SelectList FreezerOptions { get; set; } = new SelectList(Enumerable.Empty<object>());

        [BindProperty]
        public CompartmentInputModel Input { get; set; } = new();

        public class CompartmentInputModel
        {
            public int? CompartmentID { get; set; }

            [Required]
            [StringLength(100)]
            public string CompartmentName { get; set; } = string.Empty;

            [StringLength(500)]
            public string? Description { get; set; }

            [Required]
            public int FreezerID { get; set; }
        }

        public async Task OnGetAsync()
        {
            Compartments = await _labSetupService.GetAllCompartmentsAsync();
            await LoadFreezersAsync();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (!ModelState.IsValid)
            {
                Compartments = await _labSetupService.GetAllCompartmentsAsync();
                await LoadFreezersAsync();
                return Page();
            }

            var compartment = new Compartment
            {
                CompartmentName = Input.CompartmentName,
                Description = Input.Description,
                FreezerID = Input.FreezerID
            };

            await _labSetupService.AddCompartmentAsync(compartment);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            await _auditService.LogChangeAsync("tbl_Compartments", compartment.CompartmentID.ToString(),
                "Created", null, compartment.CompartmentName, userId);

            TempData["Success"] = $"Compartment \"{compartment.CompartmentName}\" added.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            if (!ModelState.IsValid || Input.CompartmentID == null)
            {
                Compartments = await _labSetupService.GetAllCompartmentsAsync();
                await LoadFreezersAsync();
                return Page();
            }

            var existing = await _labSetupService.GetCompartmentByIdAsync(Input.CompartmentID.Value);
            if (existing == null) return NotFound();

            var oldName = existing.CompartmentName;
            existing.CompartmentName = Input.CompartmentName;
            existing.Description = Input.Description;
            existing.FreezerID = Input.FreezerID;

            await _labSetupService.UpdateCompartmentAsync(existing);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            if (oldName != existing.CompartmentName)
                await _auditService.LogChangeAsync("tbl_Compartments", existing.CompartmentID.ToString(),
                    "CompartmentName", oldName, existing.CompartmentName, userId);

            TempData["Success"] = $"Compartment \"{existing.CompartmentName}\" updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var deleted = await _labSetupService.DeleteCompartmentAsync(id);
            if (!deleted)
                TempData["Error"] = "Cannot delete compartment — it still has racks assigned to it.";
            else
                TempData["Success"] = "Compartment deleted.";

            return RedirectToPage();
        }

        private async Task LoadFreezersAsync()
        {
            var freezers = await _labSetupService.GetAllFreezersAsync();
            FreezerOptions = new SelectList(freezers, "FreezerID", "FreezerName");
        }
    }
}
