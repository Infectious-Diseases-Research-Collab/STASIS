namespace STASIS.Services;

public interface IStudyAccessService
{
    Task<bool> IsSystemAdminAsync(string userId);
    Task<StudyRole> GetRoleAsync(string userId, int studyId);
    Task<IReadOnlyCollection<int>?> GetVisibleStudyIdsAsync(string userId); // null = unrestricted (System Admin)
    Task<bool> CanViewAsync(string userId, int studyId);
    Task<bool> CanEditAsync(string userId, int studyId); // Editor or StudyAdmin or System Admin
    Task<bool> CanManageStudyAsync(string userId, int studyId); // StudyAdmin or System Admin
}
