// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Totp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthenticationFlowWithWebComponent.Pages;

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
        if (GetUserId() is not { } userId)
        {
            return RedirectToPage("/Index");
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
        if (GetUserId() is not { } userId)
        {
            return RedirectToPage("/Index");
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
        if (await userSelfService.Authenticators.TryCreateRecoveryCodesAsync(userId, HttpContext.RequestAborted) is not
            { } recoveryCodes)
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
        if (GetUserId() is not { } userId)
        {
            return RedirectToPage("/Index");
        }

        if (await userSelfService.Authenticators.TryGetAsync(userId, HttpContext.RequestAborted) is not { } authenticators)
        {
            return NotFound("User authenticators not found.");
        }

        // Remove all TOTP authenticators
        foreach (var deviceName in authenticators.TotpDeviceNames)
        {
            await userSelfService.Authenticators.TryRemoveTotpDeviceAsync(
                userId, deviceName, HttpContext.RequestAborted);
        }

        // Generate a new authenticator key
        if (await userSelfService.Profiles.TryGetAsync(userId, HttpContext.RequestAborted) is not { } profile)
        {
            return NotFound("User profile not found.");
        }

        var userUpdate = profile.ToUpdate();
        userUpdate.Set(UserAttributes.AuthenticatorKey, PlainBytesTotpKey.New().EncodeToBase32());
        await userSelfService.Profiles.TryUpdateAsync(userId, userUpdate.Validate(), HttpContext.RequestAborted);

        StatusMessage = "Your authenticator app has been reset. You will need to configure it again.";
        return RedirectToPage("/EnableAuthenticator");
    }

    public async Task<IActionResult> OnPostDisable2FAAsync()
    {
        if (GetUserId() is not { } userId)
        {
            return RedirectToPage("/Index");
        }

        if (await userSelfService.Authenticators.TryGetAsync(userId, HttpContext.RequestAborted) is not { } authenticators)
        {
            return NotFound("User authenticators not found.");
        }

        // Remove all TOTP authenticators
        foreach (var deviceName in authenticators.TotpDeviceNames)
        {
            await userSelfService.Authenticators.TryRemoveTotpDeviceAsync(
                userId, deviceName, HttpContext.RequestAborted);
        }

        // Clear the authenticator key by removing it
        if (await userSelfService.Profiles.TryGetAsync(userId, HttpContext.RequestAborted) is not { } profile)
        {
            return NotFound("User profile not found.");
        }

        var userUpdate = profile.ToUpdate();
        userUpdate.Remove(UserAttributes.AuthenticatorKey);
        await userSelfService.Profiles.TryUpdateAsync(userId, userUpdate.Validate(), HttpContext.RequestAborted);

        StatusMessage = "Two-factor authentication has been disabled. You can re-enable it at any time.";
        return RedirectToPage("/Profile");
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
