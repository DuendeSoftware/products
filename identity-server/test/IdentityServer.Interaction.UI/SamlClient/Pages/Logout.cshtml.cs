// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sustainsys.Saml2.AspNetCore2;

namespace Duende.IdentityServer.UI.SamlClient.Pages;

[AllowAnonymous]
public sealed class LogoutModel : PageModel
{
    public IActionResult OnGet() =>
        SignOut(Saml2Defaults.Scheme, CookieAuthenticationDefaults.AuthenticationScheme);
}
