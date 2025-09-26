using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.Federation;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class NewModel(FederationRepository repository) : PageModel
{
    [BindProperty]
    public CreateProviderModel InputModel { get; set; } = default!;

    public bool Created { get; set; }

    public void OnGet(string type) => InputModel = new CreateProviderModel
    {
        Type = type,
        Configuration = repository.FindProviderConfigurationModelFactoryFor(type).Create()
    };

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await repository.CreateAsync(InputModel);
            Created = true;
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        return Page();
    }

}
