// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Totp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

[Authorize]
public class Manage2FAModel(
    IUserSelfService userSelfService) : PageModel
{
    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public int RecoveryCodesLeft { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (GetSubjectId() is not { } userId)
        {
            return RedirectToPage("/Login");
        }

        if (await userSelfService.Authenticators.TryGetAsync(userId, HttpContext.RequestAborted) is not { } authenticators)
        {
            return NotFound("User authenticators not found.");
        }

        // Check if 2FA is enabled
        if (authenticators.TotpDeviceNames.Count == 0)
        {
            return RedirectToPage("/EnableAuthenticator");
        }

        RecoveryCodesLeft = authenticators.RecoveryCodeCount;

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateRecoveryCodesAsync()
    {
        if (GetSubjectId() is not { } userId)
        {
            return RedirectToPage("/Login");
        }

        if (await userSelfService.Authenticators.TryGetAsync(userId, HttpContext.RequestAborted) is not { } authenticators)
        {
            return NotFound("User authenticators not found.");
        }

        // Check if 2FA is enabled
        if (authenticators.TotpDeviceNames.Count == 0)
        {
            ErrorMessage = "Two-factor authentication is not enabled.";
            return RedirectToPage("/EnableAuthenticator");
        }

        // Generate new recovery codes
        if (await userSelfService.Authenticators.TryCreateRecoveryCodesAsync(userId, HttpContext.RequestAborted) is not { } recoveryCodes)
        {
            ErrorMessage = "Failed to generate recovery codes.";
            return RedirectToPage();
        }

        StatusMessage = "You have generated new recovery codes.";
        TempData["RecoveryCodes"] = recoveryCodes.Select(c => string.Join("-", c.ToTextGroups())).ToArray();
        return RedirectToPage("/ShowRecoveryCodes");
    }

    public async Task<IActionResult> OnPostResetAuthenticatorAsync()
    {
        if (GetSubjectId() is not { } subjectId)
        {
            return RedirectToPage("/Login");
        }

        if (await userSelfService.Authenticators.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } authenticators)
        {
            return NotFound("User authenticators not found.");
        }

        if (await userSelfService.Profiles.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } profile)
        {
            return NotFound("User profile not found.");
        }

        // Remove all TOTP authenticators
        foreach (var deviceName in authenticators.TotpDeviceNames)
        {
            await userSelfService.Authenticators.TryRemoveTotpDeviceAsync(subjectId, deviceName, HttpContext.RequestAborted);
        }

        // Generate a new authenticator key
        var update = profile.ToUpdate();
        update.Set(UserAttributes.AuthenticatorKey, PlainBytesTotpKey.New().EncodeToBase32());
        await userSelfService.Profiles.TryUpdateAsync(subjectId, update.Validate(), HttpContext.RequestAborted);

        StatusMessage = "Your authenticator app has been reset. You will need to configure it again.";
        return RedirectToPage("/EnableAuthenticator");
    }

    public async Task<IActionResult> OnPostDisable2FAAsync()
    {
        if (GetSubjectId() is not { } subjectId)
        {
            return RedirectToPage("/Login");
        }

        if (await userSelfService.Authenticators.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } authenticators)
        {
            return NotFound("User authenticators not found.");
        }

        if (await userSelfService.Profiles.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } profile)
        {
            return NotFound("User profile not found.");
        }

        // Remove all TOTP authenticators
        foreach (var deviceName in authenticators.TotpDeviceNames)
        {
            await userSelfService.Authenticators.TryRemoveTotpDeviceAsync(subjectId, deviceName, HttpContext.RequestAborted);
        }

        // Clear the authenticator key by removing it
        var update = profile.ToUpdate();
        update.Remove(UserAttributes.AuthenticatorKey);
        await userSelfService.Profiles.TryUpdateAsync(subjectId, update.Validate(), HttpContext.RequestAborted);

        StatusMessage = "Two-factor authentication has been disabled. You can re-enable it at any time.";
        return RedirectToPage("/Index");
    }

    private UserSubjectId? GetSubjectId()
    {
        if (User.FindFirst(JwtClaimTypes.Subject)?.Value is not { } claim)
        {
            return null;
        }

        return UserSubjectId.Create(claim);
    }
}
