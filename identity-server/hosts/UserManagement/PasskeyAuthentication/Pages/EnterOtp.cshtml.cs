// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesShared;

namespace PasskeyAuthentication.Pages;

public class EnterOtpModel(
    IOtpAuthenticator authenticator,
    IUserAuthenticatorsSelfService authenticatorsSelfService,
    OtpCookie otpCookie)
    : PageModel
{
    public string? Email { get; set; }

    [BindProperty][Required] public string Otp { get; set; } = string.Empty;

    [BindProperty] public string? ReturnUrl { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public IActionResult OnGet(string? returnUrl)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        if (!otpCookie.TryRead(out _, out var emailAddress))
        {
            ErrorMessages.Add("OTP not sent or expired.");
            return Page();
        }

        Email = emailAddress.ToString();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!otpCookie.TryRead(out var token, out var emailAddress))
        {
            ErrorMessages.Add("OTP expired.");
            return Page();
        }

        Email = emailAddress.ToString();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!PlainTextOtp.TryCreate(Otp, out var otp))
        {
            ModelState.AddModelError(nameof(Otp), "Invalid OTP format");
            return Page();
        }

        if (await authenticator.TryAuthenticateAsync(otp.Value, token.Value, HttpContext.RequestAborted) is not OtpAuthenticationResult.Success result)
        {
            ErrorMessages.Add("Authentication failed. Please try again.");
            return Page();
        }

        otpCookie.Clear();

        if (await authenticatorsSelfService.TryGetAsync(result.UserSubjectId, HttpContext.RequestAborted) is not { } authenticators)
        {
            ErrorMessages.Add("Failed to retrieve user details. Please try logging in again.");
            return Page();
        }

        await HttpContext.SignInAsync(ClaimsPrincipal.Create(
            authenticators.SubjectId,
            (EmailAddress)result.Address.SubjectId,
            OidcConstants.AuthenticationMethods.OneTimePassword,
            null));

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/"));
    }
}
