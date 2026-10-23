using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using STASIS.Data;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.Administration
{
    // No class-level [Authorize] here: viewing this page and editing study access is for System
    // Admins AND Study Admins (of at least one study), mirroring Pages/Administration/Users.cshtml.cs
    // and CreateUser.cshtml.cs. A Study Admin may only grant/revoke/change roles for studies they
    // personally manage -- re-validated server-side via CanManageStudyAsync in each handler, not
    // just enforced by what the UI happens to show. Profile-wide fields (Department, approval
    // flags) and password resets remain restricted to System Admins only, checked separately in
    // their own handlers, since those aren't study-scoped.
    public class EditUserModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly StasisDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IStudyAccessService _studyAccessService;

        public EditUserModel(
            UserManager<IdentityUser> userManager,
            StasisDbContext context,
            IAuditService auditService,
            IStudyAccessService studyAccessService)
        {
            _userManager = userManager;
            _context = context;
            _auditService = auditService;
            _studyAccessService = studyAccessService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        // Drives whether the view shows the profile-fields form / password reset form at all. A
        // Study Admin never gets those controls; they may only manage study access below.
        public bool CallerIsSystemAdmin { get; set; }

        // The target user's current study-access rows (filtered to studies the caller manages,
        // for a Study Admin caller -- mirrors Pages/Administration/Users.cshtml.cs's visibility
        // rule), each flagged with whether the caller may act on it.
        public List<AccessRowViewModel> AccessRows { get; set; } = new();

        // Studies the caller may grant this user NEW access to (manageable studies minus ones the
        // user already has a row for).
        public SelectList GrantableStudies { get; set; } = new SelectList(Enumerable.Empty<Study>(), "StudyID", "StudyCode");

        public static readonly string[] StudyRoleOptions = { "Viewer", "Editor", "StudyAdmin" };

        public SelectList StudyRoles { get; } = new SelectList(StudyRoleOptions);

        public class InputModel
        {
            public string UserId { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Department { get; set; }
            public bool CanApproveShipments { get; set; }
            public bool CanApproveDiscards { get; set; }
        }

        public class AccessRowViewModel
        {
            public int StudyID { get; set; }
            public string StudyCode { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public bool CallerCanManage { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var authorization = await AuthorizeCallerAsync(currentUserId);
            if (authorization.Forbidden)
            {
                return Forbid();
            }

            CallerIsSystemAdmin = authorization.IsSystemAdmin;

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.AspNetUserId == user.Id);

            Input = new InputModel
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Department = profile?.Department,
                CanApproveShipments = profile?.CanApproveShipments ?? false,
                CanApproveDiscards = profile?.CanApproveDiscards ?? false
            };

            await LoadAccessRowsAsync(user.Id, authorization);
            GrantableStudies = await BuildGrantableStudySelectListAsync(currentUserId, user.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var authorization = await AuthorizeCallerAsync(currentUserId);
            if (authorization.Forbidden)
            {
                return Forbid();
            }

            // Profile-wide fields (Department, approval flags) are account-wide, not study-scoped
            // -- only a System Admin may edit them, even though a Study Admin can reach this page
            // to manage study access.
            if (!authorization.IsSystemAdmin)
            {
                return Forbid();
            }

            CallerIsSystemAdmin = authorization.IsSystemAdmin;

            if (!ModelState.IsValid)
            {
                await LoadAccessRowsAsync(Input.UserId, authorization);
                GrantableStudies = await BuildGrantableStudySelectListAsync(currentUserId, Input.UserId);
                return Page();
            }

            var user = await _userManager.FindByIdAsync(Input.UserId);
            if (user == null) return NotFound();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.AspNetUserId == user.Id);
            if (profile == null)
            {
                profile = new UserProfile { AspNetUserId = user.Id };
                _context.UserProfiles.Add(profile);
            }

            profile.Department = Input.Department;
            profile.CanApproveShipments = Input.CanApproveShipments;
            profile.CanApproveDiscards = Input.CanApproveDiscards;

            await _context.SaveChangesAsync();

            await _auditService.LogChangeAsync("AspNetUsers", user.Id,
                "Updated", null, $"Dept: {Input.Department}", currentUserId);

            return RedirectToPage("./Users");
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(string id, string newPassword)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Password resets are account-wide, not study-scoped -- System Admin only.
            if (!await _studyAccessService.IsSystemAdminAsync(currentUserId))
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.AspNetUserId == user.Id);
                if (profile != null)
                {
                    profile.MustChangePassword = true;
                    await _context.SaveChangesAsync();
                }

                await _auditService.LogChangeAsync("AspNetUsers", user.Id,
                    "PasswordReset", null, "Password reset by admin", currentUserId);

                return RedirectToPage("./Users");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        // Grants the target user a new role on a study. Rejected server-side (IDOR guard,
        // mirroring CreateUser.cshtml.cs) if the caller doesn't manage that study, even though the
        // UI would never offer a study the caller can't manage in GrantableStudies.
        public async Task<IActionResult> OnPostGrantAccessAsync(string userId, int studyId, string role)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (!StudyRoleOptions.Contains(role))
            {
                TempData["Error"] = "Select a valid role.";
                return RedirectToPage(new { id = userId });
            }

            if (!await _studyAccessService.CanManageStudyAsync(currentUserId, studyId))
            {
                TempData["Error"] = "You do not have permission to manage that study.";
                return RedirectToPage(new { id = userId });
            }

            // Avoid violating the (AspNetUserId, StudyID) unique constraint -- a user who already
            // has an access row for this study must go through OnPostChangeRoleAsync instead.
            if (await _studyAccessService.GetRoleAsync(userId, studyId) != StudyRole.None)
            {
                TempData["Error"] = "That user already has access to this study. Use Change Role instead.";
                return RedirectToPage(new { id = userId });
            }

            // GrantAccessAsync writes its own "tbl_UserStudyAccess" audit log entry, so there is no
            // separate LogChangeAsync call to make here.
            await _studyAccessService.GrantAccessAsync(userId, studyId, role, currentUserId);

            TempData["Success"] = "Access granted.";
            return RedirectToPage(new { id = userId });
        }

        // Revokes the target user's access to a study. Rejected server-side if the caller doesn't
        // manage that study, or if the target row is the study's last remaining Study Admin -- a
        // study must always have at least one Study Admin, even when the caller is a System Admin.
        public async Task<IActionResult> OnPostRevokeAccessAsync(string userId, int studyId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (!await _studyAccessService.CanManageStudyAsync(currentUserId, studyId))
            {
                TempData["Error"] = "You do not have permission to manage that study.";
                return RedirectToPage(new { id = userId });
            }

            var currentRole = await _studyAccessService.GetRoleAsync(userId, studyId);
            if (currentRole == StudyRole.None)
            {
                TempData["Error"] = "That user has no access to this study.";
                return RedirectToPage(new { id = userId });
            }

            try
            {
                // RevokeAccessAsync writes its own "tbl_UserStudyAccess" audit log entry, mirroring
                // GrantAccessAsync, so there is no separate LogChangeAsync call to make here. It
                // also enforces the "at least one Study Admin" guardrail itself, throwing if the
                // target row is the study's last remaining Study Admin.
                await _studyAccessService.RevokeAccessAsync(userId, studyId, currentUserId);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToPage(new { id = userId });
            }

            TempData["Success"] = "Access revoked.";
            return RedirectToPage(new { id = userId });
        }

        // Changes the target user's role on a study. Rejected server-side if the caller doesn't
        // manage that study, or if the target row is the study's last remaining Study Admin and
        // the new role is not StudyAdmin -- a study must always have at least one Study Admin,
        // even when the caller is a System Admin.
        public async Task<IActionResult> OnPostChangeRoleAsync(string userId, int studyId, string newRole)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (!StudyRoleOptions.Contains(newRole))
            {
                TempData["Error"] = "Select a valid role.";
                return RedirectToPage(new { id = userId });
            }

            if (!await _studyAccessService.CanManageStudyAsync(currentUserId, studyId))
            {
                TempData["Error"] = "You do not have permission to manage that study.";
                return RedirectToPage(new { id = userId });
            }

            var currentRole = await _studyAccessService.GetRoleAsync(userId, studyId);
            if (currentRole == StudyRole.None)
            {
                TempData["Error"] = "That user has no access to this study.";
                return RedirectToPage(new { id = userId });
            }

            try
            {
                // ChangeRoleAsync writes its own "tbl_UserStudyAccess" audit log entry, mirroring
                // GrantAccessAsync, so there is no separate LogChangeAsync call to make here. It
                // also enforces the "at least one Study Admin" guardrail itself, throwing if the
                // target row is the study's last remaining Study Admin and newRole isn't StudyAdmin.
                await _studyAccessService.ChangeRoleAsync(userId, studyId, newRole, currentUserId);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToPage(new { id = userId });
            }

            TempData["Success"] = "Role updated.";
            return RedirectToPage(new { id = userId });
        }

        private readonly record struct CallerAuthorization(bool IsSystemAdmin, HashSet<int> ManagedStudyIds, bool Forbidden);

        // Mirrors Pages/Administration/Users.cshtml.cs and CreateUser.cshtml.cs's access check:
        // this page is for System Admins and Study Admins (of at least one study) only.
        private async Task<CallerAuthorization> AuthorizeCallerAsync(string currentUserId)
        {
            var isSystemAdmin = await _studyAccessService.IsSystemAdminAsync(currentUserId);
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

                if (managedStudyIds.Count == 0)
                {
                    return new CallerAuthorization(false, managedStudyIds, true);
                }
            }

            return new CallerAuthorization(isSystemAdmin, managedStudyIds, false);
        }

        private async Task LoadAccessRowsAsync(string userId, CallerAuthorization authorization)
        {
            var allAccess = await _studyAccessService.GetAllAccessRowsAsync();
            var rows = allAccess.Where(a => a.AspNetUserId == userId);

            if (!authorization.IsSystemAdmin)
            {
                // A Study Admin only sees/manages rows for studies they manage, not the target
                // user's full access list, mirroring Pages/Administration/Users.cshtml.cs.
                rows = rows.Where(a => authorization.ManagedStudyIds.Contains(a.StudyID));
            }

            AccessRows = rows
                .Select(a => new AccessRowViewModel
                {
                    StudyID = a.StudyID,
                    StudyCode = a.Study?.StudyCode ?? string.Empty,
                    Role = a.Role,
                    CallerCanManage = authorization.IsSystemAdmin || authorization.ManagedStudyIds.Contains(a.StudyID)
                })
                .OrderBy(a => a.StudyCode)
                .ToList();
        }

        private async Task<SelectList> BuildGrantableStudySelectListAsync(string currentUserId, string targetUserId)
        {
            var manageableStudies = await _studyAccessService.GetManageableStudiesAsync(currentUserId);

            var allAccess = await _studyAccessService.GetAllAccessRowsAsync();
            var alreadyGrantedStudyIds = allAccess
                .Where(a => a.AspNetUserId == targetUserId)
                .Select(a => a.StudyID)
                .ToHashSet();

            var grantable = manageableStudies.Where(s => !alreadyGrantedStudyIds.Contains(s.StudyID));
            return new SelectList(grantable, "StudyID", "StudyCode");
        }
    }
}
