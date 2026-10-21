using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Samples
{
    public class DiscardModel : PageModel
    {
        private readonly ISampleService _sampleService;
        private readonly IStudyAccessService _studyAccessService;

        public DiscardModel(ISampleService sampleService, IStudyAccessService studyAccessService)
        {
            _sampleService = sampleService;
            _studyAccessService = studyAccessService;
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
                .Select(b => b.Trim().ToUpperInvariant())
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct()
                .ToList();

            foreach (var barcode in barcodeList)
            {
                var matches = await _sampleService.GetSpecimensByBarcode(barcode);
                if (matches.Count == 0)
                {
                    NotFoundBarcodes.Add(barcode);
                    continue;
                }
                foreach (var specimen in matches)
                {
                    if (specimen.Status == "Discarded")
                        InvalidBarcodes.Add($"{barcode} ({specimen.Study?.StudyCode}) — already discarded");
                    else if (specimen.Status == "Shipped")
                        InvalidBarcodes.Add($"{barcode} ({specimen.Study?.StudyCode}) — already shipped");
                    else if (specimen.DiscardApprovalID != null)
                        InvalidBarcodes.Add($"{barcode} ({specimen.Study?.StudyCode}) — discard already requested");
                    else
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
                .Select(b => b.Trim().ToUpperInvariant())
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct()
                .ToList();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var specimenIds = new List<int>();
            var skippedBarcodes = new List<string>();
            foreach (var barcode in barcodeList)
            {
                var matches = await _sampleService.GetSpecimensByBarcode(barcode);
                foreach (var specimen in matches)
                {
                    if (specimen.Status != "Discarded" &&
                        specimen.Status != "Shipped" &&
                        specimen.DiscardApprovalID == null)
                    {
                        // Per-specimen skip (not a whole-request reject): a mixed batch of
                        // editable and non-editable-study barcodes discards only the ones the
                        // caller can edit, mirroring the per-row skip pattern in Samples/Import.
                        if (await _studyAccessService.CanEditAsync(userId, specimen.StudyID))
                        {
                            specimenIds.Add(specimen.SpecimenID);
                        }
                        else
                        {
                            skippedBarcodes.Add($"{barcode} ({specimen.Study?.StudyCode}) — no edit access to this study");
                        }
                    }
                }
            }

            if (specimenIds.Count == 0)
            {
                ModelState.AddModelError("", "No valid specimens to discard.");
                return Page();
            }

            CreatedApproval = await _sampleService.RequestDiscardAsync(specimenIds, userId);

            var message = $"Discard request created (Approval #{CreatedApproval.ApprovalID}) for {specimenIds.Count} specimen(s). Awaiting ED, Regulatory, and PI approval.";
            if (skippedBarcodes.Count > 0)
            {
                message += $" Skipped {skippedBarcodes.Count} specimen(s) you do not have edit access to: {string.Join(", ", skippedBarcodes)}";
            }
            TempData["Success"] = message;
            return Page();
        }
    }
}
