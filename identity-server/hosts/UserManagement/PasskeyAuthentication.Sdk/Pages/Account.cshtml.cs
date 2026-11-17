// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesShared;

namespace PasskeyAuthentication.Sdk.Pages;

[Authorize]
public class AccountModel(IUserAuthenticatorsSelfService selfService) : PageModel
{
    public string? Email { get; set; }

    public IReadOnlyCollection<UserPasskey> Passkeys { get; set; } = [];

    public List<string> ErrorMessages { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        Email = User.GetEmailAddress().ToString();

        if (await selfService.TryGetAsync(User.GetSubjectId(), HttpContext.RequestAborted) is not { } authenticators)
        {
            ErrorMessages.Add("Failed to get authenticators. Please try logging in again.");
            return Page();
        }

        Passkeys = authenticators.Passkeys;
        return Page();
    }
}
