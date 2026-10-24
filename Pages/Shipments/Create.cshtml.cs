using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Shipments
{
    public class CreateModel : PageModel
    {
        private readonly IShipmentService _shipmentService;
        private readonly ISampleService _sampleService;
        private readonly IStudyAccessService _studyAccessService;

        public CreateModel(IShipmentService shipmentService, ISampleService sampleService, IStudyAccessService studyAccessService)
        {
            _shipmentService = shipmentService;
            _sampleService = sampleService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty]
        public IFormFile? CsvFile { get; set; }

        [BindProperty]
        public string? RequestorName { get; set; }

        [BindProperty]
        public string? RequestorEmail { get; set; }

        public ShipmentBatch? ImportedBatch { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var (isSystemAdmin, editableStudyIds) = await ResolveEditableStudyIdsAsync(userId);
            if (!isSystemAdmin && editableStudyIds!.Count == 0)
            {
                return Forbid();
            }

            if (CsvFile == null || CsvFile.Length == 0)
            {
                ModelState.AddModelError("CsvFile", "Please select a CSV file.");
                return Page();
            }

            using var stream = CsvFile.OpenReadStream();
            ImportedBatch = await _shipmentService.ImportBatchFromCsvAsync(
                stream, userId, RequestorName, RequestorEmail, isSystemAdmin ? null : editableStudyIds);

            return Page();
        }

        // Same body as Pages/Samples/Import.cshtml.cs's ResolveEditableStudyIdsAsync.
        private async Task<(bool IsSystemAdmin, IReadOnlyCollection<int>? EditableStudyIds)> ResolveEditableStudyIdsAsync(string userId)
        {
            var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(userId);
            if (isSystemAdmin)
            {
                return (true, null);
            }

            var visibleStudyIds = await _studyAccessService.GetVisibleStudyIdsAsync(userId);
            var candidateStudies = await _sampleService.GetAllStudies(visibleStudyIds);

            var editableStudyIds = new List<int>();
            foreach (var study in candidateStudies)
            {
                if (await _studyAccessService.CanEditAsync(userId, study.StudyID))
                {
                    editableStudyIds.Add(study.StudyID);
                }
            }

            return (false, editableStudyIds);
        }
    }
}
