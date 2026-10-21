using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Administration
{
    // No class-level [Authorize] here: this page is for System Admins AND Study Admins (of at
    // least one study), so the check happens in OnGetAsync, where both groups are recognized and
    // everyone else gets Forbid().
    public class UsersModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly StasisDbContext _context;
        private readonly IStudyAccessService _studyAccessService;

        public UsersModel(UserManager<IdentityUser> userManager, StasisDbContext context, IStudyAccessService studyAccessService)
        {
            _userManager = userManager;
            _context = context;
            _studyAccessService = studyAccessService;
        }

        public IList<UserViewModel> Users { get; set; } = new List<UserViewModel>();

        public class UserViewModel
        {
            public string UserId { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public List<(string StudyCode, string Role)> StudyAccess { get; set; } = new();
            public string Department { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(currentUserId);

            // Studies where the caller is a Study Admin. Irrelevant (left empty) for a System
            // Admin, who sees everyone unfiltered.
            var managedStudyIds = new HashSet<int>();
            if (!isSystemAdmin)
            {
                var visibleStudyIds = await _studyAccessService.GetVisibleStudyIdsAsync(currentUserId);
                if (visibleStudyIds != null)
                {
                    foreach (var studyId in visibleStudyIds)
                    {
                        if (await _studyAccessService.GetRoleAsync(currentUserId, studyId) == StudyRole.StudyAdmin)
                        {
                            managedStudyIds.Add(studyId);
                        }
                    }
                }

                // Neither a System Admin nor a Study Admin of any study: this page is for admins
                // only, never plain Viewer/Editor staff.
                if (managedStudyIds.Count == 0)
                {
                    return Forbid();
                }
            }

            var users = await _userManager.Users.ToListAsync();
            var userProfiles = await _context.UserProfiles.ToListAsync();
            var allAccess = await _studyAccessService.GetAllAccessRowsAsync();

            foreach (var user in users)
            {
                var accessRows = allAccess.Where(a => a.AspNetUserId == user.Id);
                if (!isSystemAdmin)
                {
                    // A Study Admin only sees rows for studies they manage, not a user's full
                    // access list.
                    accessRows = accessRows.Where(a => managedStudyIds.Contains(a.StudyID));
                }

                var studyAccess = accessRows
                    .Select(a => (StudyCode: a.Study?.StudyCode ?? string.Empty, a.Role))
                    .ToList();

                var isCaller = user.Id == currentUserId;

                // System Admin sees every user. A Study Admin sees only users who have some
                // access row on a study they manage, plus themselves.
                if (!isSystemAdmin && studyAccess.Count == 0 && !isCaller)
                {
                    continue;
                }

                var profile = userProfiles.FirstOrDefault(p => p.AspNetUserId == user.Id);

                Users.Add(new UserViewModel
                {
                    UserId = user.Id,
                    Email = user.Email ?? string.Empty,
                    StudyAccess = studyAccess,
                    Department = profile?.Department ?? "-"
                });
            }

            return Page();
        }
    }
}