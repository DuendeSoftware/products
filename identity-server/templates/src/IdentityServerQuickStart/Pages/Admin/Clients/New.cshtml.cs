using Duende.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages.Admin.Clients;

[SecurityHeaders]
[Authorize]
public class NewModel(ClientRepository repository) : PageModel
{
    private readonly ClientRepository _repository = repository;

    [BindProperty]
    public CreateClientModel InputModel { get; set; } = default!;

    public bool Created { get; set; }

    public void OnGet(string type) => InputModel = new CreateClientModel
    {
        Secret = Convert.ToBase64String(CryptoRandom.CreateRandomKey(16)),
        Flow = type == "m2m" ? Flow.ClientCredentials : Flow.CodeFlowWithPkce,
    };

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            await _repository.CreateAsync(InputModel);
            Created = true;
        }

        return Page();
    }
}
