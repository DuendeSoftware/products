// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesShared;

namespace PasskeyAuthenticationAs2FA.Pages;

/// <summary>
/// Razor page that presents the passkey second-factor challenge after OTP verification.
/// Requires a valid partial authentication cookie (issued by EnterOtp on OTP success).
/// On POST (triggered after the client-side passkey ceremony succeeds), promotes the partial
/// session to a full session with both OTP and passkey authentication method references.
/// </summary>
[Authorize(AuthenticationSchemes = PartialAuthScheme.Name)]
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
        if (!User.HasClaimValue(PartialAuthScheme.SecondFactorCompleteClaimType, bool.TrueString))
        {
            return Unauthorized();
        }

        // The partial cookie principal holds the user identity from the first factor (OTP)
        var subjectId = User.GetSubjectId();
        var email = User.GetEmailAddress();
        var name = User.FindFirst(JwtClaimTypes.Name)?.Value;

        // Sign out the short-lived partial cookie — it has served its purpose
        await HttpContext.SignOutAsync(PartialAuthScheme.Name);

        // Promote to a full session recording both authentication methods
        List<Claim> claims =
        [
            new(JwtClaimTypes.Subject, subjectId.ToString()),
            new(JwtClaimTypes.Email, email.ToString()),
            new(JwtClaimTypes.EmailVerified, "true"),
            new(JwtClaimTypes.AuthenticationMethod, OidcConstants.AuthenticationMethods.OneTimePassword),
            new(JwtClaimTypes.AuthenticationMethod, "passkey")
        ];

        if (name is not null)
        {
            claims.Add(new Claim(JwtClaimTypes.Name, name));
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            IssuedUtc = DateTimeOffset.UtcNow,
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/"));
    }

    public async Task<IActionResult> OnPostCompleteSecondFactorAsync(
        [FromBody] PasskeyCompleteAuthenticationRequest request)
    {
        var subjectId = User.GetSubjectId();
        var result = await ceremonies.CompleteAuthenticationAsync(request, HttpContext.RequestAborted);

        if (result is not PasskeyAuthenticationCompleteResult.Success success)
        {
            return BadRequest(new { detail = "Unable to complete second-factor passkey authentication." });
        }

        if (success.UserSubjectId != subjectId)
        {
            await HttpContext.SignOutAsync(PartialAuthScheme.Name);
            return Unauthorized();
        }

        var authenticatedPartial = User.WithClaim(PartialAuthScheme.SecondFactorCompleteClaimType, bool.TrueString);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            IssuedUtc = DateTimeOffset.UtcNow
        };

        await HttpContext.SignInAsync(PartialAuthScheme.Name, authenticatedPartial, authProperties);

        return new JsonResult(new
        {
            userVerified = success.UserVerified,
            backedUp = success.BackedUp,
            userSubjectId = success.UserSubjectId.ToString()
        });
    }
}
