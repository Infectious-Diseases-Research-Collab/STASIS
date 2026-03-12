using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Administration
{
    [Authorize(Roles = "Admin")]
    public class ApprovalsModel : PageModel
    {
        private readonly StasisDbContext _context;
        private readonly ISampleService _sampleService;
        private readonly IShipmentService _shipmentService;

        public ApprovalsModel(StasisDbContext context, ISampleService sampleService, IShipmentService shipmentService)
        {
            _context = context;
            _sampleService = sampleService;
            _shipmentService = shipmentService;
        }

        [BindProperty(SupportsGet = true)]
        public string? TypeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public List<ApprovalViewModel> Approvals { get; set; } = new();

        // Detail view
        [BindProperty(SupportsGet = true)]
        public int? ApprovalId { get; set; }
        public Approval? SelectedApproval { get; set; }

        // Approval form
        [BindProperty]
        public string? ApprovalLevel { get; set; }
        [BindProperty]
        public string? ApprovalStatus { get; set; }
        [BindProperty]
        public string? ApprovalComments { get; set; }

        public async Task OnGetAsync()
        {
            var query = _context.Approvals
                .Include(a => a.RequestedByUser)
                .Include(a => a.DiscardedSpecimens)
                .Include(a => a.ShipmentBatches)
                .AsQueryable();

            if (!string.IsNullOrEmpty(TypeFilter))
                query = query.Where(a => a.ApprovalType == TypeFilter);

            if (!string.IsNullOrEmpty(StatusFilter))
                query = query.Where(a => a.OverallStatus == StatusFilter);
            else
                query = query.Where(a => a.OverallStatus == "Pending");

            Approvals = await query
                .OrderByDescending(a => a.RequestedDate)
                .Select(a => new ApprovalViewModel
                {
                    ApprovalID = a.ApprovalID,
                    ApprovalType = a.ApprovalType,
                    RequestedDate = a.RequestedDate,
                    RequestedByEmail = a.RequestedByUser != null ? a.RequestedByUser.Email : "-",
                    OverallStatus = a.OverallStatus,
                    EDStatus = a.EDApprovalStatus,
                    RegulatoryStatus = a.RegulatoryApprovalStatus,
                    PIStatus = a.PIApprovalStatus,
                    ItemCount = a.ApprovalType == "Discard"
                        ? a.DiscardedSpecimens.Count
                        : a.ShipmentBatches.Count
                })
                .ToListAsync();

            if (ApprovalId.HasValue)
            {
                SelectedApproval = await _context.Approvals
                    .Include(a => a.RequestedByUser)
                    .Include(a => a.EDApproverUser)
                    .Include(a => a.RegulatoryApproverUser)
                    .Include(a => a.PIApproverUser)
                    .Include(a => a.DiscardedSpecimens)
                        .ThenInclude(s => s.SampleType)
                    .Include(a => a.DiscardedSpecimens)
                        .ThenInclude(s => s.Study)
                    .Include(a => a.DiscardedSpecimens)
                        .ThenInclude(s => s.Box)
                    .Include(a => a.ShipmentBatches)
                    .FirstOrDefaultAsync(a => a.ApprovalID == ApprovalId.Value);
            }
        }

        public async Task<IActionResult> OnPostApproveAsync()
        {
            if (ApprovalId == null || string.IsNullOrEmpty(ApprovalLevel) || string.IsNullOrEmpty(ApprovalStatus))
                return RedirectToPage();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var approval = await _context.Approvals.FindAsync(ApprovalId.Value);
            if (approval == null) return RedirectToPage();

            if (approval.ApprovalType == "Discard")
            {
                await _sampleService.ApproveDiscardAsync(ApprovalId.Value, userId,
                    ApprovalLevel, ApprovalStatus, ApprovalComments);
            }
            else if (approval.ApprovalType == "Shipment")
            {
                // Find the batch linked to this approval
                var batch = await _context.ShipmentBatches
                    .FirstOrDefaultAsync(b => b.ApprovalID == ApprovalId.Value);
                if (batch != null)
                {
                    await _shipmentService.ApproveBatchAsync(batch.BatchID, userId,
                        ApprovalLevel, ApprovalStatus, ApprovalComments);
                }
            }

            TempData["Success"] = $"{ApprovalLevel} approval recorded as {ApprovalStatus}.";
            return RedirectToPage(new { ApprovalId, StatusFilter });
        }

        public async Task<IActionResult> OnPostExecuteDiscardAsync()
        {
            if (ApprovalId == null) return RedirectToPage();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            await _sampleService.ExecuteDiscardAsync(ApprovalId.Value, userId);

            TempData["Success"] = "Discard executed. Specimens marked as Discarded.";
            return RedirectToPage(new { ApprovalId, StatusFilter = "Approved" });
        }
    }

    public class ApprovalViewModel
    {
        public int ApprovalID { get; set; }
        public string ApprovalType { get; set; } = "";
        public DateTime RequestedDate { get; set; }
        public string? RequestedByEmail { get; set; }
        public string OverallStatus { get; set; } = "";
        public string? EDStatus { get; set; }
        public string? RegulatoryStatus { get; set; }
        public string? PIStatus { get; set; }
        public int ItemCount { get; set; }
    }
}
