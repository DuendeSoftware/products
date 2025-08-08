using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TemplateWebApp.Pages;

public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public string? ErrorMessage { get; set; }

    public void OnGet(string? message)
    {
        RequestId = HttpContext.TraceIdentifier;
        ErrorMessage = message;
    }
}
