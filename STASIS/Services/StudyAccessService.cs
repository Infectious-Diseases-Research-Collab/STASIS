using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;

namespace STASIS.Services;

public class StudyAccessService : IStudyAccessService
{
    private const string SystemAdminRole = "Admin";

    private readonly StasisDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IAuditService _auditService;

    public StudyAccessService(StasisDbContext context, UserManager<IdentityUser> userManager, IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<bool> IsSystemAdminAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        return await _userManager.IsInRoleAsync(user, SystemAdminRole);
    }

    public async Task<StudyRole> GetRoleAsync(string userId, int studyId)
    {
        var access = await _context.UserStudyAccesses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AspNetUserId == userId && a.StudyID == studyId);

        return MapRole(access?.Role);
    }

    public async Task<IReadOnlyCollection<int>?> GetVisibleStudyIdsAsync(string userId)
    {
        if (await IsSystemAdminAsync(userId))
        {
            return null;
        }

        return await _context.UserStudyAccesses
            .AsNoTracking()
            .Where(a => a.AspNetUserId == userId)
            .Select(a => a.StudyID)
            .ToListAsync();
    }

    public async Task<bool> CanViewAsync(string userId, int studyId)
    {
        if (await IsSystemAdminAsync(userId))
        {
            return true;
        }

        return await GetRoleAsync(userId, studyId) != StudyRole.None;
    }

    public async Task<bool> CanEditAsync(string userId, int studyId)
    {
        if (await IsSystemAdminAsync(userId))
        {
            return true;
        }

        return await GetRoleAsync(userId, studyId) >= StudyRole.Editor;
    }

    public async Task<bool> CanManageStudyAsync(string userId, int studyId)
    {
        if (await IsSystemAdminAsync(userId))
        {
            return true;
        }

        return await GetRoleAsync(userId, studyId) == StudyRole.StudyAdmin;
    }

    public async Task<List<UserStudyAccess>> GetAllAccessRowsAsync()
    {
        return await _context.UserStudyAccesses
            .AsNoTracking()
            .Include(a => a.Study)
            .ToListAsync();
    }

    public async Task GrantAccessAsync(string userId, int studyId, string role, string grantedByUserId)
    {
        var access = new UserStudyAccess
        {
            AspNetUserId = userId,
            StudyID = studyId,
            Role = role,
            GrantedByUserId = grantedByUserId,
            GrantedDate = DateTime.UtcNow
        };

        _context.UserStudyAccesses.Add(access);
        await _context.SaveChangesAsync();

        // Logged here (rather than by the caller) so RecordID is always the newly-created row's
        // own int UserStudyAccessID, available only after SaveChangesAsync assigns it. A caller
        // logging this itself would be tempted to pass userId (a GUID string), which
        // AuditService.LogChangeAsync can't parse into its int RecordID column and would
        // silently collapse to 0.
        await _auditService.LogChangeAsync("tbl_UserStudyAccess", access.UserStudyAccessID.ToString(),
            "Created", null, $"UserId: {userId}, StudyID: {studyId}, Role: {role}", grantedByUserId);
    }

    public async Task<List<Study>> GetManageableStudiesAsync(string userId)
    {
        if (await IsSystemAdminAsync(userId))
        {
            return await _context.Studies
                .AsNoTracking()
                .OrderBy(s => s.StudyCode)
                .ToListAsync();
        }

        var managedStudyIds = await _context.UserStudyAccesses
            .AsNoTracking()
            .Where(a => a.AspNetUserId == userId && a.Role == "StudyAdmin")
            .Select(a => a.StudyID)
            .ToListAsync();

        return await _context.Studies
            .AsNoTracking()
            .Where(s => managedStudyIds.Contains(s.StudyID))
            .OrderBy(s => s.StudyCode)
            .ToListAsync();
    }

    public async Task<int> CountStudyAdminsAsync(int studyId)
    {
        return await _context.UserStudyAccesses
            .AsNoTracking()
            .CountAsync(a => a.StudyID == studyId && a.Role == "StudyAdmin");
    }

    public async Task RevokeAccessAsync(string userId, int studyId, string revokedByUserId)
    {
        var access = await _context.UserStudyAccesses
            .FirstOrDefaultAsync(a => a.AspNetUserId == userId && a.StudyID == studyId);

        if (access == null)
        {
            return;
        }

        var oldRole = access.Role;
        var recordId = access.UserStudyAccessID;

        _context.UserStudyAccesses.Remove(access);
        await _context.SaveChangesAsync();

        // Logged here (rather than by the caller), mirroring GrantAccessAsync, so RecordID is
        // always the deleted row's own int UserStudyAccessID rather than userId (a GUID string
        // AuditService.LogChangeAsync can't parse).
        await _auditService.LogChangeAsync("tbl_UserStudyAccess", recordId.ToString(),
            "Revoked", oldRole, null, revokedByUserId);
    }

    public async Task ChangeRoleAsync(string userId, int studyId, string newRole, string changedByUserId)
    {
        var access = await _context.UserStudyAccesses
            .FirstOrDefaultAsync(a => a.AspNetUserId == userId && a.StudyID == studyId);

        if (access == null)
        {
            return;
        }

        var oldRole = access.Role;
        access.Role = newRole;
        await _context.SaveChangesAsync();

        // Logged here (rather than by the caller), mirroring GrantAccessAsync, so RecordID is
        // always this row's own int UserStudyAccessID rather than userId (a GUID string
        // AuditService.LogChangeAsync can't parse).
        await _auditService.LogChangeAsync("tbl_UserStudyAccess", access.UserStudyAccessID.ToString(),
            "RoleChanged", oldRole, newRole, changedByUserId);
    }

    private static StudyRole MapRole(string? role) => role switch
    {
        "Viewer" => StudyRole.Viewer,
        "Editor" => StudyRole.Editor,
        "StudyAdmin" => StudyRole.StudyAdmin,
        _ => StudyRole.None
    };
}
