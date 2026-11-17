// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesShared;

namespace PasskeyAuthentication.Pages;

[Authorize]
public class AccountModel(IUserAuthenticatorsSelfService selfService) : PageModel
{
    public string? Email { get; set; }

    public IReadOnlyCollection<UserPasskey> Passkeys { get; set; } = [];

    [TempData] public string? SuccessMessage { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadModelAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRemovePasskeyAsync(string credentialId)
    {
        if (!await selfService.TryRemovePasskeyAsync(
                User.GetSubjectId(),
                PasskeyCredentialId.From(Convert.FromBase64String(credentialId)),
                HttpContext.RequestAborted))
        {
            ErrorMessages.Add("Failed to remove passkey.");
            await LoadModelAsync();
            return Page();
        }

        SuccessMessage = "Passkey removed.";
        return RedirectToPage();
    }

    private async Task LoadModelAsync()
    {
        Email = User.GetEmailAddress().ToString();

        if (await selfService.TryGetAsync(User.GetSubjectId(), HttpContext.RequestAborted) is not { } authenticators)
        {
            ErrorMessages.Add("Failed to get authenticators. Please try logging in again.");
            return;
        }

        Passkeys = authenticators.Passkeys;
    }
}
