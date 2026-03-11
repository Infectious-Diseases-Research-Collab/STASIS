using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace STASIS.Pages.LabSetup
{
    [Authorize(Roles = "Admin")]
    public class BoxTypesModel : PageModel
    {
        public static readonly string[] ValidBoxTypes = { "81-slot", "100-slot", "Filter Paper Binder" };
        public static readonly string[] ValidBoxCategories = { "Standard", "Temp", "Trash", "Shipping" };

        public void OnGet()
        {
        }
    }
}
