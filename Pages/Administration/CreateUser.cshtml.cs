using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using STASIS.Data;
using STASIS.Models;
using STASIS.Services;
using System.ComponentModel.DataAnnotations;

namespace STASIS.Pages.Administration
{
    // No class-level [Authorize] here: this page is for System Admins AND Study Admins (of at
    // least one study), so the check happens in OnGetAsync/OnPostAsync, where both groups are
    // recognized and everyone else gets Forbid(). Mirrors Pages/Administration/Users.cshtml.cs.
    public class CreateUserModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly StasisDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IStudyAccessService _studyAccessService;

        public CreateUserModel(
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

        // Studies the caller may assign a new user to: every study for a System Admin, only the
        // studies they personally manage (StudyAdmin role) for a Study Admin.
        public SelectList Studies { get; set; } = new SelectList(Enumerable.Empty<Study>(), "StudyID", "StudyCode");

        // Drives whether the view shows the "System Admin" checkbox at all. A Study Admin never
        // gets the option in the UI, and the server ignores it even if posted anyway.
        public bool CallerIsSystemAdmin { get; set; }

        public static readonly string[] StudyRoleOptions = { "Viewer", "Editor", "StudyAdmin" };

        public SelectList StudyRoles { get; } = new SelectList(StudyRoleOptions);

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public string? Department { get; set; }

            public bool IsSystemAdmin { get; set; }

            public int? StudyID { get; set; }

            public string? StudyRole { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var authorization = await AuthorizeCallerAsync(currentUserId);
            if (authorization.Forbidden)
            {
                return Forbid();
            }

            CallerIsSystemAdmin = authorization.IsSystemAdmin;
            Studies = await BuildStudySelectListAsync(currentUserId);
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

            CallerIsSystemAdmin = authorization.IsSystemAdmin;

            // A Study Admin cannot grant System Admin. Ignore the posted flag rather than trust
            // the client — the view never offers this control to a Study Admin in the first
            // place, but this guards against a direct POST bypassing the UI.
            if (!authorization.IsSystemAdmin)
            {
                Input.IsSystemAdmin = false;
            }

            if (Input.StudyID.HasValue)
            {
                if (string.IsNullOrWhiteSpace(Input.StudyRole) || !StudyRoleOptions.Contains(Input.StudyRole))
                {
                    ModelState.AddModelError("Input.StudyRole", "Select a valid role for the assigned study.");
                }

                // IDOR guard (anti-goal in the spec): re-validate server-side that the caller can
                // actually manage the posted study, regardless of what the dropdown would have
                // offered. A direct POST of a StudyID the caller doesn't manage must be rejected
                // even though the UI would never have offered it.
                if (!await _studyAccessService.CanManageStudyAsync(currentUserId, Input.StudyID.Value))
                {
                    ModelState.AddModelError("Input.StudyID", "You do not have permission to assign users to that study.");
                }
            }

            if (!ModelState.IsValid)
            {
                Studies = await BuildStudySelectListAsync(currentUserId);
                return Page();
            }

            var user = new IdentityUser { UserName = Input.Email, Email = Input.Email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                var grantedSystemAdmin = Input.IsSystemAdmin && authorization.IsSystemAdmin;
                if (grantedSystemAdmin)
                {
                    await _userManager.AddToRoleAsync(user, "Admin");
                }

                // Create Profile
                var profile = new UserProfile
                {
                    AspNetUserId = user.Id,
                    Department = Input.Department,
                    MustChangePassword = true
                };
                _context.UserProfiles.Add(profile);
                await _context.SaveChangesAsync();

                await _auditService.LogChangeAsync("AspNetUsers", user.Id,
                    "Created", null, $"{Input.Email}, SystemAdmin: {grantedSystemAdmin}", currentUserId);

                if (Input.StudyID.HasValue && !string.IsNullOrWhiteSpace(Input.StudyRole))
                {
                    // GrantAccessAsync writes its own "tbl_UserStudyAccess" audit log entry
                    // (using the new UserStudyAccess row's own int ID as RecordID), so there is
                    // no separate LogChangeAsync call to make here.
                    await _studyAccessService.GrantAccessAsync(user.Id, Input.StudyID.Value, Input.StudyRole, currentUserId);
                }

                return RedirectToPage("./Users");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            Studies = await BuildStudySelectListAsync(currentUserId);
            return Page();
        }

        private readonly record struct CallerAuthorization(bool IsSystemAdmin, HashSet<int> ManagedStudyIds, bool Forbidden);

        // Mirrors Pages/Administration/Users.cshtml.cs's access check: this page is for System
        // Admins and Study Admins (of at least one study) only.
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

        private async Task<SelectList> BuildStudySelectListAsync(string currentUserId)
        {
            var studies = await _studyAccessService.GetManageableStudiesAsync(currentUserId);
            return new SelectList(studies, "StudyID", "StudyCode");
        }
    }
}
