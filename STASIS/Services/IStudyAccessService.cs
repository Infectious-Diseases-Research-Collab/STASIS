using STASIS.Models;

namespace STASIS.Services;

public interface IStudyAccessService
{
    Task<bool> IsSystemAdminAsync(string userId);
    Task<StudyRole> GetRoleAsync(string userId, int studyId);
    Task<IReadOnlyCollection<int>?> GetVisibleStudyIdsAsync(string userId); // null = unrestricted (System Admin)
    Task<bool> CanViewAsync(string userId, int studyId);
    Task<bool> CanEditAsync(string userId, int studyId); // Editor or StudyAdmin or System Admin
    Task<bool> CanManageStudyAsync(string userId, int studyId); // StudyAdmin or System Admin

    // Every UserStudyAccess row in the system, each with its Study navigation populated.
    // Lets page models (e.g. Administration/Users) build per-user study-access lists without
    // calling StasisDbContext directly, per this codebase's "services only" data-access rule.
    Task<List<UserStudyAccess>> GetAllAccessRowsAsync();

    // Grants a user a role on a study by inserting a new UserStudyAccess row. Lets page models
    // (e.g. Administration/CreateUser) write access grants without calling
    // StasisDbContext directly, per this codebase's "services only" data-access rule. Callers
    // are responsible for authorizing the grant (e.g. CanManageStudyAsync) before calling this.
    // Also writes the "tbl_UserStudyAccess" audit log entry itself, using the newly-created
    // row's own UserStudyAccessID as RecordID (not the caller-supplied userId, which is a GUID
    // string and would never parse into AuditLog.RecordID's int column).
    Task GrantAccessAsync(string userId, int studyId, string role, string grantedByUserId);

    // Studies the given user may assign other users to / manage: every study for a System
    // Admin, or only the studies where they hold the StudyAdmin role otherwise. Lets page models
    // (e.g. Administration/CreateUser) build a study picker without calling StasisDbContext
    // directly, per this codebase's "services only" data-access rule.
    Task<List<Study>> GetManageableStudiesAsync(string userId);
}
