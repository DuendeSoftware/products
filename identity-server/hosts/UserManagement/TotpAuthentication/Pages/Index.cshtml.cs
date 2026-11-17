// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

public class IndexModel(IUserAuthenticatorsSelfService selfService) : PageModel
{
    public bool Has2FA { get; set; }
    public int RecoveryCodesLeft { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity is not null &&
            User.Identity.IsAuthenticated &&
            User.FindFirst(JwtClaimTypes.Subject)?.Value is { } userId)
        {
            var user = await selfService.TryGetAsync(UserSubjectId.Create(userId), HttpContext.RequestAborted);
            Has2FA = user?.TotpDeviceNames.Count > 0;
            RecoveryCodesLeft = user?.RecoveryCodeCount ?? 0;
        }

        return Page();
    }
}
