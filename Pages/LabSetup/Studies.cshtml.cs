using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;
using STASIS.Services;

namespace STASIS.Pages.LabSetup
{
    [Authorize(Roles = "Admin")]
    public class StudiesModel : PageModel
    {
        private readonly StasisDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IStudyAccessService _studyAccessService;
        private readonly UserManager<IdentityUser> _userManager;

        public StudiesModel(
            StasisDbContext context,
            IAuditService auditService,
            IStudyAccessService studyAccessService,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _auditService = auditService;
            _studyAccessService = studyAccessService;
            _userManager = userManager;
        }

        public List<Study> Studies { get; set; } = new();

        // All AspNetUsers, for picking a brand-new study's first Study Admin. Deliberately
        // unscoped (not filtered by existing study access) -- any user in the system could
        // become the first admin of a study that, by definition, nobody has access to yet.
        public SelectList Users { get; set; } = new SelectList(Enumerable.Empty<IdentityUser>(), "Id", "Email");

        [BindProperty]
        public StudyInput Input { get; set; } = new();

        public class StudyInput
        {
            public int? StudyID { get; set; }

            [Required]
            [StringLength(50)]
            [Display(Name = "Study Code")]
            public string StudyCode { get; set; } = string.Empty;

            [StringLength(200)]
            [Display(Name = "Study Name")]
            public string? StudyName { get; set; }

            [StringLength(200)]
            [Display(Name = "Principal Investigator")]
            public string? PrincipalInvestigator { get; set; }

            // Required only on Add (checked explicitly in OnPostAddAsync rather than via
            // [Required] here) since the Edit modal doesn't collect this field and shares this
            // same Input class.
            [Display(Name = "First Study Admin")]
            public string? FirstStudyAdminUserId { get; set; }
        }

        public async Task OnGetAsync()
        {
            Studies = await _context.Studies.OrderBy(s => s.StudyCode).ToListAsync();
            Users = await BuildUserSelectListAsync();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (string.IsNullOrWhiteSpace(Input.FirstStudyAdminUserId))
            {
                ModelState.AddModelError("Input.FirstStudyAdminUserId", "Select the study's first Study Admin.");
            }
            // IDOR/staleness guard, mirroring CreateUser.cshtml.cs's server-side re-validation of
            // Input.StudyID: re-check that the posted user actually exists before the Study is
            // committed. Without this, a forged or stale ID (not in the dropdown, or a user
            // deleted between page render and submit) would let the Study row commit via the
            // SaveChangesAsync below, then throw an unhandled DbUpdateException out of
            // GrantAccessAsync's own save (AspNetUserId has a real FK constraint) -- leaving a
            // headless Study with zero Study Admins and a 500 for the caller instead of a clean
            // rejection.
            else if (await _userManager.FindByIdAsync(Input.FirstStudyAdminUserId) == null)
            {
                ModelState.AddModelError("Input.FirstStudyAdminUserId", "Select a valid first Study Admin.");
            }

            if (!ModelState.IsValid)
            {
                Studies = await _context.Studies.OrderBy(s => s.StudyCode).ToListAsync();
                Users = await BuildUserSelectListAsync();
                return Page();
            }

            var study = new Study
            {
                StudyCode = Input.StudyCode,
                StudyName = Input.StudyName,
                PrincipalInvestigator = Input.PrincipalInvestigator
            };

            _context.Studies.Add(study);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("Input.StudyCode", "This study code is already in use.");
                Studies = await _context.Studies.OrderBy(s => s.StudyCode).ToListAsync();
                Users = await BuildUserSelectListAsync();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            await _auditService.LogChangeAsync("tbl_Studies", study.StudyID.ToString(),
                "Created", null, study.StudyCode, userId);

            // GrantAccessAsync writes its own "tbl_UserStudyAccess" audit log entry (using the
            // new UserStudyAccess row's own int ID as RecordID), so there is no separate
            // LogChangeAsync call to make here. The System Admin who created the study does NOT
            // get an implicit access row of their own -- per the design doc, they stay in
            // System Admin override status unless they separately grant themselves access.
            await _studyAccessService.GrantAccessAsync(Input.FirstStudyAdminUserId!, study.StudyID, "StudyAdmin", userId);

            TempData["Success"] = $"Study \"{study.StudyCode}\" added.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            if (!ModelState.IsValid || Input.StudyID == null)
            {
                Studies = await _context.Studies.OrderBy(s => s.StudyCode).ToListAsync();
                Users = await BuildUserSelectListAsync();
                return Page();
            }

            var existing = await _context.Studies.FindAsync(Input.StudyID.Value);
            if (existing == null) return NotFound();

            var oldCode = existing.StudyCode;
            existing.StudyCode = Input.StudyCode;
            existing.StudyName = Input.StudyName;
            existing.PrincipalInvestigator = Input.PrincipalInvestigator;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("Input.StudyCode", "This study code is already in use.");
                Studies = await _context.Studies.OrderBy(s => s.StudyCode).ToListAsync();
                Users = await BuildUserSelectListAsync();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            if (oldCode != existing.StudyCode)
                await _auditService.LogChangeAsync("tbl_Studies", existing.StudyID.ToString(),
                    "StudyCode", oldCode, existing.StudyCode, userId);

            TempData["Success"] = $"Study \"{existing.StudyCode}\" updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var study = await _context.Studies
                .Include(s => s.Specimens)
                .FirstOrDefaultAsync(s => s.StudyID == id);

            if (study == null) return NotFound();

            if (study.Specimens.Count > 0)
            {
                TempData["Error"] = "Cannot delete study — it has specimens associated with it.";
                return RedirectToPage();
            }

            _context.Studies.Remove(study);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Study deleted.";
            return RedirectToPage();
        }

        // Mirrors Pages/Administration/Users.cshtml.cs's use of _userManager.Users for listing
        // every AspNetUser -- not a direct StasisDbContext query -- to build the "first Study
        // Admin" dropdown.
        private async Task<SelectList> BuildUserSelectListAsync()
        {
            var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
            return new SelectList(users, "Id", "Email");
        }
    }
}
