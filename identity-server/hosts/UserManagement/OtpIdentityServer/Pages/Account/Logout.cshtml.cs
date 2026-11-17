// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OtpIdentityServer.Pages.Account;

public class LogoutModel(IIdentityServerInteractionService interaction) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string? logoutId)
    {
        // Get logout context from IdentityServer
        var context = await interaction.GetLogoutContextAsync(logoutId, HttpContext.RequestAborted);

        // Sign out of cookie authentication
        await HttpContext.SignOutAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);

        // If we have a post logout redirect URI, use it
        if (!string.IsNullOrEmpty(context?.PostLogoutRedirectUri))
        {
            return Redirect(context.PostLogoutRedirectUri);
        }

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostAsync(string? logoutId) => await OnGetAsync(logoutId);
}
