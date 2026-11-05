using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Samples
{
    public class DetailModel : PageModel
    {
        private readonly ISampleService _sampleService;
        private readonly IStudyAccessService _studyAccessService;

        public DetailModel(ISampleService sampleService, IStudyAccessService studyAccessService)
        {
            _sampleService = sampleService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public Specimen? Specimen { get; set; }
        public List<FilterPaperUsage> FilterPaperUsages { get; set; } = new();

        public bool IsFilterPaper => Specimen?.SampleType?.TypeName?.Equals("Filter Paper", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsPlasma => Specimen?.SampleType?.TypeName?.Equals("Plasma", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsPBMC => Specimen?.SampleType?.TypeName?.Equals("PBMC", StringComparison.OrdinalIgnoreCase) == true;

        public async Task<IActionResult> OnGetAsync()
        {
            Specimen = await _sampleService.GetSpecimenDetailAsync(Id);
            if (Specimen == null)
                return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!await _studyAccessService.CanViewAsync(currentUserId, Specimen.StudyID))
                return NotFound();

            if (IsFilterPaper)
                FilterPaperUsages = await _sampleService.GetFilterPaperUsageAsync(Id);

            return Page();
        }
    }
}
