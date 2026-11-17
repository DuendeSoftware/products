// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using AspNetIdentityApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AspNetIdentityApp.Pages.Account;

public sealed class LoginModel(
    SignInManager<Data.ApplicationUser> signInManager,
    UserManager<Data.ApplicationUser> userManager,
    OtpService otpService,
    OtpSessionHelper otpSession) : PageModel
{
    [BindProperty][Required] public string UserName { get; set; } = string.Empty;
    [BindProperty][Required] public string Password { get; set; } = string.Empty;
    [BindProperty] public string? ReturnUrl { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public void OnGet(string? returnUrl) => ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByNameAsync(UserName);
        if (user is null)
        {
            ErrorMessages.Add("Invalid username or password.");
            return Page();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ErrorMessages.Add("Invalid username or password.");
            return Page();
        }

        // Generate and send OTP to user's email
        var otp = otpService.GenerateOtp();
        await otpService.SendOtpAsync(user.Email!, otp, HttpContext.RequestAborted);

        // Store OTP and user ID in a secure cookie for the next step
        otpSession.Write(user.Id, otp, DateTimeOffset.UtcNow.AddMinutes(10));

        return RedirectToPage("/Account/VerifyOtp", new { ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Content("~/") });
    }
}
