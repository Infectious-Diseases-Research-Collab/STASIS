using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using STASIS.Models;
using STASIS.Services;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace STASIS.Pages
{
    public class SamplesModel : PageModel
    {
        private readonly ISampleService _sampleService;
        private readonly IStudyAccessService _studyAccessService;
        private const int PageSize = 25;

        public SamplesModel(ISampleService sampleService, IStudyAccessService studyAccessService)
        {
            _sampleService = sampleService;
            _studyAccessService = studyAccessService;
        }

        public List<Specimen> Specimens { get; set; } = new List<Specimen>();

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? StudyId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SampleTypeId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ParticipantId { get; set; }

        public SelectList Studies { get; set; } = new SelectList(new List<Study>());
        public SelectList SampleTypes { get; set; } = new SelectList(new List<SampleType>());

        public int PageIndex { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int FirstItemIndex => TotalItems == 0 ? 0 : (PageIndex - 1) * PageSize + 1;
        public int LastItemIndex => TotalItems == 0 ? 0 : FirstItemIndex + Specimens.Count - 1;


        public async Task OnGetAsync(int? pageIndex)
        {
            PageIndex = pageIndex ?? 1;

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var visibleStudyIds = await _studyAccessService.GetVisibleStudyIdsAsync(currentUserId);

            var studies = await _sampleService.GetAllStudies(visibleStudyIds);
            var sampleTypes = await _sampleService.GetAllSampleTypes();

            Studies = new SelectList(studies, nameof(Study.StudyID), nameof(Study.StudyCode), StudyId);
            SampleTypes = new SelectList(sampleTypes, nameof(SampleType.SampleTypeID), nameof(SampleType.TypeName), SampleTypeId);

            var (specimens, totalCount) = await _sampleService.GetSpecimensAsync(SearchString, StudyId, SampleTypeId, ParticipantId, PageIndex, PageSize, visibleStudyIds);

            Specimens = specimens;
            TotalItems = totalCount;
            TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);
        }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }
}
