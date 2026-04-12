using System.ComponentModel.DataAnnotations;

namespace STASIS.Models;

public class VisitType
{
    public int VisitTypeID { get; set; }

    [Required]
    [MaxLength(100)]
    public string VisitTypeName { get; set; } = string.Empty;

    public ICollection<Specimen> Specimens { get; set; } = new List<Specimen>();
}
