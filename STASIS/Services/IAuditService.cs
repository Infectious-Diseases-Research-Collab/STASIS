namespace STASIS.Services;

public interface IAuditService
{
    Task LogChangeAsync(string tableName, string recordId, string fieldName,
        string? oldValue, string? newValue, string userId);
}
