using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace STASIS.Pages.Shipments
{
    [Authorize(Roles = "Write,Admin")]
    public class CreateModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
