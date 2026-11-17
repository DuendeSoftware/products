// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using AspNetIdentityApp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AspNetIdentityApp.Pages;

public sealed class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public bool HasPasskey { get; set; }

    public async Task OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        var passkeys = await userManager.GetPasskeysAsync(user);
        HasPasskey = passkeys.Count > 0;
    }

    public async Task<IActionResult> OnPostRemovePasskeyAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return RedirectToPage();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return RedirectToPage();
        }

        var passkeys = await userManager.GetPasskeysAsync(user);
        foreach (var passkey in passkeys)
        {
            _ = await userManager.RemovePasskeyAsync(user, passkey.CredentialId);
        }

        return RedirectToPage();
    }
}
