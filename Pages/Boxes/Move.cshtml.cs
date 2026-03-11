using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace STASIS.Pages.Boxes
{
    [Authorize(Roles = "Write,Admin")]
    public class MoveModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
