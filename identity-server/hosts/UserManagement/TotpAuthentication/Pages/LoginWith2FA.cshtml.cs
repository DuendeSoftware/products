// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Totp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

public class LoginWith2FAModel(
    ITotpAuthenticator totpAuthenticator,
    IUserAuthenticatorsSelfService selfService,
    CookieAuthenticationStateService cookieAuthenticationStateService) : PageModel
{
    [BindProperty]
    [Required]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "Code must be 6 or 7 characters.")]
    public string Code { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? ReturnUrl { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (!cookieAuthenticationStateService.TryRetrieveAuthenticationState(out _))
        {
            return RedirectToPage("/Login");
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!cookieAuthenticationStateService.TryRetrieveAuthenticationState(out var authenticationState))
        {
            return RedirectToPage("/Login");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var subjectId = UserSubjectId.Create(authenticationState.UserId);

        if (await selfService.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } authenticators)
        {
            ErrorMessage = "User authenticators not found.";
            return Page();
        }

        // Strip spaces and hyphens from code
        var cleanCode = Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        if (!PlainTextTotp.TryCreate(cleanCode, out var totp))
        {
            ErrorMessage = "Invalid code format.";
            return Page();
        }

        var isValid = await totpAuthenticator.TryAuthenticateAsync(
            subjectId, TotpDeviceName.Default, totp.Value, HttpContext.RequestAborted);

        if (!isValid)
        {
            ErrorMessage = "Invalid authenticator code.";
            return Page();
        }

        // Clear the TOTP cookie
        cookieAuthenticationStateService.ClearAuthenticationState();

        // Sign in the user
        await SignInWithMfaAsync(authenticators.SubjectId, authenticationState.RememberMe);

        return LocalRedirect(authenticationState.ReturnUrl ?? "/");
    }

    private async Task SignInWithMfaAsync(UserSubjectId subjectId, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, subjectId.ToString()),
            new(JwtClaimTypes.AuthenticationMethod, "mfa")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });
    }
}
