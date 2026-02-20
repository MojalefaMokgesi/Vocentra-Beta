using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Vocentra.Pages
{
    public class Page030511Model : PageModel
    {
        [BindProperty]
        public string? Pin { get; set; }

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            // This page only performs a simple PIN check and redirects to the Admin area.
            // It does NOT change user roles or bypass normal ASP.NET Core Identity authorization.
            if (!string.IsNullOrEmpty(Pin) && Pin == "03051155")
            {
                return Redirect("/Admin");
            }

            ErrorMessage = "Incorrect PIN.";
            return Page();
        }
    }
}
