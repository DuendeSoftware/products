// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Authentication.Internal.Passkeys;

internal sealed class DefaultPasskeySignInHandler : IPasskeySignInHandler
{
    public async Task<IResult> SignInAsync(HttpContext context, Authentication.UserAuthenticators user, bool userVerified, bool backedUp, Ct ct)
    {
        var email = user.OtpAddresses
            .FirstOrDefault(a => a.Channel == OtpChannel.Email)
            ?.SubjectId.ToString();

        var subjectIdString = user.SubjectId.Value;

        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Subject, subjectIdString),
            new Claim(JwtClaimTypes.AuthenticationMethod, "passkey"),
            new Claim(JwtClaimTypes.AuthenticationTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        };

        if (email is not null)
        {
            claims.Add(new Claim(JwtClaimTypes.Email, email));
        }

        var identity = new ClaimsIdentity(claims, "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            IssuedUtc = DateTimeOffset.UtcNow,
            AllowRefresh = true
        };

        await context.SignInAsync(principal, authProperties);

        return new PasskeyCompleteAuthenticationResult(userVerified, backedUp);
    }
}
