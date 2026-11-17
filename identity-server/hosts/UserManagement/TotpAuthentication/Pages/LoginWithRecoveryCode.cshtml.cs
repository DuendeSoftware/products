// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

public class LoginWithRecoveryCodeModel(
    IRecoveryCodeAuthenticator recoveryCodeAuthenticator,
    CookieAuthenticationStateService cookieAuthenticationStateService,
    ILogger<LoginWithRecoveryCodeModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Recovery Code")]
        public string RecoveryCode { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        // Ensure the user has gone through the username & password screen first
        if (cookieAuthenticationStateService.GetAuthenticationState() is null)
        {
            logger.LogWarning("Recovery code login attempted without prior authentication");
            return RedirectToPage("/Login");
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ReturnUrl = returnUrl;
            return Page();
        }

        if (cookieAuthenticationStateService.GetAuthenticationState() is not { } authenticationState)
        {
            logger.LogWarning("Recovery code login attempted without prior authentication");
            return RedirectToPage("/Login");
        }

        var userId = UserSubjectId.Create(authenticationState.UserId);

        // Strip spaces and hyphens from the recovery code
        var cleanCode = Input.RecoveryCode.Replace(" ", string.Empty).Replace("-", string.Empty);

        if (!PlainTextRecoveryCode.TryCreate(cleanCode, out var recoveryCode))
        {
            logger.LogWarning("Invalid recovery code format for user {UserId}", userId);
            ModelState.AddModelError(string.Empty, "Invalid recovery code format.");
            ReturnUrl = returnUrl;
            return Page();
        }

        // Try to authenticate with the recovery code
        var success = await recoveryCodeAuthenticator.TryAuthenticateAsync(
            userId,
            recoveryCode.Value,
            HttpContext.RequestAborted);

        if (success)
        {
            // Clear the TOTP data cookie since we're logging in
            cookieAuthenticationStateService.ClearAuthenticationState();

            // Sign in the user
            var claims = new[]
            {
                new System.Security.Claims.Claim(JwtClaimTypes.Subject, userId.ToString()),
                new System.Security.Claims.Claim(JwtClaimTypes.PreferredUserName, authenticationState.Email),
                new System.Security.Claims.Claim(JwtClaimTypes.AuthenticationMethod, "mfa") // Multi-factor authentication
            };

            var identity = new System.Security.Claims.ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new System.Security.Claims.ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = authenticationState.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            logger.LogInformation("User {UserId} logged in with a recovery code", userId);

            return LocalRedirect(returnUrl ?? authenticationState.ReturnUrl ?? "/");
        }

        logger.LogWarning("Invalid recovery code entered for user {UserId}", userId);
        ModelState.AddModelError(string.Empty, "Invalid recovery code entered.");
        ReturnUrl = returnUrl;
        return Page();
    }
}
