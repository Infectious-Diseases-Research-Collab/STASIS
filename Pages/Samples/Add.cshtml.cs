using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Samples
{
    [Authorize(Roles = "Write,Admin")]
    public class AddModel : PageModel
    {
        private readonly ISampleService _sampleService;
        private readonly IStorageService _storageService;

        public AddModel(ISampleService sampleService, IStorageService storageService)
        {
            _sampleService = sampleService;
            _storageService = storageService;
        }

        [BindProperty]
        [Required]
        [StringLength(100)]
        public string ParticipantID { get; set; } = string.Empty;

        [BindProperty]
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
        public SelectList BoxOptions { get; set; } = new SelectList(Enumerable.Empty<object>());
        public List<SampleType> SampleTypes { get; set; } = new();
        public class SampleInput
        {
            [Display(Name = "Barcode ID")]
            public string? BarcodeID { get; set; }

            [Display(Name = "Sample Type")]
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

        public async Task OnGetAsync()
        {
            await LoadDropdownsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return Page();
            }

            // Filter out blank cards (no barcode)
            var activeSamples = Samples
                .Where(s => !string.IsNullOrWhiteSpace(s.BarcodeID))
                .ToList();

            if (!activeSamples.Any())
            {
                ModelState.AddModelError(string.Empty, "Enter at least one barcode.");
                await LoadDropdownsAsync();
                return Page();
            }

            // Validate all barcodes are unique within the batch
            var duplicatesInBatch = activeSamples
                .GroupBy(s => s.BarcodeID!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatesInBatch.Any())
            {
                ModelState.AddModelError(string.Empty,
                    $"Duplicate barcodes in batch: {string.Join(", ", duplicatesInBatch)}");
                await LoadDropdownsAsync();
                return Page();
            }

            // Validate all barcodes are unique in the database
            var takenBarcodes = new List<string>();
            foreach (var s in activeSamples)
            {
                if (await _sampleService.IsBarcodeTaken(s.BarcodeID!.Trim()))
                    takenBarcodes.Add(s.BarcodeID!.Trim());
            }

            if (takenBarcodes.Any())
            {
                ModelState.AddModelError(string.Empty,
                    $"Barcodes already in use: {string.Join(", ", takenBarcodes)}");
                await LoadDropdownsAsync();
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
                await LoadDropdownsAsync();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var specimens = activeSamples.Select(s => new Specimen
            {
                BarcodeID = s.BarcodeID!.Trim(),
                ParticipantID = ParticipantID,
                StudyID = StudyID,
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
                await LoadDropdownsAsync();
                return Page();
            }

            TempData["BatchSuccess"] = true;
            TempData["BatchCount"] = specimens.Count;
            TempData["BatchParticipant"] = ParticipantID;
            TempData["CreatedBarcodes"] = string.Join(",", specimens.Select(s => s.BarcodeID));

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetCheckBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode) || barcode.Length > 100)
                return new JsonResult(new { taken = false });
            var taken = await _sampleService.IsBarcodeTaken(barcode);
            return new JsonResult(new { taken });
        }

        public async Task<IActionResult> OnGetNextPositionAsync(int boxId)
        {
            if (boxId <= 0) return BadRequest();
            var pos = await _sampleService.GetNextAvailablePosition(boxId);
            if (pos == null)
                return new JsonResult(new { full = true });
            return new JsonResult(new { row = pos.Value.Row, col = pos.Value.Col });
        }

        public async Task<IActionResult> OnGetOccupiedPositionsAsync(int boxId)
        {
            if (boxId <= 0) return BadRequest();
            var positions = await _sampleService.GetOccupiedPositions(boxId);
            return new JsonResult(positions.Select(p => new { row = p.Row, col = p.Col }));
        }

        private async Task LoadDropdownsAsync()
        {
            var studies = await _sampleService.GetAllStudies();
            StudyOptions = new SelectList(studies, "StudyID", "StudyCode");

            var visitTypes = await _sampleService.GetAllVisitTypes();
            VisitTypeOptions = new SelectList(visitTypes, "VisitTypeID", "VisitTypeName");

            SampleTypes = await _sampleService.GetAllSampleTypes();
            SampleTypeOptions = new SelectList(SampleTypes, "SampleTypeID", "TypeName");

            var freezers = await _storageService.GetAllFreezers();
            var allBoxes = new List<Box>();
            foreach (var freezer in freezers)
            {
                var compartments = await _storageService.GetCompartmentsByFreezer(freezer.FreezerID);
                foreach (var compartment in compartments)
                {
                    compartment.Freezer = freezer;
                    var racks = await _storageService.GetRacksByCompartment(compartment.CompartmentID);
                    foreach (var rack in racks)
                    {
                        rack.Compartment = compartment;
                        var boxes = await _storageService.GetBoxesByRack(rack.RackID);
                        foreach (var box in boxes)
                            box.Rack = rack;
                        allBoxes.AddRange(boxes);
                    }
                }
            }

            BoxOptions = new SelectList(
                allBoxes.OrderBy(b => b.BoxLabel).Select(b => new
                {
                    b.BoxID,
                    Display = $"{b.BoxLabel} ({b.Rack?.Compartment?.Freezer?.FreezerName} \u203a {b.Rack?.Compartment?.CompartmentName} \u203a {b.Rack?.RackName})"
                }),
                "BoxID", "Display");
        }
    }
}
