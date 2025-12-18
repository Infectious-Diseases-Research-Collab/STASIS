using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;
using System.ComponentModel.DataAnnotations;

namespace STASIS.Pages.Administration
{
    public class EditUserModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly StasisDbContext _context;

        public EditUserModel(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            StasisDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public SelectList Roles { get; set; }

        public class InputModel
        {
            public string UserId { get; set; }
            public string Email { get; set; }
            public string Department { get; set; }
            [Required]
            public string Role { get; set; }
            public bool CanApproveShipments { get; set; }
            public bool CanApproveDiscards { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.AspNetUserId == user.Id);
            var userRoles = await _userManager.GetRolesAsync(user);
            var currentRole = userRoles.FirstOrDefault();

            Input = new InputModel
            {
                UserId = user.Id,
                Email = user.Email,
                Department = profile?.Department,
                Role = currentRole,
                CanApproveShipments = profile?.CanApproveShipments ?? false,
                CanApproveDiscards = profile?.CanApproveDiscards ?? false
            };

            var roles = await _roleManager.Roles.ToListAsync();
            Roles = new SelectList(roles, "Name", "Name");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                var roles = await _roleManager.Roles.ToListAsync();
                Roles = new SelectList(roles, "Name", "Name");
                return Page();
            }

            var user = await _userManager.FindByIdAsync(Input.UserId);
            if (user == null) return NotFound();

            // Update Role
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains(Input.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, userRoles);
                await _userManager.AddToRoleAsync(user, Input.Role);
            }

            // Update Profile
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

            return RedirectToPage("./Users");
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(string id, string newPassword)
        {
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

                return RedirectToPage("./Users");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }
    }
}
