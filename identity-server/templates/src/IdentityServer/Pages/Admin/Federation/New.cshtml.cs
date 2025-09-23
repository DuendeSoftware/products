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
        Type = type
    };

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (InputModel.Type.Equals("oidc", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(InputModel.Authority))
            {
                if (!Uri.TryCreate(InputModel.Authority, UriKind.Absolute, out var baseUri))
                {
                    ModelState.AddModelError(nameof(InputModel.Authority), "Authority URL is not a valid absolute URI.");
                    return Page();
                }

                if (baseUri.Scheme is not ("http" or "https"))
                {
                    ModelState.AddModelError(nameof(InputModel.Authority), "Authority URL must start with http:// or https://.");
                    return Page();
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(InputModel.IconUrl))
        {
            if (!Uri.TryCreate(InputModel.IconUrl, UriKind.RelativeOrAbsolute, out var iconUrl))
            {
                ModelState.AddModelError(nameof(InputModel.IconUrl), "Icon URL is not a valid URI.");
                return Page();
            }
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
