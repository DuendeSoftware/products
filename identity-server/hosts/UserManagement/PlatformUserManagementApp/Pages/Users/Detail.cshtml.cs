// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PlatformUserManagementApp.Pages.Users;

[Authorize]
public sealed class DetailModel(
    IUserAdmin userAdmin,
    IUserAuthenticatorsSelfService authenticatorsSelfService) : PageModel
{
    public Duende.UserManagement.Profiles.UserProfile? UserProfile { get; set; }
    public UserAuthenticators? Authenticators { get; set; }
    public IReadOnlyDictionary<AttributeGroupCode, AttributeGroup> Groups { get; set; } =
        new Dictionary<AttributeGroupCode, AttributeGroup>();
    public IReadOnlyDictionary<AttributeCode, AttributeDefinition> Schema { get; set; } =
        new Dictionary<AttributeCode, AttributeDefinition>();
    public string? Id { get; set; }

    public async Task<IActionResult> OnGetAsync(string id) => await LoadAsync(id);

    public async Task<IActionResult> OnPostRemovePasskeyAsync(string id, string credentialId)
    {
        if (!UserSubjectId.TryCreate(id, out var subjectId))
        {
            return NotFound();
        }

        if (!TryDecodeBase64(credentialId, out var bytes) ||
            !PasskeyCredentialId.TryFrom(bytes, out var passkeyId))
        {
            return BadRequest();
        }

        _ = await authenticatorsSelfService.TryRemovePasskeyAsync(subjectId, passkeyId.Value, HttpContext.RequestAborted);

        return await LoadAsync(id);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        if (!UserSubjectId.TryCreate(id, out var subjectId))
        {
            return NotFound();
        }

        _ = await userAdmin.TryRemoveAsync(subjectId, HttpContext.RequestAborted);

        return RedirectToPage("/Users/Index");
    }

    private async Task<IActionResult> LoadAsync(string id)
    {
        Id = id;
        if (!UserSubjectId.TryCreate(id, out var subjectId))
        {
            return NotFound();
        }

        UserProfile = await userAdmin.Profiles.TryGetAsync(subjectId, HttpContext.RequestAborted);
        if (UserProfile is null)
        {
            return NotFound();
        }

        Schema = UserProfile.Schema.AttributeDefinitions;
        Groups = UserProfile.Schema.Groups;
        Authenticators = await userAdmin.Authenticators.TryGetAsync(subjectId, HttpContext.RequestAborted);

        return Page();
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }
}
