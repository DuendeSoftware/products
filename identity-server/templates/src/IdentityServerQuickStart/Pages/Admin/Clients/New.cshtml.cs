using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerQuickStart.Pages.Admin.Clients;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class NewModel(ClientRepository repository) : PageModel
{
    [BindProperty]
    public CreateClientModel InputModel { get; set; } = default!;

    public bool Created { get; set; }

    public void OnGet(string type) => InputModel = new CreateClientModel
    {
        Flow = type == "m2m" ? Flow.ClientCredentials : Flow.CodeFlowWithPkce,
        ClientId = Guid.NewGuid().ToString()
    };

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (InputModel.Flow == Flow.CodeFlowWithPkce)
        {
            if (!string.IsNullOrWhiteSpace(InputModel.BaseUrl))
            {
                if (!Uri.TryCreate(InputModel.BaseUrl, UriKind.Absolute, out var baseUri))
                {
                    ModelState.AddModelError(nameof(InputModel.BaseUrl), "Base URL is not a valid absolute URI.");
                    return Page();
                }

                if (baseUri.Scheme is not ("http" or "https"))
                {
                    ModelState.AddModelError(nameof(InputModel.BaseUrl), "Base URL must start with http:// or https://.");
                    return Page();
                }
            }
        }

        else if (InputModel.Flow == Flow.ClientCredentials)
        {
            if (string.IsNullOrWhiteSpace(InputModel.Secret))
            {
                ModelState.AddModelError(nameof(InputModel.Secret), "Secret is required for Machine to Machine client.");
                return Page();
            }
        }

        await repository.CreateAsync(InputModel);
        Created = true;

        return Page();
    }

}
