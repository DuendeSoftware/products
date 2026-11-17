// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExternalAuthentication.Pages;

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var idp = authResult?.Properties?.Items[".AuthScheme"];

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!string.IsNullOrEmpty(idp) && idp != "Google")
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = "/"
            };
            return SignOut(properties, idp);
        }

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostAsync() => await OnGetAsync();
}
