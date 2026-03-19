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
        private readonly IAuditService _auditService;

        public AddModel(ISampleService sampleService, IStorageService storageService, IAuditService auditService)
        {
            _sampleService = sampleService;
            _storageService = storageService;
            _auditService = auditService;
        }

        [BindProperty]
        public SampleInput Input { get; set; } = new();

        public SelectList StudyOptions { get; set; } = new SelectList(Enumerable.Empty<object>());
        public SelectList SampleTypeOptions { get; set; } = new SelectList(Enumerable.Empty<object>());
        public SelectList BoxOptions { get; set; } = new SelectList(Enumerable.Empty<object>());
        public List<SampleType> SampleTypes { get; set; } = new();

        public class SampleInput
        {
            [Required]
            [StringLength(100)]
            [Display(Name = "Barcode ID")]
            public string BarcodeID { get; set; } = string.Empty;

            [StringLength(100)]
            [Display(Name = "Legacy ID")]
            public string? LegacyID { get; set; }

            [Display(Name = "Study")]
            public int? StudyID { get; set; }

            [Display(Name = "Sample Type")]
            public int? SampleTypeID { get; set; }

            [Display(Name = "Collection Date")]
            [DataType(DataType.Date)]
            public DateTime? CollectionDate { get; set; }

            [Display(Name = "Box")]
            public int? BoxID { get; set; }

            [Display(Name = "Row")]
            public int? PositionRow { get; set; }

            [Display(Name = "Column")]
            public int? PositionCol { get; set; }

            [Display(Name = "Aliquot Number")]
            public int? AliquotNumber { get; set; }

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

            // Check barcode uniqueness
            if (await _sampleService.IsBarcodeTaken(Input.BarcodeID))
            {
                ModelState.AddModelError("Input.BarcodeID", "This barcode is already in use.");
                await LoadDropdownsAsync();
                return Page();
            }

            var specimen = new Specimen
            {
                BarcodeID = Input.BarcodeID,
                LegacyID = Input.LegacyID,
                StudyID = Input.StudyID,
                SampleTypeID = Input.SampleTypeID,
                CollectionDate = Input.CollectionDate.HasValue
                    ? DateTime.SpecifyKind(Input.CollectionDate.Value, DateTimeKind.Utc)
                    : null,
                BoxID = Input.BoxID,
                PositionRow = Input.PositionRow,
                PositionCol = Input.PositionCol,
                AliquotNumber = Input.AliquotNumber,
                RemainingSpots = Input.RemainingSpots,
                Status = "In-Stock"
            };

            try
            {
                await _sampleService.AddSpecimen(specimen);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty,
                    "A conflict occurred. The barcode or box position may already be in use.");
                await LoadDropdownsAsync();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            await _auditService.LogChangeAsync("tbl_Specimens", specimen.SpecimenID.ToString(),
                "Created", null, specimen.BarcodeID, userId);

            TempData["Success"] = $"Specimen \"{specimen.BarcodeID}\" added successfully.";
            return RedirectToPage("/Samples");
        }

        public async Task<IActionResult> OnGetCheckBarcodeAsync(string barcode)
        {
            var taken = await _sampleService.IsBarcodeTaken(barcode);
            return new JsonResult(new { taken });
        }

        public async Task<IActionResult> OnGetOccupiedPositionsAsync(int boxId)
        {
            var positions = await _sampleService.GetOccupiedPositions(boxId);
            return new JsonResult(positions.Select(p => new { row = p.Row, col = p.Col }));
        }

        private async Task LoadDropdownsAsync()
        {
            var studies = await _sampleService.GetAllStudies();
            StudyOptions = new SelectList(studies, "StudyID", "StudyCode");

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
                        {
                            box.Rack = rack;
                        }
                        allBoxes.AddRange(boxes);
                    }
                }
            }

            BoxOptions = new SelectList(
                allBoxes.OrderBy(b => b.BoxLabel).Select(b => new
                {
                    b.BoxID,
                    Display = $"{b.BoxLabel} ({b.Rack?.Compartment?.Freezer?.FreezerName} > {b.Rack?.Compartment?.CompartmentName} > {b.Rack?.RackName})",
                    b.BoxType
                }),
                "BoxID", "Display");
        }
    }
}
