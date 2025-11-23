namespace STASIS.Models;

public class Specimen
{
    public int SpecimenID { get; set; }
    public string BarcodeID { get; set; } = string.Empty;
    public int? StudyID { get; set; }
    public Study? Study { get; set; }
    public int? SampleTypeID { get; set; }
    public SampleType? SampleType { get; set; }
    public DateTime? CollectionDate { get; set; }
    public int? BoxID { get; set; }
    public Box? Box { get; set; }
    public int? PositionRow { get; set; }
    public int? PositionCol { get; set; }
    public int? RemainingSpots { get; set; }
    public string Status { get; set; } = "In-Stock";
}
