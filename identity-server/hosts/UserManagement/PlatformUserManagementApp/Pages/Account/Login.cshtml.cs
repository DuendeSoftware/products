// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesShared;

namespace PlatformUserManagementApp.Pages.Account;

public sealed class LoginModel(
    IPasswordAuthenticator passwordAuthenticator,
    IUserAuthenticatorsSelfService authenticatorsSelfService,
    OtpCookie otpCookie,
    IOtpSender otpSender) : PageModel
{
    [BindProperty][Required] public string UserName { get; set; } = string.Empty;
    [BindProperty][Required][DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [BindProperty] public string? ReturnUrl { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public void OnGet(string? returnUrl) =>
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var passwordResult = await passwordAuthenticator.TryAuthenticateAsync(ScimSchema.UserName.Code, UserName, NonValidatedPassword.Create(Password), HttpContext.RequestAborted);
        if (passwordResult is not PasswordAuthenticationResult.Success success)
        {
            ErrorMessages.Add("Invalid username or password.");
            return Page();
        }

        var subjectId = success.UserSubjectId;
        var authenticators = await authenticatorsSelfService.TryGetAsync(subjectId, HttpContext.RequestAborted);

        var emailOtpAddress = authenticators?.OtpAddresses
            .FirstOrDefault(a => a.Channel == OtpChannel.Email);

        if (emailOtpAddress is null)
        {
            ErrorMessages.Add("No email address configured for OTP verification.");
            return Page();
        }

        switch (await otpSender.TrySendOtpAsync(emailOtpAddress, HttpContext.RequestAborted))
        {
            case SendOtpResult.Sent sent:
                otpCookie.Write(sent.Token, (EmailAddress)emailOtpAddress.SubjectId, sent.ExpiresAtUtc);
                return RedirectToPage("/Account/VerifyOtp", new { ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Content("~/") });
            case SendOtpResult.Blocked blocked:
                ErrorMessages.Add($"Too many attempts. Try again in {Math.Ceiling(blocked.SendingBlockedFor.TotalSeconds)} second(s).");
                return Page();
            default:
                ErrorMessages.Add("Failed to send OTP. Please try again.");
                return Page();
        }
    }
}
