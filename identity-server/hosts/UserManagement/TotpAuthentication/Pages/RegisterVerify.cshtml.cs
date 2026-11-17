// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

public class RegisterVerifyModel(
    IOtpAuthenticator otpAuthenticator,
    IUserAuthenticatorsSelfService selfService,
    OtpCookieService otpCookieService,
    RegistrationManager registrationManager) : PageModel
{
    [BindProperty]
    [Required]
    public string Code { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/");
        }

        if (!otpCookieService.TryRetrieveOtpData(out var otpData))
        {
            return RedirectToPage("/Register");
        }

        Email = otpData.Email;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!otpCookieService.TryRetrieveOtpData(out var otpData))
        {
            ErrorMessage = "Session expired. Please try registering again.";
            return RedirectToPage("/Register");
        }

        Email = otpData.Email;

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please enter a valid verification code.";
            return Page();
        }

        if (await otpAuthenticator.TryAuthenticateAsync(
                PlainTextOtp.Create(Code),
                OtpToken.Create(otpData.Token),
                HttpContext.RequestAborted) is not OtpAuthenticationResult.Success result)
        {
            ErrorMessage = "Invalid or expired verification code. Please try again.";
            return Page();
        }

        otpCookieService.ClearOtpData();

        if (await selfService.TryGetAsync(result.UserSubjectId, HttpContext.RequestAborted) is not { } authenticators)
        {
            ErrorMessage = "Failed to retrieve user details. Please try logging in again.";
            return Page();
        }

        // User already exists - check if they have completed onboarding
        if (authenticators.TotpDeviceNames.Count > 0)
        {
            // User has TOTP enabled, which means onboarding is complete
            // Redirect them to login instead
            TempData["InfoMessage"] = "This email is already registered. Please log in.";
            return RedirectToPage("/Login");
        }

        await registrationManager.SignInPartialAsync(result.UserSubjectId, result.Address.SubjectId.ToString(), "set_password");

        return RedirectToPage("/RegisterSetPassword");
    }
}
