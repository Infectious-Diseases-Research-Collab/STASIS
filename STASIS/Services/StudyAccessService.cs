using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;

namespace STASIS.Services;

public class StudyAccessService : IStudyAccessService
{
    private const string SystemAdminRole = "Admin";

    private readonly StasisDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public StudyAccessService(StasisDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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

    private static StudyRole MapRole(string? role) => role switch
    {
        "Viewer" => StudyRole.Viewer,
        "Editor" => StudyRole.Editor,
        "StudyAdmin" => StudyRole.StudyAdmin,
        _ => StudyRole.None
    };
}
