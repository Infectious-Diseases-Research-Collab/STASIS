using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Samples
{
    public class AddModel : PageModel
    {
        private readonly ISampleService _sampleService;
        private readonly IStorageService _storageService;
        private readonly IStudyAccessService _studyAccessService;

        public AddModel(ISampleService sampleService, IStorageService storageService, IStudyAccessService studyAccessService)
        {
            _sampleService = sampleService;
            _storageService = storageService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty]
        [Required]
        [StringLength(100)]
        public string ParticipantID { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Study is required.")]
        public int? StudyID { get; set; }

        [BindProperty]
        public int? VisitTypeID { get; set; }

        [BindProperty]
        [DataType(DataType.Date)]
        public DateTime? CollectionDate { get; set; }

        [BindProperty]
        public List<SampleInput> Samples { get; set; } = new();

        public SelectList StudyOptions { get; set; } = new SelectList(Enumerable.Empty<object>());
        public SelectList VisitTypeOptions { get; set; } = new SelectList(Enumerable.Empty<object>());
        public SelectList SampleTypeOptions { get; set; } = new SelectList(Enumerable.Empty<object>());
        public List<BoxOption> AllBoxes { get; set; } = new();
        public record BoxOption(int BoxID, string Display, string BoxType);
        public List<SampleType> SampleTypes { get; set; } = new();
        public class SampleInput
        {
            [Display(Name = "Barcode ID")]
            public string? BarcodeID { get; set; }

            [Display(Name = "Sample Type")]
            [Required]
            public int? SampleTypeID { get; set; }

            [Display(Name = "Box")]
            public int? BoxID { get; set; }

            [Display(Name = "Row")]
            public int? PositionRow { get; set; }

            [Display(Name = "Column")]
            public int? PositionCol { get; set; }

            [Display(Name = "Aliquot Number")]
            public int? AliquotNumber { get; set; }

            [Display(Name = "Cell Count")]
            public int? CellCount { get; set; }

            [Display(Name = "Remaining Spots")]
            public int? RemainingSpots { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            // No studies to add to at all -- distinct from "some studies, but only as a Viewer",
            // which still gets the page (just an empty Study dropdown filtered down below).
            var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(userId);
            if (!isSystemAdmin)
            {
                var visibleStudyIds = await _studyAccessService.GetVisibleStudyIdsAsync(userId);
                if (visibleStudyIds != null && visibleStudyIds.Count == 0)
                {
                    return Forbid();
                }
            }

            await LoadDropdownsAsync(userId, isSystemAdmin, StudyID);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(userId);

            await LoadDropdownsAsync(userId, isSystemAdmin, StudyID);

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Re-validate the posted StudyID server-side -- the Study dropdown only offers
            // editable studies, but a caller can post any StudyID directly, so don't trust it.
            if (!await _studyAccessService.CanEditAsync(userId, StudyID!.Value))
            {
                ModelState.AddModelError(string.Empty, "You do not have permission to add samples to this study.");
                return Page();
            }

            // Filter out blank cards (no barcode) and normalize to uppercase
            var activeSamples = Samples
                .Where(s => !string.IsNullOrWhiteSpace(s.BarcodeID))
                .ToList();

            foreach (var s in activeSamples)
                s.BarcodeID = s.BarcodeID!.Trim().ToUpperInvariant();

            if (!activeSamples.Any())
            {
                ModelState.AddModelError(string.Empty, "Enter at least one barcode.");
                return Page();
            }

            // Validate all active samples have a SampleTypeID
            for (int i = 0; i < activeSamples.Count; i++)
            {
                if (!activeSamples[i].SampleTypeID.HasValue)
                {
                    ModelState.AddModelError(string.Empty, $"Sample {i + 1}: Sample Type is required.");
                    return Page();
                }
            }

            // Validate all barcodes are unique within the batch
            var duplicatesInBatch = activeSamples
                .GroupBy(s => s.BarcodeID!)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatesInBatch.Any())
            {
                ModelState.AddModelError(string.Empty,
                    $"Duplicate barcodes in batch: {string.Join(", ", duplicatesInBatch)}");
                return Page();
            }

            // Validate all barcodes are unique in the database (scoped to this study)
            var takenBarcodes = await _sampleService.GetTakenBarcodesAsync(
                activeSamples.Select(s => s.BarcodeID!), StudyID!.Value);

            if (takenBarcodes.Any())
            {
                ModelState.AddModelError(string.Empty,
                    $"Barcodes already in use: {string.Join(", ", takenBarcodes)}");
                return Page();
            }

            // Validate no two cards claim the same box position
            var positionConflicts = activeSamples
                .Where(s => s.BoxID.HasValue && s.PositionRow.HasValue)
                .GroupBy(s => (s.BoxID, s.PositionRow, s.PositionCol))
                .Where(g => g.Count() > 1)
                .Select(g => $"Box {g.Key.BoxID} Row {g.Key.PositionRow} Col {g.Key.PositionCol}")
                .ToList();

            if (positionConflicts.Any())
            {
                ModelState.AddModelError(string.Empty,
                    $"Duplicate box positions in batch: {string.Join(", ", positionConflicts)}");
                return Page();
            }

            // Validate Filter Paper samples go only into Filter Paper Binder boxes
            var boxTypeErrors = new List<string>();
            foreach (var s in activeSamples.Where(s => s.BoxID.HasValue && s.SampleTypeID.HasValue))
            {
                var sampleType = SampleTypes.FirstOrDefault(t => t.SampleTypeID == s.SampleTypeID);
                var box = AllBoxes.FirstOrDefault(b => b.BoxID == s.BoxID);
                if (sampleType == null || box == null) continue;

                bool isFP = sampleType.TypeName.Contains("filter paper", StringComparison.OrdinalIgnoreCase);
                bool isFPBox = box.BoxType == "Filter Paper Binder";

                if (isFP && !isFPBox)
                    boxTypeErrors.Add($"{s.BarcodeID}: Filter Paper samples must go in a Filter Paper Binder box.");
                else if (!isFP && isFPBox)
                    boxTypeErrors.Add($"{s.BarcodeID}: Non-Filter Paper samples cannot go in a Filter Paper Binder box.");
            }

            if (boxTypeErrors.Any())
            {
                foreach (var err in boxTypeErrors)
                    ModelState.AddModelError(string.Empty, err);
                return Page();
            }

            var specimens = activeSamples.Select(s => new Specimen
            {
                BarcodeID = s.BarcodeID!,
                ParticipantID = ParticipantID,
                StudyID = StudyID!.Value,
                VisitTypeID = VisitTypeID,
                CollectionDate = CollectionDate.HasValue
                    ? DateTime.SpecifyKind(CollectionDate.Value, DateTimeKind.Utc)
                    : null,
                SampleTypeID = s.SampleTypeID,
                BoxID = s.BoxID,
                PositionRow = s.PositionRow,
                PositionCol = s.PositionCol,
                AliquotNumber = s.AliquotNumber,
                CellCount = s.CellCount,
                RemainingSpots = s.RemainingSpots,
                Status = "In-Stock"
            }).ToList();

            try
            {
                await _sampleService.AddSpecimensBatch(specimens, userId);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty,
                    "A conflict occurred (duplicate barcode or box position already taken). No samples were saved.");
                return Page();
            }
            catch (InvalidOperationException ex)
            {
                // Thrown by IStorageService.ValidateAndAssignBoxStudyAsync (via AddSpecimensBatch)
                // when a targeted box already holds a different study's specimens. The batch's
                // own transaction has already rolled back -- no samples were saved.
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }

            TempData["BatchSuccess"] = true;
            TempData["BatchCount"] = specimens.Count;
            TempData["BatchParticipant"] = ParticipantID;
            TempData["CreatedBarcodes"] = string.Join(",", specimens.Select(s => s.BarcodeID));

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetCheckBarcodeAsync(string barcode, int? studyId)
        {
            if (string.IsNullOrWhiteSpace(barcode) || barcode.Length > 100 || studyId == null)
                return new JsonResult(new { taken = false });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            // This handler carried no access check at all and is reachable directly by URL with
            // an arbitrary studyId, regardless of the caller's actual access to that study. Gate
            // it the same way OnPostAsync re-validates a posted StudyID -- don't trust the
            // caller, and don't leak which barcodes exist in a study they can't edit. On failure,
            // return the same shape the existing "invalid input" branch above already returns.
            if (!await _studyAccessService.CanEditAsync(userId, studyId.Value))
                return new JsonResult(new { taken = false });

            var taken = await _sampleService.IsBarcodeTaken(barcode, studyId.Value);
            return new JsonResult(new { taken });
        }

        public async Task<IActionResult> OnGetBoxesForStudyAsync(int studyId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            // Don't trust the posted studyId -- re-check CanEditAsync the same way every other
            // handler on this page does, and return an empty list rather than leak box
            // labels/locations for a study the caller can't edit.
            if (!await _studyAccessService.CanEditAsync(userId, studyId))
                return new JsonResult(Array.Empty<object>());

            var boxes = await _storageService.GetAllBoxesAsync(new[] { studyId });
            var result = boxes.Select(b => new
            {
                id = b.BoxID,
                display = b.Rack != null
                    ? $"{b.BoxLabel} ({b.Rack.Compartment?.Freezer?.FreezerName} › {b.Rack.Compartment?.CompartmentName} › {b.Rack.RackName})"
                    : $"{b.BoxLabel} (unassigned)",
                boxType = b.BoxType
            });
            return new JsonResult(result);
        }

        public async Task<IActionResult> OnGetNextPositionAsync(int boxId, string? claimed = null)
        {
            if (boxId <= 0) return BadRequest();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            if (!await CanAccessBoxForAddAsync(userId, boxId))
                return BadRequest();

            var claimedPositions = new List<(int Row, int? Col)>();
            if (!string.IsNullOrEmpty(claimed))
            {
                foreach (var item in claimed.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = item.Split(':');
                    if (parts.Length >= 1 && int.TryParse(parts[0], out var row))
                    {
                        int? col = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : null;
                        claimedPositions.Add((row, col));
                    }
                }
            }

            var pos = await _sampleService.GetNextAvailablePosition(boxId, claimedPositions);
            if (pos == null)
                return new JsonResult(new { full = true });
            return new JsonResult(new { row = pos.Value.Row, col = pos.Value.Col });
        }

        public async Task<IActionResult> OnGetOccupiedPositionsAsync(int boxId)
        {
            if (boxId <= 0) return BadRequest();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            if (!await CanAccessBoxForAddAsync(userId, boxId))
                return new JsonResult(Array.Empty<object>());

            var positions = await _sampleService.GetOccupiedPositions(boxId);
            return new JsonResult(positions.Select(p => new { row = p.Row, col = p.Col }));
        }

        // Shared gate for the boxId-based AJAX helpers above (OnGetNextPositionAsync,
        // OnGetOccupiedPositionsAsync): neither takes a studyId directly, so the box's own
        // StudyID has to be resolved first. GetBoxWithSpecimens(boxId, null) is called with
        // visibleStudyIds == null (unrestricted) deliberately -- this is an authorization
        // lookup, not a display query, so it needs the box's real StudyID regardless of the
        // caller's own visibility, to decide whether CanEditAsync should even apply. A box
        // already claimed by a study requires CanEditAsync on that study; a box that doesn't
        // exist, or is genuinely unassigned (StudyID == null), falls back to "is this caller an
        // Editor on at least one study anywhere" -- same philosophy as Boxes/Create's
        // IsEditorAnywhereAsync (anyone with Editor access anywhere may claim an unassigned box).
        private async Task<bool> CanAccessBoxForAddAsync(string userId, int boxId)
        {
            var box = await _storageService.GetBoxWithSpecimens(boxId, null);
            if (box?.StudyID is int studyId)
                return await _studyAccessService.CanEditAsync(userId, studyId);

            return await IsEditorAnywhereAsync(userId);
        }

        // Mirrors Pages/Boxes/Create.cshtml.cs's IsEditorAnywhereAsync exactly: there's no bulk
        // "editable study ids" method on IStudyAccessService, so this resolves the caller's own
        // visible studies first (bounded by their own small assigned-study count) and then checks
        // CanEditAsync per study, short-circuiting on the first match.
        private async Task<bool> IsEditorAnywhereAsync(string userId)
        {
            if (await _studyAccessService.IsSystemAdminAsync(userId))
            {
                return true;
            }

            var visibleStudyIds = await _studyAccessService.GetVisibleStudyIdsAsync(userId);
            var candidateStudies = await _sampleService.GetAllStudies(visibleStudyIds);

            foreach (var study in candidateStudies)
            {
                if (await _studyAccessService.CanEditAsync(userId, study.StudyID))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task LoadDropdownsAsync(string userId, bool isSystemAdmin, int? selectedStudyId)
        {
            // Study dropdown: restricted to studies this caller can actually Edit (Editor,
            // StudyAdmin, or System Admin) -- not merely View -- since this page only ever
            // creates new specimens. There's no bulk "editable study ids" method on
            // IStudyAccessService, so this resolves the caller's visible studies first (narrowing
            // the DB query / per-study checks to just those) and then filters with CanEditAsync
            // per study. The Visit Type dropdown remains unfiltered by study -- VisitType has no
            // StudyID column yet, so that half of STASIS_Deferred_Items.md item 9 is still
            // deferred (the Box half is resolved below).
            var visibleStudyIds = isSystemAdmin ? null : await _studyAccessService.GetVisibleStudyIdsAsync(userId);
            var candidateStudies = await _sampleService.GetAllStudies(visibleStudyIds);

            var editableStudies = new List<Study>();
            foreach (var study in candidateStudies)
            {
                if (isSystemAdmin || await _studyAccessService.CanEditAsync(userId, study.StudyID))
                {
                    editableStudies.Add(study);
                }
            }
            StudyOptions = new SelectList(editableStudies, "StudyID", "StudyCode");

            var visitTypes = await _sampleService.GetAllVisitTypes();
            VisitTypeOptions = new SelectList(visitTypes, "VisitTypeID", "VisitTypeName");

            SampleTypes = await _sampleService.GetAllSampleTypes();
            SampleTypeOptions = new SelectList(SampleTypes, "SampleTypeID", "TypeName");

            // Box dropdown is scoped to the currently selected study (or nothing, until a study
            // is chosen) -- resolves STASIS_Deferred_Items.md item 9 for boxes. Reuses
            // GetAllBoxesAsync's existing visibility filter: passing the one selected study as a
            // single-element set returns boxes already claimed by that study plus genuinely
            // unassigned ones, exactly the "relevant to this study" set we want here. An empty
            // (non-null) set when no study is selected yet correctly returns nothing, per that
            // filter's own "zero access = zero boxes" semantics.
            var boxStudyFilter = selectedStudyId.HasValue
                ? new[] { selectedStudyId.Value }
                : Array.Empty<int>();
            var allBoxes = await _storageService.GetAllBoxesAsync(boxStudyFilter);
            AllBoxes = allBoxes.Select(b => new BoxOption(
                b.BoxID,
                b.Rack != null
                    ? $"{b.BoxLabel} ({b.Rack.Compartment?.Freezer?.FreezerName} \u203a {b.Rack.Compartment?.CompartmentName} \u203a {b.Rack.RackName})"
                    : $"{b.BoxLabel} (unassigned)",
                b.BoxType
            )).ToList();
        }
    }
}
