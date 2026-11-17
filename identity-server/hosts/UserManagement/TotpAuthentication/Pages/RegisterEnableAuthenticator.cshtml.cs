// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

public class RegisterEnableAuthenticatorModel(
    IUserSelfService userSelfService,
    TotpAuthenticatorService totpAuthenticatorService) : PageModel
{
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public string QrCodeSvg { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "Code must be 6 or 7 characters.")]
    public string Code { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/");
        }

        if (User.Identity is null || User.FindFirst("registration")?.Value != "enable_authenticator")
        {
            return RedirectToPage("/Register");
        }

        if (User.FindFirst(JwtClaimTypes.Email)?.Value is not { } email ||
            User.FindFirst(JwtClaimTypes.Subject)?.Value is not { } userIdString)
        {
            return RedirectToPage("/Register");
        }

        Email = email;

        if (await userSelfService.Authenticators.TryGetAsync(UserSubjectId.Create(userIdString), HttpContext.RequestAborted) is not { } authenticators)
        {
            return RedirectToPage("/Register");
        }

        if (authenticators.TotpDeviceNames.Count > 0)
        {
            // user already has a totp.
            TempData["InfoMessage"] = "You already have an authenticator app configured. Please sign in here.";
            return RedirectToPage("/Login");
        }

        if (await userSelfService.Profiles.TryGetAsync(UserSubjectId.Create(userIdString), HttpContext.RequestAborted) is not { } profile)
        {
            return RedirectToPage("/Register");
        }

        await LoadSharedKeyAndQrCodeUriAsync(profile);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity is null || User.FindFirst("registration")?.Value != "enable_authenticator")
        {
            return RedirectToPage("/Register");
        }

        if (User.FindFirst(JwtClaimTypes.Email)?.Value is not { } email ||
            User.FindFirst(JwtClaimTypes.Subject)?.Value is not { } userIdString)
        {
            return RedirectToPage("/Register");
        }

        Email = email;
        var userId = UserSubjectId.Create(userIdString);

        if (!ModelState.IsValid)
        {
            if (await userSelfService.Profiles.TryGetAsync(userId, HttpContext.RequestAborted) is { } profile)
            {
                await LoadSharedKeyAndQrCodeUriAsync(profile);
            }

            return Page();
        }

        if (await userSelfService.Profiles.TryGetAsync(userId, HttpContext.RequestAborted) is not { } profileForAuth)
        {
            return RedirectToPage("/Register");
        }

        var cleanCode = Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        if (!PlainTextTotp.TryCreate(cleanCode, out var totp))
        {
            ErrorMessage = "Invalid code format.";
            await LoadSharedKeyAndQrCodeUriAsync(profileForAuth);
            return Page();
        }

        var keyBase32 = (string?)profileForAuth.Attributes.GetValueOrDefault(UserAttributes.AuthenticatorKey)?.UntypedValue;
        if (string.IsNullOrEmpty(keyBase32))
        {
            ErrorMessage = "Authenticator key not found.";
            await LoadSharedKeyAndQrCodeUriAsync(profileForAuth);
            return Page();
        }

        var key = PlainBytesTotpKey.DecodeFromBase32(keyBase32);

        if (!await userSelfService.Authenticators.TryAddTotpDeviceAsync(
                userId,
                TotpDeviceName.Default,
                key,
                totp.Value,
                HttpContext.RequestAborted))
        {
            ErrorMessage = "Verification code is invalid.";
            await LoadSharedKeyAndQrCodeUriAsync(profileForAuth);
            return Page();
        }

        // Registration complete. Sign out and force the user to sign in with the new authenticator.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["InfoMessage"] = "Registration Complete, please login to access the application.";
        return RedirectToPage("/Login");
    }

    private async Task LoadSharedKeyAndQrCodeUriAsync(UserProfile profile)
    {
        var email = profile.Attributes.GetValueOrDefault(UserAttributes.Email)?.UntypedValue.ToString() ?? Email;
        var setup = await totpAuthenticatorService.GetOrCreateAuthenticatorSetupAsync(
            profile, email, HttpContext.RequestAborted);

        SharedKey = setup.SharedKeyFormatted;
        AuthenticatorUri = setup.AuthenticatorUri;
        QrCodeSvg = setup.QrCodeSvg;
    }
}
