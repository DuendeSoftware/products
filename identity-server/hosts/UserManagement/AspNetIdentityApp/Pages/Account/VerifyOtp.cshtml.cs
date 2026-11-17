// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AspNetIdentityApp.Pages.Account;

public sealed class VerifyOtpModel(
    UserManager<Data.ApplicationUser> userManager,
    SignInManager<Data.ApplicationUser> signInManager,
    OtpSessionHelper otpSession) : PageModel
{
    [BindProperty][Required][StringLength(6, MinimumLength = 6)] public string Otp { get; set; } = string.Empty;
    [BindProperty] public string? ReturnUrl { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public IActionResult OnGet(string? returnUrl)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        if (!otpSession.TryRead(out _, out _))
        {
            ErrorMessages.Add("OTP session expired. Please log in again.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!otpSession.TryRead(out var userId, out var expectedOtp))
        {
            ErrorMessages.Add("OTP session expired. Please log in again.");
            return Page();
        }

        if (!string.Equals(Otp, expectedOtp, StringComparison.Ordinal))
        {
            ErrorMessages.Add("Invalid OTP. Please try again.");
            return Page();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            ErrorMessages.Add("User not found. Please log in again.");
            return Page();
        }

        otpSession.Clear();

        // Sign in via Identity's SignInManager
        await signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToPage("/Account/EnrollPasskey", new { ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Content("~/") });
    }
}
