namespace STASIS.Models;

public class Rack
{
    public int RackID { get; set; }
    public string RackName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CompartmentID { get; set; }
    public Compartment? Compartment { get; set; }
    public ICollection<Box> Boxes { get; set; } = new List<Box>();
}
