// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExternalAuthentication.Pages;

public class LoginModel(IAuthenticationSchemeProvider schemeProvider) : PageModel
{
    public string ReturnUrl { get; set; } = "/";
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public IEnumerable<AuthenticationScheme> ExternalProviders { get; set; } = Enumerable.Empty<AuthenticationScheme>();

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";

        var schemes = await schemeProvider.GetAllSchemesAsync();
        ExternalProviders = schemes.Where(x => x.DisplayName is not null);

        return Page();
    }

    public IActionResult OnPost(string provider, string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";

        if (string.IsNullOrWhiteSpace(provider))
        {
            ErrorMessage = "Please select a valid provider.";
            return Page();
        }

        // Redirect to external authentication provider
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/"
        };

        return Challenge(properties, provider);
    }
}
