using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.Clients;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class EditSecretModel(
    ClientRepository clientRepository
    ) : PageModel
{

    [BindProperty(SupportsGet = true)]
    public InputModel Input { get; set; } = default!;

    [BindProperty]
    public string Button { get; set; } = string.Empty;

    public bool Updated { get; set; } = false;
    public bool Cleared { get; set; } = false;

    public void OnGet(string clientId) => Input = new InputModel
    {
        ClientId = clientId,
        Secret = string.Empty
    };

    public async Task<IActionResult> OnPost()
    {
        if (Button == "clear")
        {
            await clientRepository.ClearClientSecret(Input.ClientId);
            Cleared = true;
        }

        if (Button == "save")
        {
            if (string.IsNullOrEmpty(Input.Secret))
            {
                ModelState.AddModelError(string.Empty, "Secret cannot be empty.");
                return Page();
            }

            await clientRepository.SetClientSecret(Input.ClientId, Input.Secret, Input.Description);
            Updated = true;
        }

        Input = new InputModel
        {
            ClientId = Input.ClientId
        };

        return Page();
    }
}

public class InputModel
{
    public string ClientId { get; set; } = string.Empty;
    public string? Secret { get; set; } = string.Empty;
    public string? Description { get; set; } = null;
}
