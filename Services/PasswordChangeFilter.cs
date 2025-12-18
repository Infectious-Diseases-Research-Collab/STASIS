using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;

namespace STASIS.Services;

public class PasswordChangeFilter : IAsyncPageFilter
{
    private readonly StasisDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public PasswordChangeFilter(StasisDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        await Task.CompletedTask;
    }

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // If user is logged in
        if (user.Identity != null && user.Identity.IsAuthenticated)
        {
            var path = context.HttpContext.Request.Path.Value?.ToLower();

            // Allow access to the Change Password page and Logout (to avoid infinite loops)
            if (!string.IsNullOrEmpty(path) && 
                (path.Contains("/account/manage/changepassword") || 
                 path.Contains("/account/logout")))
            {
                await next();
                return;
            }

            var userId = _userManager.GetUserId(user);
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.AspNetUserId == userId);

            if (profile != null && profile.MustChangePassword)
            {
                // Force redirect
                context.Result = new RedirectToPageResult("/Account/Manage/ChangePassword", new { area = "Identity" });
                return;
            }
        }

        await next();
    }
}
