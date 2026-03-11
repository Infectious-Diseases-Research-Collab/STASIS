using STASIS.Data;
using STASIS.Models;

namespace STASIS.Services;

public class AuditService : IAuditService
{
    private readonly StasisDbContext _context;

    public AuditService(StasisDbContext context)
    {
        _context = context;
    }

    public async Task LogChangeAsync(string tableName, string recordId, string fieldName,
        string? oldValue, string? newValue, string userId)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            TableName = tableName,
            RecordID = int.TryParse(recordId, out var id) ? id : 0,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedByUserId = userId,
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }
}
