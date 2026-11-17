// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OtpIdentityServer.Pages.Account;

[Authorize]
public class ProfileModel(IUserProfileSelfService selfService) : PageModel
{
    [BindProperty]
    public string? GivenName { get; set; }

    [BindProperty]
    public string? FamilyName { get; set; }

    [BindProperty]
    public string? Picture { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var subjectIdClaim = User.FindFirst(JwtClaimTypes.Subject)?.Value;
        if (string.IsNullOrEmpty(subjectIdClaim))
        {
            ErrorMessage = "Unable to identify user. Please sign in again.";
            return Page();
        }

        var subjectId = UserSubjectId.Create(subjectIdClaim);
        if (await selfService.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } user)
        {
            ErrorMessage = "User account not found.";
            return Page();
        }

        Email = (string?)user.Attributes.GetValueOrDefault(UserAttributes.Email)?.UntypedValue ?? string.Empty;

        // Pre-populate form with existing data
        if (user.Attributes.TryGetValue(UserAttributes.GivenName, out var givenName))
        {
            GivenName = (string)givenName.UntypedValue;
        }

        if (user.Attributes.TryGetValue(UserAttributes.FamilyName, out var familyName))
        {
            FamilyName = (string)familyName.UntypedValue;
        }

        if (user.Attributes.TryGetValue(UserAttributes.Picture, out var picture))
        {
            Picture = (string)picture.UntypedValue;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var subjectIdClaim = User.FindFirst(JwtClaimTypes.Subject)?.Value;
        if (string.IsNullOrEmpty(subjectIdClaim))
        {
            ErrorMessage = "Unable to identify user. Please sign in again.";
            return await OnGetAsync();
        }

        var subjectId = UserSubjectId.Create(subjectIdClaim);

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please correct the errors below.";
            return await OnGetAsync();
        }

        if (await selfService.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } user)
        {
            ErrorMessage = "User account not found.";
            return await OnGetAsync();
        }

        // Get email for display
        Email = (string?)user.Attributes.GetValueOrDefault(UserAttributes.Email)?.UntypedValue ?? string.Empty;

        var update = user.ToUpdate();

        if (!string.IsNullOrWhiteSpace(GivenName))
        {
            update.Set(UserAttributes.GivenName, GivenName.Trim());
        }
        else
        {
            update.Remove(UserAttributes.GivenName);
        }

        if (!string.IsNullOrWhiteSpace(FamilyName))
        {
            update.Set(UserAttributes.FamilyName, FamilyName.Trim());
        }
        else
        {
            update.Remove(UserAttributes.FamilyName);
        }

        if (!string.IsNullOrWhiteSpace(Picture))
        {
            update.Set(UserAttributes.Picture, Picture.Trim());
        }
        else
        {
            update.Remove(UserAttributes.Picture);
        }

        if (await selfService.TryUpdateAsync(subjectId, update.Validate(), HttpContext.RequestAborted) is null)
        {
            ErrorMessage = "Failed to update profile";
            return await OnGetAsync();
        }

        // Update the name claim in the current authentication cookie
        if (User.Identity is ClaimsIdentity identity)
        {
            if (identity.FindFirst(JwtClaimTypes.Name) is { } nameClaim)
            {
                identity.RemoveClaim(nameClaim);
            }

            // NOTE: this isn't fully culture aware since some cultures put given name last
            identity.AddClaim(new Claim(JwtClaimTypes.Name, GivenName + " " + FamilyName));

            await HttpContext.SignInAsync(new ClaimsPrincipal(identity));
        }

        SuccessMessage = "Your profile has been updated successfully.";
        return await OnGetAsync();
    }
}
