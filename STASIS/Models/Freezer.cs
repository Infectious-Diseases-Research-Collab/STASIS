namespace STASIS.Models;

public class Freezer
{
    public int FreezerID { get; set; }
    public string FreezerName { get; set; } = string.Empty;
    public int? Temperature { get; set; }
    public string? LocationInBuilding { get; set; }
}
