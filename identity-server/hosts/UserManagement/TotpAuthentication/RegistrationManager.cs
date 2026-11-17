// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace TotpAuthentication;

public class RegistrationManager(IHttpContextAccessor httpContextAccessor)
{
    public async Task SignInPartialAsync(UserSubjectId userId, string email, string registrationStep)
    {
        if (httpContextAccessor.HttpContext is null)
        {
            throw new InvalidOperationException("HttpContext is not available.");
        }

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, userId.ToString()),
            new(JwtClaimTypes.Email, email),
            new("registration", registrationStep)
        };

        // explicitly creating a ClaimsIdentity without an authentication type
        // marks this as a partial login. It's not authenticated.
        var identity = new ClaimsIdentity(claims, null);
        var principal = new ClaimsPrincipal(identity);

        await httpContextAccessor.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            });
    }

    public async Task UpdateRegistrationStepAsync(string step)
    {
        if (httpContextAccessor.HttpContext is null)
        {
            throw new InvalidOperationException("HttpContext is not available.");
        }

        if (httpContextAccessor.HttpContext.User.FindFirst(JwtClaimTypes.Subject)?.Value is not { } userIdString ||
            httpContextAccessor.HttpContext.User.FindFirst(JwtClaimTypes.Email)?.Value is not { } email)
        {
            return;
        }

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, userIdString),
            new(JwtClaimTypes.Email, email),
            new("registration", step)
        };

        // explicitly creating a ClaimsIdentity without an authentication type
        // marks this as a partial login. It's not authenticated.
        var identity = new ClaimsIdentity(claims, null);
        var principal = new ClaimsPrincipal(identity);

        await httpContextAccessor.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            });
    }

}
