// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TotpAuthentication.Pages;

public class RegisterSetPasswordModel(
    IUserSelfService userSelfService,
    RegistrationManager registrationManager) : PageModel
{
    [BindProperty]
    [Required]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least {2} characters long.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/");
        }

        if (User.Identity is null || User.FindFirst("registration")?.Value != "set_password")
        {
            return RedirectToPage("/Register");
        }

        Email = User.FindFirst(JwtClaimTypes.Email)?.Value ?? string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity is null || User.FindFirst("registration")?.Value != "set_password")
        {
            return RedirectToPage("/Register");
        }

        if (User.FindFirst(JwtClaimTypes.Email)?.Value is not { } email ||
            User.FindFirst(JwtClaimTypes.Subject)?.Value is not { } subjectIdString)
        {
            return RedirectToPage("/Register");
        }

        Email = email;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var subjectId = UserSubjectId.Create(subjectIdString);

        if (await userSelfService.Authenticators.TryGetAsync(subjectId, HttpContext.RequestAborted) is { } existingUser)
        {
            // Different flow for when the user already exists
            if (existingUser.HasPassword)
            {
                if (await userSelfService.Authenticators.TryValidatePasswordAsync(subjectId, Password, HttpContext.RequestAborted)
                    is not PasswordCreationResult.Success { Password: var resetPwd })
                {
                    ErrorMessage = "Password does not meet requirements.";
                    return Page();
                }

                var resetPassword = await userSelfService.Authenticators.TryResetPasswordAsync(
                    subjectId, resetPwd, HttpContext.RequestAborted);

                if (!resetPassword)
                {
                    ErrorMessage = "Failed to set password.";
                    return Page();
                }

                // Update registration step to show recovery codes
                await registrationManager.UpdateRegistrationStepAsync("show_recovery_codes");

                return RedirectToPage("/RegisterShowRecoveryCodes");
            }

            // User exists but doesn't have a password, update username and set password
            if (await userSelfService.Profiles.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } profile)
            {
                ErrorMessage = "Failed to get user profile.";
                return Page();
            }

            var attributes = profile.ToUpdate();
            attributes.Set(UserAttributes.UserName, Email);
            if (await userSelfService.Profiles.TryUpdateAsync(profile.SubjectId, attributes.Validate(), HttpContext.RequestAborted) is null)
            {
                ErrorMessage = "Failed to set username.";
                return Page();
            }

            if (await userSelfService.Authenticators.TryValidatePasswordAsync(subjectId, Password, HttpContext.RequestAborted)
                is not PasswordCreationResult.Success { Password: var setPwd })
            {
                ErrorMessage = "Password does not meet requirements.";
                return Page();
            }

            if (!await userSelfService.Authenticators.TrySetPasswordAsync(
                    subjectId, setPwd, HttpContext.RequestAborted))
            {
                ErrorMessage = "Failed to set password.";
                return Page();
            }

            // Update registration step to show recovery codes
            await registrationManager.UpdateRegistrationStepAsync("show_recovery_codes");

            return RedirectToPage("/RegisterShowRecoveryCodes");
        }

        // User doesn't exist - this shouldn't happen in normal flow
        ErrorMessage = "User not found. Please start the registration process again.";
        return RedirectToPage("/Register");
    }
}
