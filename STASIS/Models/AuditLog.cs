using System.ComponentModel.DataAnnotations;

namespace STASIS.Models;

public class AuditLog
{
    [Key]
    public int AuditLogID { get; set; }
    public string? TableName { get; set; }
    public int? RecordID { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public int? ChangedBy { get; set; }
    public User? User { get; set; }
    public DateTime Timestamp { get; set; }
}
