// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages.Admin.Clients;

[SecurityHeaders]
[Authorize]
public class EditModel : PageModel
{
    private readonly ClientRepository _repository;

    public EditModel(ClientRepository repository)
    {
        _repository = repository;
    }

    [BindProperty]
    public ClientModel InputModel { get; set; } = default!;
    [BindProperty]
    public string? Button { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var model = await _repository.GetByIdAsync(id);
        if (model == null)
        {
            return RedirectToPage("/Admin/Clients/Index");
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
            await _repository.DeleteAsync(id);
            return RedirectToPage("/Admin/Clients/Index");
        }

        if (ModelState.IsValid)
        {
            await _repository.UpdateAsync(InputModel);
            return RedirectToPage("/Admin/Clients/Edit", new { id });
        }

        return Page();
    }
}
