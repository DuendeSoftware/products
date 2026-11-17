// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

public class RegisterShowRecoveryCodesModel(IUserAuthenticatorsSelfService selfService, RegistrationManager registrationManager) : PageModel
{
    public string[]? RecoveryCodes { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/");
        }

        if (User.Identity is null ||
            User.FindFirst("registration")?.Value != "show_recovery_codes" ||
            User.FindFirst(JwtClaimTypes.Subject)?.Value is not { } userIdString)
        {
            return RedirectToPage("/Register");
        }

        var userId = UserSubjectId.Create(userIdString);

        if (await selfService.TryCreateRecoveryCodesAsync(userId, HttpContext.RequestAborted) is not { } recoveryCodes)
        {
            ErrorMessage = "Failed to generate recovery codes.";
            return Page();
        }

        if (recoveryCodes.Count == 0)
        {
            return RedirectToPage("/Register");
        }

        RecoveryCodes = recoveryCodes.Select(x => x.Text).ToArray();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity is null ||
            User.FindFirst("registration")?.Value != "show_recovery_codes" ||
            User.FindFirst(JwtClaimTypes.Subject)?.Value is null ||
            User.FindFirst(JwtClaimTypes.Email)?.Value is null)
        {
            return RedirectToPage("/Register");
        }

        await registrationManager.UpdateRegistrationStepAsync("enable_authenticator");

        return RedirectToPage("/RegisterEnableAuthenticator");
    }
}
