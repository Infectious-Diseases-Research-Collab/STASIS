using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Samples
{
    public class DetailModel : PageModel
    {
        private readonly ISampleService _sampleService;

        public DetailModel(ISampleService sampleService)
        {
            _sampleService = sampleService;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public Specimen? Specimen { get; set; }
        public List<FilterPaperUsage> FilterPaperUsages { get; set; } = new();

        public bool IsFilterPaper => Specimen?.SampleType?.TypeName?.Equals("Filter Paper", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsPlasma => Specimen?.SampleType?.TypeName?.Equals("Plasma", StringComparison.OrdinalIgnoreCase) == true;

        public async Task<IActionResult> OnGetAsync()
        {
            Specimen = await _sampleService.GetSpecimenDetailAsync(Id);
            if (Specimen == null)
                return NotFound();

            if (IsFilterPaper)
                FilterPaperUsages = await _sampleService.GetFilterPaperUsageAsync(Id);

            return Page();
        }
    }
}
