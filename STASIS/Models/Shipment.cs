namespace STASIS.Models;

public class Shipment
{
    public int ShipmentID { get; set; }
    public DateTime ShipmentDate { get; set; }
    public string? Courier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? DestinationAddress { get; set; }
}
