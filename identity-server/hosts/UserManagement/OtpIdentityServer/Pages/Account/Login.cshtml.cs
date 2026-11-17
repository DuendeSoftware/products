// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OtpIdentityServer.Pages.Account;

public class LoginModel(IOtpSender otpSender, OtpCookieService otpCookieService) : PageModel
{
    [BindProperty][Required] public string Email { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = "/";

    public string? Prompt { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? prompt = null)
    {
        Prompt = prompt;

        if (otpCookieService.TryRetrieveOtpData(out var otpData))
        {
            ErrorMessage =
                $"You currently have a pending OTP request. You can request a new OTP after {otpData.ExpiresAtUtc.ToLocalTime()}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null, string? prompt = null)
    {
        ReturnUrl = returnUrl ??= Url.Content("~/");
        Prompt = prompt;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Try send an OTP to the email address
        switch (await otpSender.TrySendOtpAsync(new OtpAddress(OtpChannel.Email, EmailAddress.Create(Email)), HttpContext.RequestAborted))
        {
            case SendOtpResult.Sent sent:
                // Store OTP data in secure cookie (include prompt parameter)
                otpCookieService.StoreOtpData(otpData: new OtpData(Email, sent.Token.ToString(), sent.ExpiresAtUtc, returnUrl, prompt));
                break;
            case SendOtpResult.Blocked blocked:
                ErrorMessage = $"Failed to send email to confirm your account. Try again in {Math.Ceiling(blocked.SendingBlockedFor.TotalSeconds)} second(s).";
                return Page();
            default:
                ErrorMessage = "Failed to send email to confirm your account.";
                return Page();
        }

        // Redirect to LoginVerify page
        return RedirectToPage("./LoginVerify");
    }
}
