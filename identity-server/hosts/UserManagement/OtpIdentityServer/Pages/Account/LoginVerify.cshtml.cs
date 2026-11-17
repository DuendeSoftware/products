// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OtpIdentityServer.Pages.Account;

public class LoginVerifyModel(
    IOtpAuthenticator otpAuthenticator,
    IUserProfileSelfService profileSelfService,
    OtpCookieService otpCookieService)
    : PageModel
{
    [BindProperty][Required] public string Code { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = "/";

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!otpCookieService.TryRetrieveOtpData(out var otpData))
        {
            // No valid OTP data found, redirect back to login
            return RedirectToPage("./Login");
        }

        Email = otpData.Email;
        ReturnUrl = otpData.ReturnUrl ?? "/";

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!otpCookieService.TryRetrieveOtpData(out var otpData))
        {
            ErrorMessage = "Session expired. Please try logging in again.";
            return RedirectToPage("./Login");
        }

        Email = otpData.Email;
        ReturnUrl = otpData.ReturnUrl ?? "/";

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

        // Clear the OTP cookie after successful authentication
        otpCookieService.ClearOtpData();

        var isNewUser = false;

        // Silently onboard the user if they don't yet have an account
        if (await profileSelfService.TryGetAsync(result.UserSubjectId, HttpContext.RequestAborted) is not { } profile)
        {
            isNewUser = true;
            // Create the user profile with the email attribute so the Register page can find it
            var schema = await profileSelfService.GetSchemaAsync(HttpContext.RequestAborted);
            var attributes = new AttributeValueCollection(schema);
            attributes.Set(UserAttributes.Email, result.Address.SubjectId.ToString());
            if (await profileSelfService.TryCreateAsync(
                    result.UserSubjectId, attributes.Validate(), HttpContext.RequestAborted) is not { } registered)
            {
                ErrorMessage = "Failed to create user profile. Please try again.";
                return Page();
            }

            profile = registered;
        }

        var identityServerUser = new IdentityServerUser(result.UserSubjectId.ToString())
        {
            AdditionalClaims =
            [
                new Claim(JwtClaimTypes.Subject, result.UserSubjectId.ToString()),
                new Claim(JwtClaimTypes.Email, result.Address.SubjectId.ToString()),
                new Claim(JwtClaimTypes.AuthenticationMethod, "otp")
            ]
        };

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            IssuedUtc = DateTimeOffset.UtcNow,
            AllowRefresh = true
        };

        // Sign in the user
        await HttpContext.SignInAsync(identityServerUser, authProperties);

        // Redirect to registration page if new user or if prompt=create was passed
        if (isNewUser || otpData.Prompt == "create")
        {
            return RedirectToPage("./Register", new { returnUrl = ReturnUrl });
        }

        // Redirect to return URL or home page
        return LocalRedirect(ReturnUrl);
    }
}
