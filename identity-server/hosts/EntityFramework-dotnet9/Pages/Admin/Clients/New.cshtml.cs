// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages.Admin.Clients;

[SecurityHeaders]
[Authorize]
public class NewModel : PageModel
{
    private readonly ClientRepository _repository;

    public NewModel(ClientRepository repository) => _repository = repository;

    [BindProperty]
    public CreateClientModel InputModel { get; set; } = default!;

    public bool Created { get; set; }

    public void OnGet() => InputModel = new CreateClientModel
    {
        Secret = Convert.ToBase64String(CryptoRandom.CreateRandomKey(16))
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
