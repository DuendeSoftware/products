// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesShared;

namespace MfaStepUp.Pages;

/// <summary>
/// Razor page that presents the passkey second-factor challenge after OTP verification.
/// Requires a valid authentication cookie (issued by the login page).
/// On POST (triggered after the client-side passkey ceremony succeeds), the user is also
/// signed in with the MFA authentication scheme, granting a temporary cookie for 5 minutes.
/// </summary>
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class PasskeyChallengeModel(IPasskeyCeremonies ceremonies) : PageModel
{
    [BindProperty] public string? ReturnUrl { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public IActionResult OnGet(string? returnUrl)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Verify that the user now has an MFA authentication cookie as well.
        var authResult = await HttpContext.AuthenticateAsync(MfaAuthScheme.Name);
        if (!authResult.Succeeded || authResult.Principal is null)
        {
            return Unauthorized();
        }

        var user = authResult.Principal;
        var hasMfaClaim = user.Claims.Any(c =>
            c is
            {
                Type: JwtClaimTypes.AuthenticationMethod,
                Value: OidcConstants.AuthenticationMethods.MultiFactorAuthentication
            });

        return !hasMfaClaim
            ? Unauthorized()
            : LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/"));
    }

    public async Task<IActionResult> OnPostCompleteSecondFactorAsync(
        [FromBody] PasskeyCompleteAuthenticationRequest request,
        [FromServices] TimeProvider timeProvider)
    {
        var subjectId = User.GetSubjectId();
        var result = await ceremonies.CompleteAuthenticationAsync(request, HttpContext.RequestAborted);

        if (result is not PasskeyAuthenticationCompleteResult.Success success)
        {
            return BadRequest(new { detail = "Unable to complete second-factor passkey authentication." });
        }

        if (success.UserSubjectId != subjectId)
        {
            await HttpContext.SignOutAsync(MfaAuthScheme.Name);
            return Unauthorized();
        }

        var now = timeProvider.GetUtcNow();
        Claim[] claims =
        [
            new(JwtClaimTypes.Subject, success.UserSubjectId.ToString()),
            new(JwtClaimTypes.AuthenticationMethod, OidcConstants.AuthenticationMethods.MultiFactorAuthentication),
            new(MfaAuthScheme.MfaTimeClaimType, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        ];

        var authenticatedPartial =
            new ClaimsPrincipal(
                new ClaimsIdentity(claims,
                    OidcConstants.AuthenticationMethods.MultiFactorAuthentication));

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = now.AddMinutes(5),
            IssuedUtc = now
        };

        await HttpContext.SignInAsync(MfaAuthScheme.Name, authenticatedPartial, authProperties);

        return new JsonResult(new
        {
            userVerified = success.UserVerified,
            backedUp = success.BackedUp,
            userSubjectId = success.UserSubjectId.ToString()
        });
    }
}
