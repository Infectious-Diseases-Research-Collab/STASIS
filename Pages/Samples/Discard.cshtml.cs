using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Samples
{
    [Authorize(Roles = "Write,Admin")]
    public class DiscardModel : PageModel
    {
        private readonly ISampleService _sampleService;

        public DiscardModel(ISampleService sampleService)
        {
            _sampleService = sampleService;
        }

        [BindProperty]
        public string? Barcodes { get; set; }

        public List<Specimen> FoundSpecimens { get; set; } = new();
        public List<string> NotFoundBarcodes { get; set; } = new();
        public List<string> InvalidBarcodes { get; set; } = new();

        public Approval? CreatedApproval { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostLookupAsync()
        {
            if (string.IsNullOrWhiteSpace(Barcodes))
            {
                ModelState.AddModelError("Barcodes", "Enter at least one barcode.");
                return Page();
            }

            var barcodeList = Barcodes
                .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var barcode in barcodeList)
            {
                var specimen = await _sampleService.GetSpecimenByBarcode(barcode);
                if (specimen == null)
                {
                    NotFoundBarcodes.Add(barcode);
                }
                else if (specimen.Status == "Discarded")
                {
                    InvalidBarcodes.Add($"{barcode} (already discarded)");
                }
                else if (specimen.Status == "Shipped")
                {
                    InvalidBarcodes.Add($"{barcode} (already shipped)");
                }
                else if (specimen.DiscardApprovalID != null)
                {
                    InvalidBarcodes.Add($"{barcode} (discard already requested)");
                }
                else
                {
                    FoundSpecimens.Add(specimen);
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostRequestAsync()
        {
            if (string.IsNullOrWhiteSpace(Barcodes))
                return RedirectToPage();

            var barcodeList = Barcodes
                .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var specimenIds = new List<int>();
            foreach (var barcode in barcodeList)
            {
                var specimen = await _sampleService.GetSpecimenByBarcode(barcode);
                if (specimen != null &&
                    specimen.Status != "Discarded" &&
                    specimen.Status != "Shipped" &&
                    specimen.DiscardApprovalID == null)
                {
                    specimenIds.Add(specimen.SpecimenID);
                }
            }

            if (specimenIds.Count == 0)
            {
                ModelState.AddModelError("", "No valid specimens to discard.");
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            CreatedApproval = await _sampleService.RequestDiscardAsync(specimenIds, userId);

            TempData["Success"] = $"Discard request created (Approval #{CreatedApproval.ApprovalID}) for {specimenIds.Count} specimen(s). Awaiting ED, Regulatory, and PI approval.";
            return Page();
        }
    }
}
