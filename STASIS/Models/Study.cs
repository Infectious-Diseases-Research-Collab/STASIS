namespace STASIS.Models;

public class Study
{
    public int StudyID { get; set; }
    public string StudyCode { get; set; } = string.Empty;
    public string? PrincipalInvestigator { get; set; }
}
