// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

public class RegisterModel(IOtpSender otpSender, OtpCookieService otpCookieService) : PageModel
{
    [BindProperty]
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/");
        }

        if (otpCookieService.TryRetrieveOtpData(out var otpData))
        {
            ErrorMessage =
                $"You currently have a pending OTP request. You can request a new OTP after {otpData.ExpiresAtUtc.ToLocalTime()}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        switch (await otpSender.TrySendOtpAsync(new OtpAddress(OtpChannel.Email, EmailAddress.Create(Email)), HttpContext.RequestAborted))
        {
            case SendOtpResult.Sent sent:
                otpCookieService.StoreOtpData(new OtpData(Email, sent.Token.ToString(), sent.ExpiresAtUtc, null));
                break;
            case SendOtpResult.Blocked blocked:
                ErrorMessage = $"Failed to send verification email. Try again in {Math.Ceiling(blocked.SendingBlockedFor.TotalSeconds)} second(s).";
                return Page();
            default:
                ErrorMessage = "Failed to send verification email.";
                return Page();
        }

        return RedirectToPage("/RegisterVerify");
    }
}
