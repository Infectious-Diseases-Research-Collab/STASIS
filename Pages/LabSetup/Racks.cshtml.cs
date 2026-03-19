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
    public class RacksModel : PageModel
    {
        private readonly ILabSetupService _labSetupService;
        private readonly IAuditService _auditService;

        public RacksModel(ILabSetupService labSetupService, IAuditService auditService)
        {
            _labSetupService = labSetupService;
            _auditService = auditService;
        }

        public List<Rack> Racks { get; set; } = new();
        public SelectList FreezerOptions { get; set; } = new SelectList(Enumerable.Empty<object>());
        public SelectList CompartmentOptions { get; set; } = new SelectList(Enumerable.Empty<object>());

        [BindProperty]
        public RackInputModel Input { get; set; } = new();

        public class RackInputModel
        {
            public int? RackID { get; set; }

            [Required]
            [StringLength(100)]
            public string RackName { get; set; } = string.Empty;

            [StringLength(500)]
            public string? Description { get; set; }

            public int? CompartmentID { get; set; }
        }

        public async Task OnGetAsync()
        {
            Racks = await _labSetupService.GetAllRacksAsync();
            await LoadOptionsAsync();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (!ModelState.IsValid)
            {
                Racks = await _labSetupService.GetAllRacksAsync();
                await LoadOptionsAsync();
                return Page();
            }

            var rack = new Rack
            {
                RackName = Input.RackName,
                Description = Input.Description,
                CompartmentID = Input.CompartmentID
            };

            await _labSetupService.AddRackAsync(rack);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            await _auditService.LogChangeAsync("tbl_Racks", rack.RackID.ToString(),
                "Created", null, rack.RackName, userId);

            TempData["Success"] = $"Rack \"{rack.RackName}\" added.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            if (!ModelState.IsValid || Input.RackID == null)
            {
                Racks = await _labSetupService.GetAllRacksAsync();
                await LoadOptionsAsync();
                return Page();
            }

            var existing = await _labSetupService.GetRackByIdAsync(Input.RackID.Value);
            if (existing == null) return NotFound();

            var oldName = existing.RackName;
            existing.RackName = Input.RackName;
            existing.Description = Input.Description;
            existing.CompartmentID = Input.CompartmentID;

            await _labSetupService.UpdateRackAsync(existing);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            if (oldName != existing.RackName)
                await _auditService.LogChangeAsync("tbl_Racks", existing.RackID.ToString(),
                    "RackName", oldName, existing.RackName, userId);

            TempData["Success"] = $"Rack \"{existing.RackName}\" updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var deleted = await _labSetupService.DeleteRackAsync(id);
            if (!deleted)
                TempData["Error"] = "Cannot delete rack — it still has boxes assigned to it.";
            else
                TempData["Success"] = "Rack deleted.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetCompartmentsAsync(int freezerId)
        {
            var compartments = await _labSetupService.GetCompartmentsByFreezerAsync(freezerId);
            return new JsonResult(compartments.Select(c => new { c.CompartmentID, c.CompartmentName }));
        }

        private async Task LoadOptionsAsync()
        {
            var freezers = await _labSetupService.GetAllFreezersAsync();
            FreezerOptions = new SelectList(freezers, "FreezerID", "FreezerName");

            var compartments = await _labSetupService.GetAllCompartmentsAsync();
            CompartmentOptions = new SelectList(compartments.Select(c => new {
                c.CompartmentID,
                Display = $"{c.Freezer?.FreezerName} > {c.CompartmentName}"
            }), "CompartmentID", "Display");
        }
    }
}
