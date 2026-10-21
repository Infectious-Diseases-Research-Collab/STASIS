using Microsoft.AspNetCore.Identity;

namespace STASIS.Models;

public class UserStudyAccess
{
    public int UserStudyAccessID { get; set; }
    public string AspNetUserId { get; set; } = string.Empty;
    public IdentityUser? User { get; set; }
    public int StudyID { get; set; }
    public Study? Study { get; set; }
    public string Role { get; set; } = string.Empty;
    public string GrantedByUserId { get; set; } = string.Empty;
    public IdentityUser? GrantedByUser { get; set; }
    public DateTime GrantedDate { get; set; }
}
