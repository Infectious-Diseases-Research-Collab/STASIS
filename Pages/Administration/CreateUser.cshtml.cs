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
    public class CreateUserModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly StasisDbContext _context;

        public CreateUserModel(
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
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            public string Department { get; set; }

            [Required]
            public string Role { get; set; }
        }

        public async Task OnGetAsync()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            Roles = new SelectList(roles, "Name", "Name");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                var roles = await _roleManager.Roles.ToListAsync();
                Roles = new SelectList(roles, "Name", "Name");
                return Page();
            }

            var user = new IdentityUser { UserName = Input.Email, Email = Input.Email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                // Assign Role
                if (!string.IsNullOrEmpty(Input.Role))
                {
                    await _userManager.AddToRoleAsync(user, Input.Role);
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

                return RedirectToPage("./Users");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            // Reload roles if failed
            var rolesList = await _roleManager.Roles.ToListAsync();
            Roles = new SelectList(rolesList, "Name", "Name");
            return Page();
        }
    }
}
