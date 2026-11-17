// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthenticationFlowWithWebComponent.Pages;

[Authorize]
public class ProfileModel(IUserAuthenticatorsSelfService selfService) : PageModel
{
    [TempData]
    public string? StatusMessage { get; set; }

    public bool Has2FA { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (GetUserId() is not { } userId)
        {
            return RedirectToPage("/Index");
        }

        if (await selfService.TryGetAsync(userId, HttpContext.RequestAborted) is not { } user)
        {
            return NotFound("User not found.");
        }

        Has2FA = user.TotpDeviceNames.Count > 0;

        return Page();
    }

    private UserSubjectId? GetUserId()
    {
        if (User.FindFirst(JwtClaimTypes.Subject)?.Value is not { } userIdClaim)
        {
            return null;
        }

        return UserSubjectId.Create(userIdClaim);
    }
}
