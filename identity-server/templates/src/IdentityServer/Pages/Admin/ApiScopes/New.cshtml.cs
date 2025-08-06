using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerQuickStart.Pages.Admin.ApiScopes;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class NewModel(ApiScopeRepository repository) : PageModel
{
    [BindProperty]
    public ApiScopeModel InputModel { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            await repository.CreateAsync(InputModel);
            return RedirectToPage("/Admin/ApiScopes/Edit", new { id = InputModel.Name });
        }

        return Page();
    }
}
