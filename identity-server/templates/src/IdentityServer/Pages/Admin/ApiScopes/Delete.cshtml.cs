// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.ApiScopes;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class DeleteModel(ApiScopeRepository repository) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = default!;

    public void OnGet(string id) => Id = id;

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await repository.DeleteAsync(Id);
        return RedirectToPage("/Admin/ApiScopes/Index");
    }
}
