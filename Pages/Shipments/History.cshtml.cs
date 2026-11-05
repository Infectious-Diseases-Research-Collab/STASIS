using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Shipments
{
    // Study-scoped like the rest of the app. A batch's studies are the studies of its matched
    // specimens (IShipmentService.GetBatchStudyIdsAsync):
    // - View a batch or a manifest: view access to every study in the batch. A batch with no
    //   matched specimens is visible only to whoever imported it (and System Admins).
    // - Record an approval: view access plus CanApproveShipmentsAsync (flag or System Admin).
    // - Ship: Editor on every study in the batch (ShipmentService also requires it be Approved).
    // A batch the caller can't view returns 404, like Samples/Detail, so its existence isn't
    // revealed.
    public class HistoryModel : PageModel
    {
        private readonly IShipmentService _shipmentService;
        private readonly IStudyAccessService _studyAccessService;

        public HistoryModel(IShipmentService shipmentService, IStudyAccessService studyAccessService)
        {
            _shipmentService = shipmentService;
            _studyAccessService = studyAccessService;
        }

        public List<ShipmentBatch> Batches { get; set; } = new();

        // Detail view
        [BindProperty(SupportsGet = true)]
        public int? BatchId { get; set; }
        public ShipmentBatch? SelectedBatch { get; set; }

        // Drive whether the view shows the approval / ship forms for SelectedBatch.
        public bool CanApproveSelected { get; set; }
        public bool CanShipSelected { get; set; }

        // Approval form
        [BindProperty]
        public string? ApprovalLevel { get; set; }
        [BindProperty]
        public string? ApprovalStatus { get; set; }
        [BindProperty]
        public string? ApprovalComments { get; set; }

        // Ship form
        [BindProperty]
        public string? Courier { get; set; }
        [BindProperty]
        public string? TrackingNumber { get; set; }
        [BindProperty]
        public string? Destination { get; set; }
        [BindProperty]
        public bool IsInternational { get; set; }
        [BindProperty]
        public int FilterPaperSpots { get; set; } = 1;

        public List<string> ShipmentErrors { get; set; } = new();

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            if (BatchId.HasValue && !await CanViewBatchAsync(BatchId.Value))
            {
                return NotFound();
            }

            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync()
        {
            if (BatchId == null || string.IsNullOrEmpty(ApprovalLevel) || string.IsNullOrEmpty(ApprovalStatus))
                return RedirectToPage();

            if (!await CanViewBatchAsync(BatchId.Value))
                return NotFound();

            var userId = CurrentUserId;
            if (!await _studyAccessService.CanApproveShipmentsAsync(userId))
            {
                TempData["Error"] = "You don't have permission to approve shipments. Ask a System Admin to turn on \"Can Approve Shipments\" for your account.";
                return RedirectToPage(new { BatchId });
            }

            await _shipmentService.ApproveBatchAsync(BatchId.Value, userId,
                ApprovalLevel, ApprovalStatus, ApprovalComments);

            TempData["Success"] = $"{ApprovalLevel} approval recorded as {ApprovalStatus}.";
            return RedirectToPage(new { BatchId });
        }

        public async Task<IActionResult> OnPostShipAsync()
        {
            if (BatchId == null) return RedirectToPage();

            if (!await CanViewBatchAsync(BatchId.Value))
                return NotFound();

            var userId = CurrentUserId;
            if (!await CanEditAllBatchStudiesAsync(BatchId.Value))
            {
                TempData["Error"] = "You need Editor access to every study in this batch to ship it.";
                return RedirectToPage(new { BatchId });
            }

            try
            {
                await _shipmentService.ShipBatchAsync(BatchId.Value,
                    Courier ?? "", TrackingNumber, Destination, userId,
                    IsInternational, FilterPaperSpots);

                TempData["Success"] = "Shipment recorded. Specimens marked as Shipped.";
                return RedirectToPage(new { BatchId });
            }
            catch (InvalidOperationException ex)
            {
                // Validation errors from special sample rules
                ShipmentErrors = ex.Message.Split(" | ").ToList();
                await LoadAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnGetManifestAsync(int shipmentId)
        {
            var shipment = await _shipmentService.GetShipmentByIdAsync(shipmentId);
            if (shipment == null) return NotFound();

            // A shipment always belongs to a batch in practice; without one, only a System Admin
            // can tell whose data it is.
            var allowed = shipment.BatchID.HasValue
                ? await CanViewBatchAsync(shipment.BatchID.Value)
                : await _studyAccessService.IsSystemAdminAsync(CurrentUserId);
            if (!allowed) return NotFound();

            var sb = new StringBuilder();
            sb.AppendLine("Barcode,SampleType,Condition,BoxPosition,SpotsUsed");
            foreach (var sc in shipment.ShipmentContents)
            {
                sb.AppendLine($"\"{sc.Specimen?.BarcodeID}\",\"{sc.Specimen?.SampleType?.TypeName}\",\"{sc.ConditionAtShipment}\",\"{sc.ShippingBoxPosition}\",{sc.SpotsUsed}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
                $"manifest-shipment-{shipmentId}.csv");
        }

        private async Task LoadAsync()
        {
            var userId = CurrentUserId;
            var visibleStudyIds = await _studyAccessService.GetVisibleStudyIdsAsync(userId);
            Batches = await _shipmentService.GetAllBatchesAsync(visibleStudyIds, userId);

            if (BatchId.HasValue)
            {
                SelectedBatch = await _shipmentService.GetBatchByIdAsync(BatchId.Value);
                if (SelectedBatch != null)
                {
                    CanApproveSelected = await _studyAccessService.CanApproveShipmentsAsync(userId);
                    CanShipSelected = await CanEditAllBatchStudiesAsync(BatchId.Value);
                }
            }
        }

        private async Task<bool> CanViewBatchAsync(int batchId)
        {
            var userId = CurrentUserId;
            if (await _studyAccessService.IsSystemAdminAsync(userId))
                return true;

            var studyIds = await _shipmentService.GetBatchStudyIdsAsync(batchId);
            if (studyIds.Count == 0)
            {
                var batch = await _shipmentService.GetBatchByIdAsync(batchId);
                return batch != null && batch.ImportedByUserId == userId;
            }

            foreach (var studyId in studyIds)
            {
                if (!await _studyAccessService.CanViewAsync(userId, studyId))
                    return false;
            }
            return true;
        }

        private async Task<bool> CanEditAllBatchStudiesAsync(int batchId)
        {
            var userId = CurrentUserId;
            if (await _studyAccessService.IsSystemAdminAsync(userId))
                return true;

            var studyIds = await _shipmentService.GetBatchStudyIdsAsync(batchId);
            if (studyIds.Count == 0)
                return false; // nothing matched, so nothing to ship

            foreach (var studyId in studyIds)
            {
                if (!await _studyAccessService.CanEditAsync(userId, studyId))
                    return false;
            }
            return true;
        }
    }
}
