using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Boxes
{
    public class ReboxModel : PageModel
    {
        private readonly ISampleService _sampleService;
        private readonly IStorageService _storageService;
        private readonly IStudyAccessService _studyAccessService;

        public ReboxModel(ISampleService sampleService, IStorageService storageService, IStudyAccessService studyAccessService)
        {
            _sampleService = sampleService;
            _storageService = storageService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty]
        public int DestinationBoxId { get; set; }

        [BindProperty]
        public string? ScanBarcode { get; set; }

        [BindProperty]
        public string ScannedListJson { get; set; } = "[]";

        public SelectList BoxOptions { get; set; } = new SelectList(Enumerable.Empty<object>());

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            await LoadBoxOptionsAsync(userId);
        }

        public async Task<IActionResult> OnGetLookupAsync(string barcode, int? specimenId = null)
        {
            Specimen? specimen = null;

            if (specimenId.HasValue)
            {
                specimen = await _sampleService.GetSpecimenDetailAsync(specimenId.Value);
                if (specimen == null) return new JsonResult(new { found = false });
            }
            else
            {
                var matches = await _sampleService.GetSpecimensByBarcode(barcode);
                if (matches.Count == 0)
                    return new JsonResult(new { found = false });

                if (matches.Count > 1)
                    return new JsonResult(new
                    {
                        found = false,
                        ambiguous = true,
                        studies = matches.Select(s => new { specimenId = s.SpecimenID, study = s.Study?.StudyCode ?? "?" }).ToList()
                    });

                specimen = matches[0];
            }

            return new JsonResult(new
            {
                found = true,
                specimenId = specimen.SpecimenID,
                barcodeId = specimen.BarcodeID,
                sampleType = specimen.SampleType?.TypeName,
                currentBox = specimen.Box?.BoxLabel,
                currentPos = specimen.PositionRow.HasValue ? $"({specimen.PositionRow},{specimen.PositionCol})" : "-",
                status = specimen.Status
            });
        }

        public async Task<IActionResult> OnPostCommitAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var items = System.Text.Json.JsonSerializer.Deserialize<List<ReboxItem>>(ScannedListJson);
            if (items == null || items.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "No specimens scanned.");
                await LoadBoxOptionsAsync(userId);
                return Page();
            }

            // Per-item skip (not a whole-batch reject): re-validate CanEditAsync against each
            // specimen's current StudyID before calling into IStorageService -- this is the
            // access check only. The box/study-mixing check is already enforced inside
            // ReboxSpecimensAsync via ValidateAndAssignBoxStudyAsync.
            var placements = new List<(int SpecimenId, int Row, int Col)>();
            var skippedCount = 0;
            foreach (var item in items)
            {
                var specimen = await _sampleService.GetSpecimenByIdAsync(item.SpecimenId);
                if (specimen == null)
                {
                    skippedCount++;
                    continue;
                }

                if (!await _studyAccessService.CanEditAsync(userId, specimen.StudyID))
                {
                    skippedCount++;
                    continue;
                }

                placements.Add((item.SpecimenId, item.Row, item.Col));
            }

            if (placements.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "No scanned specimens you have edit access to.");
                await LoadBoxOptionsAsync(userId);
                return Page();
            }

            try
            {
                await _storageService.ReboxSpecimensAsync(placements, DestinationBoxId, userId);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "A position conflict occurred. Check for duplicates.");
                await LoadBoxOptionsAsync(userId);
                return Page();
            }

            var message = $"Successfully reboxed {placements.Count} specimen(s).";
            if (skippedCount > 0)
            {
                message += $" Skipped {skippedCount} specimen(s) you do not have edit access to.";
            }
            TempData["Success"] = message;
            return RedirectToPage("/Boxes/Search");
        }

        // Box dropdown: filtered via the caller's own GetVisibleStudyIdsAsync set -- see the
        // matching comment on Pages/Boxes/Move.cshtml.cs's LoadBoxOptionsAsync for why this is a
        // UX convenience list rather than the security boundary (the real edit gate is the
        // per-item CanEditAsync check in OnPostCommitAsync). Replaces the previous unfiltered
        // manual freezer/compartment/rack/box nested loop with Task 10's filtered
        // IStorageService.GetAllBoxesAsync.
        private async Task LoadBoxOptionsAsync(string userId)
        {
            var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(userId);
            var visibleStudyIds = isSystemAdmin ? null : await _studyAccessService.GetVisibleStudyIdsAsync(userId);
            var allBoxes = await _storageService.GetAllBoxesAsync(visibleStudyIds);
            BoxOptions = new SelectList(
                allBoxes.Select(b => new {
                    b.BoxID,
                    Display = b.Rack != null
                        ? $"{b.BoxLabel} ({b.Rack.Compartment?.Freezer?.FreezerName} > {b.Rack.Compartment?.CompartmentName} > {b.Rack.RackName})"
                        : $"{b.BoxLabel} (unassigned)"
                }), "BoxID", "Display");
        }

        public class ReboxItem
        {
            public int SpecimenId { get; set; }
            public int Row { get; set; }
            public int Col { get; set; }
        }
    }
}
