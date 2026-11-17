// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PasskeyAuthentication.TrustPolicies;
using SamplesShared;

namespace PasskeyAuthentication.Pages;

public class LoginModel(FidoMdsToggle fidoMdsToggle, IOtpSender otpSender, OtpCookie otpCookie) : PageModel
{
    public bool FidoMdsEnabled => fidoMdsToggle.Enabled;

    [BindProperty][Required] public string Email { get; set; } = string.Empty;

    [BindProperty] public string? ReturnUrl { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public void OnGet(string? returnUrl) => ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!EmailAddress.TryCreate(Email, out var email))
        {
            ModelState.AddModelError(nameof(Email), "Invalid email format");
            return Page();
        }

        switch (await otpSender.TrySendOtpAsync(new OtpAddress(OtpChannel.Email, email), HttpContext.RequestAborted))
        {
            case SendOtpResult.Sent sent:
                otpCookie.Write(sent.Token, email, sent.ExpiresAtUtc);
                return RedirectToPage("/EnterOtp", new { ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Content("~/") });
            case SendOtpResult.Blocked blocked:
                ErrorMessages.Add($"Too many attempts. Try again in {Math.Ceiling(blocked.SendingBlockedFor.TotalSeconds)} second(s).");
                return Page();
            default:
                ErrorMessages.Add("Failed to send OTP. Please try again.");
                return Page();
        }
    }

    public IActionResult OnPostToggleFidoMds(bool enabled)
    {
        fidoMdsToggle.Enabled = enabled;
        return RedirectToPage();
    }
}
