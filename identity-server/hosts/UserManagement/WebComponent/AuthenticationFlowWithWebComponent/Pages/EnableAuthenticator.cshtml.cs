// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Text;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthenticationFlowWithWebComponent.Pages;

[Authorize]
public class EnableAuthenticatorModel(
    IUserSelfService userSelfService
    ) : PageModel
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "Code must be 6 or 7 characters.")]
    public string Code { get; set; } = string.Empty;

    [TempData] public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; set; }

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

        // Check if already enabled
        if (authenticators.TotpDeviceNames.Count > 0)
        {
            return RedirectToPage("/Profile");
        }

        if (await userSelfService.Profiles.TryGetAsync(userId, HttpContext.RequestAborted) is not { } profile)
        {
            return NotFound("User profile not found.");
        }

        await LoadSharedKeyAndQrCodeUriAsync(profile);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (GetUserId() is not { } userId)
        {
            return RedirectToPage("/Index");
        }

        if (await userSelfService.Profiles.TryGetAsync(userId, HttpContext.RequestAborted) is not { } profile)
        {
            return NotFound("User profile not found.");
        }

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAndQrCodeUriAsync(profile);
            return Page();
        }

        // Strip spaces and hyphens
        var cleanCode = Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        if (!PlainTextTotp.TryCreate(cleanCode, out var totp))
        {
            ErrorMessage = "Invalid code format.";
            await LoadSharedKeyAndQrCodeUriAsync(profile);
            return Page();
        }

        // Get the authenticator key
        var keyBase32 = (string?)profile.Attributes.GetValueOrDefault(UserAttributes.AuthenticatorKey)?.UntypedValue;
        if (string.IsNullOrEmpty(keyBase32))
        {
            ErrorMessage = "Authenticator key not found.";
            await LoadSharedKeyAndQrCodeUriAsync(profile);
            return Page();
        }

        var key = PlainBytesTotpKey.DecodeFromBase32(keyBase32);

        // Try to add the TOTP authenticator
        if (!await userSelfService.Authenticators.TryAddTotpDeviceAsync(
                userId,
                TotpDeviceName.Default,
                key,
                totp.Value,
                HttpContext.RequestAborted))
        {
            ErrorMessage = "Verification code is invalid.";
            await LoadSharedKeyAndQrCodeUriAsync(profile);
            return Page();
        }

        StatusMessage = "Your authenticator app has been verified.";

        // Generate recovery codes
        if (await userSelfService.Authenticators.TryCreateRecoveryCodesAsync(userId, HttpContext.RequestAborted) is { } recoveryCodes)
        {
            TempData["RecoveryCodes"] = recoveryCodes.Select(c => string.Join("-", c.ToTextGroups())).ToArray();
            return RedirectToPage("/ShowRecoveryCodes");
        }

        return RedirectToPage("/Profile");
    }

    private async Task LoadSharedKeyAndQrCodeUriAsync(UserProfile user)
    {
        var keyBase32 = (string?)user.Attributes.GetValueOrDefault(UserAttributes.AuthenticatorKey)?.UntypedValue;

        if (string.IsNullOrEmpty(keyBase32))
        {
            // Generate new key
            var key = PlainBytesTotpKey.New();
            keyBase32 = key.EncodeToBase32();

            // Update user with new authenticator key
            var userUpdate = user.ToUpdate();
            userUpdate.Set(UserAttributes.AuthenticatorKey, keyBase32);

            await userSelfService.Profiles.TryUpdateAsync(
                user.SubjectId,
                userUpdate.Validate(),
                HttpContext.RequestAborted);
        }

        SharedKey = FormatKeyToHumanReadable(keyBase32);

        var email = User.Identity?.Name ?? "user";
        AuthenticatorUri = GenerateQrCodeUri(email, keyBase32);
    }

    /// <summary>
    /// Input (raw): JBSWY3DPEHPK3PXP
    /// Output (formatted): jbsw y3dp ehpk 3pxp
    /// </summary>
    private static string FormatKeyToHumanReadable(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateQrCodeUri(string email, string unformattedKey)
    {
        var key = PlainBytesTotpKey.DecodeFromBase32(unformattedKey);
        return TotpAuthenticatorUri.Generate("Persona WebComponent Demo", email, key);
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
