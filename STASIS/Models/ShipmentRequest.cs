using System.ComponentModel.DataAnnotations;

namespace STASIS.Models;

public class ShipmentRequest
{
    [Key]
    public int RequestID { get; set; }
    public string RequestedBarcode { get; set; } = string.Empty;
    public string? RequestorName { get; set; }
    public DateTime? RequestDate { get; set; }
    public string Status { get; set; } = "Pending";
}
