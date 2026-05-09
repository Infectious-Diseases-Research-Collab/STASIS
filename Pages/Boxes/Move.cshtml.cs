using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Boxes
{
    [Authorize(Roles = "Write,Admin")]
    public class MoveModel : PageModel
    {
        private readonly ISampleService _sampleService;
        private readonly IStorageService _storageService;

        public MoveModel(ISampleService sampleService, IStorageService storageService)
        {
            _sampleService = sampleService;
            _storageService = storageService;
        }

        [BindProperty(SupportsGet = true)]
        public string? Barcode { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SpecimenId { get; set; }

        [BindProperty]
        public int NewBoxId { get; set; }

        [BindProperty]
        public int NewRow { get; set; }

        [BindProperty]
        public int NewCol { get; set; }

        [BindProperty]
        public bool MoveToTemp { get; set; }

        public Specimen? FoundSpecimen { get; set; }
        public List<Specimen> AmbiguousSpecimens { get; set; } = new();
        public SelectList BoxOptions { get; set; } = new SelectList(Enumerable.Empty<object>());

        public async Task OnGetAsync()
        {
            if (SpecimenId.HasValue)
            {
                FoundSpecimen = await _sampleService.GetSpecimenDetailAsync(SpecimenId.Value);
            }
            else if (!string.IsNullOrEmpty(Barcode))
            {
                var matches = await _sampleService.GetSpecimensByBarcode(Barcode);
                if (matches.Count == 1)
                    FoundSpecimen = matches[0];
                else if (matches.Count > 1)
                    AmbiguousSpecimens = matches;
            }
            await LoadBoxOptionsAsync();
        }

        public async Task<IActionResult> OnPostMoveAsync()
        {
            if (SpecimenId == null)
            {
                ModelState.AddModelError(string.Empty, "No specimen selected.");
                await LoadBoxOptionsAsync();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            if (MoveToTemp)
            {
                await _storageService.MoveToTempAsync(SpecimenId.Value, userId);
                TempData["Success"] = "Specimen moved to temporary storage.";
                return RedirectToPage();
            }

            try
            {
                await _storageService.MoveSpecimenAsync(SpecimenId.Value, NewBoxId, NewRow, NewCol, userId);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "That position is already occupied.");
                if (SpecimenId.HasValue)
                    FoundSpecimen = await _sampleService.GetSpecimenDetailAsync(SpecimenId.Value);
                await LoadBoxOptionsAsync();
                return Page();
            }

            TempData["Success"] = "Specimen moved successfully.";
            return RedirectToPage();
        }

        private async Task LoadBoxOptionsAsync()
        {
            var freezers = await _storageService.GetAllFreezers();
            var allBoxes = new List<Box>();
            foreach (var f in freezers)
            {
                var compartments = await _storageService.GetCompartmentsByFreezer(f.FreezerID);
                foreach (var c in compartments)
                {
                    c.Freezer = f;
                    var racks = await _storageService.GetRacksByCompartment(c.CompartmentID);
                    foreach (var r in racks)
                    {
                        r.Compartment = c;
                        var boxes = await _storageService.GetBoxesByRack(r.RackID);
                        foreach (var b in boxes) { b.Rack = r; }
                        allBoxes.AddRange(boxes);
                    }
                }
            }
            BoxOptions = new SelectList(
                allBoxes.OrderBy(b => b.BoxLabel).Select(b => new {
                    b.BoxID,
                    Display = $"{b.BoxLabel} ({b.Rack?.Compartment?.Freezer?.FreezerName} > {b.Rack?.Compartment?.CompartmentName} > {b.Rack?.RackName})"
                }), "BoxID", "Display");
        }
    }
}
