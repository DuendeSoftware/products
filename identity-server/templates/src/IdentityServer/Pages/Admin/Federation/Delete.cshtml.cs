using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.Federation;

[Authorize(Config.Policies.Admin)]
public class DeleteModel(FederationRepository repository) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Scheme { get; set; } = default!;

    [BindProperty]
    public string? Button { get; set; }

    public void OnGet(string scheme) => Scheme = scheme;

    public async Task<IActionResult> OnPost()
    {
        await repository.DeleteAsync(Scheme);
        return RedirectToPage("/Admin/Federation/Index");
    }
}
