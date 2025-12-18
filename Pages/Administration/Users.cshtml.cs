using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;

namespace STASIS.Pages.Administration
{
    public class UsersModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly StasisDbContext _context;

        public UsersModel(UserManager<IdentityUser> userManager, StasisDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IList<UserViewModel> Users { get; set; } = new List<UserViewModel>();

        public class UserViewModel
        {
            public string UserId { get; set; }
            public string Email { get; set; }
            public string Roles { get; set; }
            public string Department { get; set; }
        }

        public async Task OnGetAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var userProfiles = await _context.UserProfiles.ToListAsync();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var profile = userProfiles.FirstOrDefault(p => p.AspNetUserId == user.Id);

                Users.Add(new UserViewModel
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Roles = string.Join(", ", roles),
                    Department = profile?.Department ?? "-"
                });
            }
        }
    }
}