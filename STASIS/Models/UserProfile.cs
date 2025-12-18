using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace STASIS.Models;

public class UserProfile
{
    [Key]
    public int UserProfileID { get; set; }

    [Required]
    public string AspNetUserId { get; set; } = string.Empty;

    [ForeignKey("AspNetUserId")]
    public IdentityUser? User { get; set; }

    public string? Department { get; set; }
    public bool CanApproveShipments { get; set; }
    public bool CanApproveDiscards { get; set; }
    public bool MustChangePassword { get; set; }
}
