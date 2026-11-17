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
public class RegisterModel(IUserProfileSelfService selfService) : PageModel
{
    [BindProperty]
    public string? GivenName { get; set; }

    [BindProperty]
    public string? FamilyName { get; set; }

    [BindProperty]
    public string? Picture { get; set; }

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";

        var subjectIdClaim = User.FindFirst(JwtClaimTypes.Subject)?.Value;
        if (string.IsNullOrEmpty(subjectIdClaim))
        {
            return RedirectToPage("./Login", new { returnUrl });
        }

        var subjectId = UserSubjectId.Create(subjectIdClaim);
        if (await selfService.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } profile)
        {
            ErrorMessage = "User account not found.";
            return Page();
        }

        Email = (string?)profile.Attributes.GetValueOrDefault(UserAttributes.Email)?.UntypedValue ?? string.Empty;

        // Pre-populate form with existing data
        if (profile.Attributes.TryGetValue(UserAttributes.GivenName, out var givenName))
        {
            GivenName = (string)givenName.UntypedValue;
        }

        if (profile.Attributes.TryGetValue(UserAttributes.FamilyName, out var familyName))
        {
            FamilyName = (string)familyName.UntypedValue;
        }

        if (profile.Attributes.TryGetValue(UserAttributes.Picture, out var picture))
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
            return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
        }

        var subjectId = UserSubjectId.Create(subjectIdClaim);

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please correct the errors below.";
            return await OnGetAsync(ReturnUrl);
        }

        if (await selfService.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } profile)
        {
            ErrorMessage = "User account not found.";
            return await OnGetAsync(ReturnUrl);
        }

        // Get email for display
        Email = (string?)profile.Attributes.GetValueOrDefault(UserAttributes.Email)?.UntypedValue ?? string.Empty;

        var update = profile.ToUpdate();

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
            ErrorMessage = "Failed to complete registration";
            return await OnGetAsync(ReturnUrl);
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

        // Redirect to return URL
        return LocalRedirect(ReturnUrl ?? "/");
    }
}
