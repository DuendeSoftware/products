// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesShared;

namespace OtpAuthentication.Pages;

[Authorize]
public class AccountModel(IUserProfileSelfService profileSelfService) : PageModel
{
    public string? Email { get; set; }

    [BindProperty] public string? Name { get; set; }

    public string NameDescription { get; } = UserAttributes.Name.Description?.ToString() ?? string.Empty;

    [TempData] public string? SuccessMessage { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        LoadModel();

        var profile = await profileSelfService.TryGetAsync(User.GetSubjectId(), HttpContext.RequestAborted);
        Name = (string?)profile?.Attributes.GetValueOrDefault(UserAttributes.Name)?.UntypedValue;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            LoadModel();
            return Page();
        }

        AttributeValue<string>? name = null;

        if (!string.IsNullOrWhiteSpace(Name))
        {
            name = AttributeValue.Load(UserAttributes.Name, Name);
        }

        var subjectId = User.GetSubjectId();

        if (await profileSelfService.TryGetAsync(subjectId, HttpContext.RequestAborted) is not { } profile)
        {
            var schema = await profileSelfService.GetSchemaAsync(HttpContext.RequestAborted);
            var attributes = new AttributeValueCollection(schema);
            if (name is not null)
            {
                attributes.Set(name);
            }

            if (await profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), HttpContext.RequestAborted) is null)
            {
                ErrorMessages.Add("Failed to create profile. Please try again.");
                LoadModel();
                return Page();
            }

            SuccessMessage = "Profile created!";
        }
        else
        {
            var update = profile.ToUpdate();

            if (name is not null)
            {
                update.Set(name);
            }
            else
            {
                _ = update.Remove(UserAttributes.Name);
            }

            if (await profileSelfService.TryUpdateAsync(profile.SubjectId, update.Validate(), HttpContext.RequestAborted) is null)
            {
                ErrorMessages.Add("Failed to update profile. Please try again.");
                LoadModel();
                return Page();
            }

            SuccessMessage = "Profile updated!";
        }

        await HttpContext.SignInAsync(User.WithName(name?.TypedValue));
        return RedirectToPage();
    }

    private void LoadModel() => Email = User.GetEmailAddress().ToString();
}
