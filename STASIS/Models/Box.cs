using System.Collections.Generic;

namespace STASIS.Models;

public class Box
{
    public int BoxID { get; set; }
    public string BoxLabel { get; set; } = string.Empty;
    public string BoxType { get; set; } = string.Empty;
    public int? RackID { get; set; }
    public Rack? Rack { get; set; }
    public virtual ICollection<Specimen> Specimens { get; set; } = new List<Specimen>();
}
