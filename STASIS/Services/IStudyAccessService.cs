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

    // Number of UserStudyAccess rows with Role == "StudyAdmin" for the given study. Lets page
    // models (e.g. Administration/EditUser) enforce the "a study must have at least one Study
    // Admin" guardrail before revoking/demoting a Study Admin, without calling StasisDbContext
    // directly.
    Task<int> CountStudyAdminsAsync(int studyId);

    // Revokes a user's access to a study by deleting their UserStudyAccess row. A no-op if the
    // user had no access row for that study. Callers are responsible for authorizing the revoke
    // (e.g. CanManageStudyAsync) and for enforcing the "at least one Study Admin" guardrail
    // before calling this. Also writes the "tbl_UserStudyAccess" audit log entry itself, mirroring
    // GrantAccessAsync, using the deleted row's own UserStudyAccessID as RecordID.
    Task RevokeAccessAsync(string userId, int studyId, string revokedByUserId);

    // Changes a user's role on a study by updating their existing UserStudyAccess row. A no-op if
    // the user had no access row for that study. Callers are responsible for authorizing the
    // change (e.g. CanManageStudyAsync) and for enforcing the "at least one Study Admin" guardrail
    // before calling this. Also writes the "tbl_UserStudyAccess" audit log entry itself, mirroring
    // GrantAccessAsync, using the row's own UserStudyAccessID as RecordID.
    Task ChangeRoleAsync(string userId, int studyId, string newRole, string changedByUserId);
}
