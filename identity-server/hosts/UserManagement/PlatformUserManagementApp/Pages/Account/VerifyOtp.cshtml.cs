// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesShared;

namespace PlatformUserManagementApp.Pages.Account;

public sealed class VerifyOtpModel(
    IOtpAuthenticator otpAuthenticator,
    IUserProfileSelfService profileSelfService,
    OtpCookie otpCookie) : PageModel
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
            ErrorMessages.Add("OTP not sent or expired. Please sign in again.");
            return Page();
        }

        Email = emailAddress.ToString();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!otpCookie.TryRead(out var token, out var emailAddress))
        {
            ErrorMessages.Add("OTP expired. Please sign in again.");
            return Page();
        }

        Email = emailAddress.ToString();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!PlainTextOtp.TryCreate(Otp, out var otp))
        {
            ModelState.AddModelError(nameof(Otp), "Invalid OTP format.");
            return Page();
        }

        if (await otpAuthenticator.TryAuthenticateAsync(otp.Value, token.Value, HttpContext.RequestAborted) is not OtpAuthenticationResult.Success authResult)
        {
            ErrorMessages.Add("Invalid OTP. Please try again.");
            return Page();
        }

        otpCookie.Clear();

        var subjectId = authResult.UserSubjectId;
        var profile = await profileSelfService.TryGetAsync(subjectId, HttpContext.RequestAborted);
        var name = (string?)profile?.Attributes.GetValueOrDefault(ScimSchema.DisplayName.Code)?.UntypedValue;

        await HttpContext.SignInAsync(System.Security.Claims.ClaimsPrincipal.Create(
            subjectId,
            (EmailAddress)authResult.Address.SubjectId,
            OidcConstants.AuthenticationMethods.OneTimePassword,
            name));

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/"));
    }
}
