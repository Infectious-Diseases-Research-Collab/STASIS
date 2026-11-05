using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace STASIS.Areas.Identity.Pages.Account
{
    // Overrides the default Identity UI page so it returns 404. STASIS has no working email, so
    // passwords are reset only by a System Admin from Administration -> Edit User. The default
    // Forgot Password page also returned a 500 for existing accounts (the email send failed) and a
    // success page for unknown ones, which revealed which emails had accounts.
    [AllowAnonymous]
    public class ForgotPasswordConfirmationModel : PageModel
    {
        public IActionResult OnGet() => NotFound();

        public IActionResult OnPost() => NotFound();
    }
}
