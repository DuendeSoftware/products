// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Duende.IdentityServer.UI.ClientWebApp.Pages;

public class LogoutModel : PageModel
{
    public IActionResult OnGet() => SignOut("oidc", "Cookies");
}
