// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExternalAuthentication.Pages;

public class IndexModel(
    IAuthenticationSchemeProvider schemeProvider,
    IUserAuthenticatorsSelfService authenticatorsSelfService,
    IUserSelfService userSelfService)
    : PageModel
{
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public IEnumerable<AuthenticationScheme> ExternalProviders { get; set; } = [];
    public IReadOnlyCollection<ExternalAuthenticatorAddress>? UserExternalAuthenticatorAddresses { get; set; }

    public async Task OnGetAsync()
    {
        var schemes = await schemeProvider.GetAllSchemesAsync();
        ExternalProviders = schemes.Where(x => x.DisplayName is not null);

        if (User.Identity is not null &&
             User.Identity.IsAuthenticated &&
             User.FindFirst(JwtClaimTypes.Subject) is { } sub &&
             await authenticatorsSelfService.TryGetAsync(
                 UserSubjectId.Create(sub.Value), HttpContext.RequestAborted) is { } authenticators)
        {
            UserExternalAuthenticatorAddresses = authenticators.ExternalAuthenticatorAddresses;
        }
    }

    public IActionResult OnPostAddAuthenticator(string provider)
    {
        if (User.Identity is null ||
            !User.Identity.IsAuthenticated ||
            User.FindFirst(JwtClaimTypes.Subject) is not { } sub)
        {
            return RedirectToPage();
        }

        // Store the subject ID in the state parameter via AuthenticationProperties
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/"
        };

        // Remember for which user and scheme we're trying to add. This will be validated
        // in the ExternalAuthenticationHandler
        properties.Items[ExternalAuthenticationHandler.UserIdStateParameter] = sub.Value;
        properties.Items[ExternalAuthenticationHandler.SchemeNameParameter] = provider;

        return Challenge(properties, provider);
    }

    public async Task<IActionResult> OnPostRemoveAuthenticatorAsync(string authenticatorName, string authenticatorSubjectId)
    {
        if (User.Identity is null ||
            !User.Identity.IsAuthenticated ||
            User.FindFirst(JwtClaimTypes.Subject) is not { } sub)
        {
            return RedirectToPage();
        }

        var userId = UserSubjectId.Create(sub.Value);

        if (await authenticatorsSelfService.TryGetAsync(userId, HttpContext.RequestAborted) is not { } user)
        {
            ErrorMessage = "User not found.";
            return RedirectToPage();
        }

        // Prevent removing the last authenticator
        if (user.ExternalAuthenticatorAddresses.Count <= 1)
        {
            ErrorMessage = "Cannot remove the last authenticator. You must have at least one authenticator linked.";
            return RedirectToPage();
        }

        var authenticator = new ExternalAuthenticatorAddress(
            ExternalAuthenticatorName.Create(authenticatorName),
            OpaqueSubjectId.Create(authenticatorSubjectId));

        if (await authenticatorsSelfService.TryRemoveExternalAuthenticatorAddressAsync(userId, authenticator,
                HttpContext.RequestAborted))
        {
            SuccessMessage = $"External authenticator '{authenticatorName}' removed successfully.";
        }
        else
        {
            ErrorMessage = $"Failed to remove external authenticator '{authenticatorName}'.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAccountAsync()
    {
        if (User.Identity is null ||
            !User.Identity.IsAuthenticated ||
            User.FindFirst(JwtClaimTypes.Subject) is not { } sub)
        {
            return RedirectToPage();
        }

        var userId = UserSubjectId.Create(sub.Value);
        await userSelfService.TryDeleteAsync(userId, HttpContext.RequestAborted);

        return SignOut(new AuthenticationProperties { RedirectUri = "/" }, "Cookies");
    }
}
