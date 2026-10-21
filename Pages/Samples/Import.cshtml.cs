using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STASIS.Services;

namespace STASIS.Pages.Samples
{
    public class ImportModel : PageModel
    {
        private readonly ISampleService _sampleService;
        private readonly IAuditService _auditService;
        private readonly IStorageService _storageService;
        private readonly IStudyAccessService _studyAccessService;

        public ImportModel(ISampleService sampleService, IAuditService auditService,
            IStorageService storageService, IStudyAccessService studyAccessService)
        {
            _sampleService = sampleService;
            _auditService = auditService;
            _storageService = storageService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty]
        public IFormFile? CsvFile { get; set; }

        public ImportResult? PreviewResult { get; set; }

        [TempData]
        public int ImportedCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var (isSystemAdmin, editableStudyIds) = await ResolveEditableStudyIdsAsync(userId);
            if (!isSystemAdmin && editableStudyIds!.Count == 0)
            {
                return Forbid();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostPreviewAsync()
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
            PreviewResult = await _sampleService.ImportSpecimensFromCsv(stream, isSystemAdmin ? null : editableStudyIds);

            // Store the valid rows in TempData as a serialized CSV so they can be committed
            if (PreviewResult.SuccessRows.Count > 0)
            {
                // Re-read the file and store it for the commit step
                CsvFile = CsvFile; // keep the form visible
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCommitAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var (isSystemAdmin, editableStudyIds) = await ResolveEditableStudyIdsAsync(userId);
            if (!isSystemAdmin && editableStudyIds!.Count == 0)
            {
                return Forbid();
            }

            if (CsvFile == null || CsvFile.Length == 0)
            {
                ModelState.AddModelError("CsvFile", "Please re-upload the CSV file to commit.");
                return Page();
            }

            using var stream = CsvFile.OpenReadStream();
            var result = await _sampleService.ImportSpecimensFromCsv(stream, isSystemAdmin ? null : editableStudyIds);

            if (result.SuccessRows.Count == 0)
            {
                PreviewResult = result;
                ModelState.AddModelError(string.Empty, "No valid rows to import.");
                return Page();
            }

            int committed = 0;

            foreach (var row in result.SuccessRows)
            {
                if (row.Specimen != null)
                {
                    // Validate (and, if the box is unclaimed, assign) the box's study affiliation
                    // before writing this row -- a box already holding a different study's
                    // specimens throws InvalidOperationException. Per-row: this row is skipped
                    // (moved to ErrorRows, matching the existing error-row shape) while the rest
                    // of the commit loop continues -- there is no batch transaction here to
                    // preserve or break, each AddSpecimen call below already does its own
                    // SaveChangesAsync.
                    if (row.Specimen.BoxID.HasValue)
                    {
                        try
                        {
                            await _storageService.ValidateAndAssignBoxStudyAsync(row.Specimen.BoxID.Value, row.Specimen.StudyID);
                        }
                        catch (InvalidOperationException ex)
                        {
                            result.ErrorRows.Add(new ImportRow
                            {
                                LineNumber = row.LineNumber,
                                BarcodeID = row.BarcodeID,
                                LegacyID = row.LegacyID,
                                StudyCode = row.StudyCode,
                                SampleType = row.SampleType,
                                CollectionDate = row.CollectionDate,
                                BoxLabel = row.BoxLabel,
                                PositionRow = row.PositionRow,
                                PositionCol = row.PositionCol,
                                ParticipantID = row.ParticipantID,
                                CellCount = row.CellCount,
                                Error = ex.Message
                            });
                            continue;
                        }
                    }

                    await _sampleService.AddSpecimen(row.Specimen);
                    await _auditService.LogChangeAsync("tbl_Specimens",
                        row.Specimen.SpecimenID.ToString(),
                        "Imported", null, row.Specimen.BarcodeID, userId);
                    committed++;
                }
            }

            TempData["Success"] = $"Successfully imported {committed} specimen(s). {result.ErrorRows.Count} row(s) skipped.";
            return RedirectToPage("/Samples");
        }

        // Resolves the caller's editable-study set for this request: (true, null) for a System
        // Admin (unrestricted), otherwise (false, <set>) where <set> is every study the caller
        // holds Editor-or-above access to. There's no bulk "editable study ids" method on
        // IStudyAccessService, so this narrows to the caller's visible studies first (bounded by
        // their own small assigned-study count, not the whole Studies table) and then filters
        // with CanEditAsync per study -- same bounded-loop pattern as Samples/Add's Study
        // dropdown (Pages/Samples/Add.cshtml.cs, LoadDropdownsAsync).
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
