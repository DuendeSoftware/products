using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TemplateWebApp.Pages;

public class IndexModel : PageModel
{
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public void OnGet()
    {
    }
}
