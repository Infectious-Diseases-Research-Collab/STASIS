namespace STASIS.Models;

public class Compartment
{
    public int CompartmentID { get; set; }
    public string CompartmentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int FreezerID { get; set; }
    public Freezer? Freezer { get; set; }
    public ICollection<Rack> Racks { get; set; } = new List<Rack>();
}
