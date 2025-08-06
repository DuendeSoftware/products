using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerQuickStart.Pages.Admin.IdentityScopes;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class NewModel(IdentityScopeRepository repository) : PageModel
{
    [BindProperty]
    public IdentityScopeModel InputModel { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            await repository.CreateAsync(InputModel);
            return RedirectToPage("/Admin/IdentityScopes/Edit", new { id = InputModel.Name });
        }

        return Page();
    }
}
