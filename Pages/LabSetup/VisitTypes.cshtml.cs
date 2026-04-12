using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.LabSetup
{
    [Authorize(Roles = "Admin")]
    public class VisitTypesModel : PageModel
    {
        private readonly ISampleService _sampleService;

        public VisitTypesModel(ISampleService sampleService)
        {
            _sampleService = sampleService;
        }

        public List<VisitType> VisitTypes { get; set; } = new();

        [BindProperty]
        public VisitTypeInput Input { get; set; } = new();

        public class VisitTypeInput
        {
            public int? VisitTypeID { get; set; }

            [Required]
            [StringLength(100)]
            [Display(Name = "Visit Type Name")]
            public string VisitTypeName { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            VisitTypes = await _sampleService.GetAllVisitTypes();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (!ModelState.IsValid)
            {
                VisitTypes = await _sampleService.GetAllVisitTypes();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            try
            {
                await _sampleService.AddVisitType(Input.VisitTypeName, userId);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("Input.VisitTypeName", "This visit type name is already in use.");
                VisitTypes = await _sampleService.GetAllVisitTypes();
                return Page();
            }

            TempData["Success"] = $"Visit type \"{Input.VisitTypeName}\" added.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            if (!ModelState.IsValid || Input.VisitTypeID == null)
            {
                VisitTypes = await _sampleService.GetAllVisitTypes();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            try
            {
                await _sampleService.UpdateVisitType(Input.VisitTypeID.Value, Input.VisitTypeName, userId);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("Input.VisitTypeName", "This visit type name is already in use.");
                VisitTypes = await _sampleService.GetAllVisitTypes();
                return Page();
            }

            TempData["Success"] = $"Visit type \"{Input.VisitTypeName}\" updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            try
            {
                await _sampleService.DeleteVisitType(id, userId);
                TempData["Success"] = "Visit type deleted.";
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToPage();
        }
    }
}
