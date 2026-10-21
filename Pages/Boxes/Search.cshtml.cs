using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Boxes
{
    public class SearchModel : PageModel
    {
        private readonly IStorageService _storageService;
        private readonly IStudyAccessService _studyAccessService;

        public SearchModel(IStorageService storageService, IStudyAccessService studyAccessService)
        {
            _storageService = storageService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty(SupportsGet = true)]
        public string? BoxLabel { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FreezerId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? RackId { get; set; }

        public List<Box> Boxes { get; set; } = new();
        public List<string> AllBoxLabels { get; set; } = new();
        public SelectList FreezerOptions { get; set; } = new SelectList(Enumerable.Empty<object>());

        // Selected box for detail view
        [BindProperty(SupportsGet = true)]
        public int? SelectedBoxId { get; set; }
        public Box? SelectedBox { get; set; }

        public async Task OnGetAsync()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var visibleStudyIds = await _studyAccessService.GetVisibleStudyIdsAsync(currentUserId);

            var freezers = await _storageService.GetAllFreezers();
            FreezerOptions = new SelectList(freezers, "FreezerID", "FreezerName");
            AllBoxLabels = await _storageService.GetAllBoxLabelsAsync(visibleStudyIds);

            bool hasFilter = !string.IsNullOrEmpty(BoxLabel) || FreezerId.HasValue || RackId.HasValue;
            if (hasFilter)
            {
                Boxes = await _storageService.SearchBoxesAsync(BoxLabel, FreezerId, RackId, visibleStudyIds);

                // Auto-select when navigating from a link with an exact label match
                if (!SelectedBoxId.HasValue && Boxes.Count == 1)
                {
                    SelectedBoxId = Boxes[0].BoxID;
                }
            }

            if (SelectedBoxId.HasValue)
            {
                SelectedBox = await _storageService.GetBoxWithSpecimens(SelectedBoxId.Value, visibleStudyIds);
            }
        }

        public int GetGridSize(Box box)
        {
            return box.BoxType switch
            {
                "100-slot" => 10,
                "81-slot" => 9,
                _ => 9
            };
        }
    }
}
