// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

[Authorize]
public class EnableAuthenticatorModel(
    IUserSelfService userSelfService,
    TotpAuthenticatorService authenticatorService) : PageModel
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public string QrCodeSvg { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "Code must be 6 or 7 characters.")]
    public string Code { get; set; } = string.Empty;

    [TempData] public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (GetUserSubjectId() is not { } userId)
        {
            return RedirectToPage("/Login");
        }

        if (await userSelfService.Authenticators.TryGetAsync(userId, HttpContext.RequestAborted) is not { } authenticators)
        {
            return NotFound("User authenticators not found.");
        }

        if (authenticators.TotpDeviceNames.Count > 0)
        {
            return RedirectToPage("/Index");
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
        if (GetUserSubjectId() is not { } subjectId)
        {
            return RedirectToPage("/Login");
        }

        if (await userSelfService.Profiles.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } profile)
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
                subjectId,
                TotpDeviceName.Default,
                key,
                totp.Value,
                HttpContext.RequestAborted))
        {
            ErrorMessage = "Verification code is invalid.";
            await LoadSharedKeyAndQrCodeUriAsync(profile);
            return Page();
        }

        return RedirectToPage("/Login");
    }

    private async Task LoadSharedKeyAndQrCodeUriAsync(UserProfile profile)
    {
        var email = profile.Attributes[UserAttributes.Email].UntypedValue?.ToString() ?? "user";
        var setup = await authenticatorService.GetOrCreateAuthenticatorSetupAsync(
            profile, email, HttpContext.RequestAborted);

        SharedKey = setup.SharedKeyFormatted;
        AuthenticatorUri = setup.AuthenticatorUri;
        QrCodeSvg = setup.QrCodeSvg;
    }

    private UserSubjectId? GetUserSubjectId()
    {
        if (User.FindFirst(JwtClaimTypes.Subject)?.Value is not { } claim)
        {
            return null;
        }

        return UserSubjectId.Create(claim);
    }
}
