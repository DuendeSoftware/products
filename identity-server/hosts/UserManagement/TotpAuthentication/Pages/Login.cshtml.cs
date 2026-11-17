// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

public class LoginModel(
    IPasswordAuthenticator passwordAuthenticator,
    IUserSelfService userSelfService,
    CookieAuthenticationStateService cookieAuthenticationStateService) : PageModel
{
    [BindProperty]
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    public string ReturnUrl { get; set; } = "/";

    public string? ErrorMessage { get; set; }

    public string? InfoMessage { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        if (TempData["InfoMessage"] is string infoMessage)
        {
            InfoMessage = infoMessage;
        }
    }
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!NonValidatedPassword.TryCreate(Password, out var suppliedPassword, out var passwordErrors))
        {
            ErrorMessage = string.Join(" ", passwordErrors);
            return Page();
        }

        if (await passwordAuthenticator.TryAuthenticateAsync(
                UserAttributes.UserName.Code,
                Email,
                suppliedPassword,
                HttpContext.RequestAborted) is not PasswordAuthenticationResult.Success { UserSubjectId: var userId })
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        if (await userSelfService.Authenticators.TryGetAsync(userId, HttpContext.RequestAborted) is not { } authenticators)
        {
            ErrorMessage = "User authenticators not found.";
            return Page();
        }

        if (authenticators.TotpDeviceNames.Count == 0)
        {
            var claims = new List<Claim>
            {
                new(JwtClaimTypes.Subject, authenticators.SubjectId.ToString()),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
            return Page();
        }

        if (await userSelfService.Profiles.TryGetAsync(userId, HttpContext.RequestAborted) is not { } profile)
        {
            ErrorMessage = "User profile not found.";
            return Page();
        }

        var username = profile.Attributes.GetValueOrDefault(UserAttributes.Email)?.UntypedValue.ToString() ?? Email;
        cookieAuthenticationStateService.StoreAuthenticationState(
            new AuthenticationStateData(userId.ToString(), username, RememberMe, ReturnUrl));

        return RedirectToPage("/LoginWith2FA");
    }
}
