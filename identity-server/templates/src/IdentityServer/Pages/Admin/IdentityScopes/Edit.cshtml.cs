using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.IdentityScopes;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class EditModel(IdentityScopeRepository repository) : PageModel
{
    [BindProperty]
    public IdentityScopeModel InputModel { get; set; } = default!;

    [BindProperty]
    public string Button { get; set; } = default!;

    public bool Updated { get; set; } = false;

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var model = await repository.GetByIdAsync(id);

        if (model == null)
        {
            return RedirectToPage("/Admin/IdentityScopes/Index");
        }
        else
        {
            InputModel = model;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        if (Button == "delete")
        {
            await repository.DeleteAsync(id);
            return RedirectToPage("/Admin/IdentityScopes/Index");
        }

        if (ModelState.IsValid)
        {
            await repository.UpdateAsync(InputModel);
            Updated = true;

            return RedirectToPage("/Admin/IdentityScopes/Edit", new { id });
        }

        return Page();
    }
}
