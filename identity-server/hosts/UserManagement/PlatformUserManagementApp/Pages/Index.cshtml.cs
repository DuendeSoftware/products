// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesShared;

namespace PlatformUserManagementApp.Pages;

[Authorize]
public sealed class IndexModel(IUserAuthenticatorsSelfService selfService) : PageModel
{
    public IReadOnlyCollection<UserPasskey> Passkeys { get; set; } = [];

    [TempData] public string? SuccessMessage { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadPasskeysAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRemovePasskeyAsync(string credentialId)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(credentialId);
        }
        catch (FormatException)
        {
            return BadRequest();
        }

        if (!PasskeyCredentialId.TryFrom(bytes, out var passkeyId))
        {
            return BadRequest();
        }

        if (!await selfService.TryRemovePasskeyAsync(
                User.GetSubjectId(),
                passkeyId.Value,
                HttpContext.RequestAborted))
        {
            ErrorMessages.Add("Failed to remove passkey.");
            await LoadPasskeysAsync();
            return Page();
        }

        SuccessMessage = "Passkey removed.";
        return RedirectToPage();
    }

    private async Task LoadPasskeysAsync()
    {
        if (await selfService.TryGetAsync(User.GetSubjectId(), HttpContext.RequestAborted) is { } authenticators)
        {
            Passkeys = authenticators.Passkeys;
        }
    }
}
