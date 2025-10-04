// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Web.Pages;

[Authorize]
public class SecureModel : PageModel
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnGetRenewTokensAsync()
    {
        await HttpContext.GetUserAccessTokenAsync(new UserTokenRequestParameters { ForceTokenRenewal = true });
        return RedirectToPage();
    }
}
