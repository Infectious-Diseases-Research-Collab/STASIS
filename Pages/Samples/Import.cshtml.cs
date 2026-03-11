using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace STASIS.Pages.Samples
{
    [Authorize(Roles = "Write,Admin")]
    public class ImportModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
