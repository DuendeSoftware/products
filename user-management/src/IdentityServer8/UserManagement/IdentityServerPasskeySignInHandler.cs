// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.UserManagement;

#pragma warning disable CA1812 // This class is not instantiated directly, but rather used by the DI container
internal sealed class IdentityServerPasskeySignInHandler : IPasskeySignInHandler
#pragma warning restore CA1812
{
    public async Task<IResult> SignInAsync(HttpContext context, UserAuthenticators user, bool userVerified, bool backedUp, Ct ct)
    {
        var email = user.OtpAddresses
            .FirstOrDefault(a => a.Channel == OtpChannel.Email)
            ?.SubjectId.ToString();

        var subjectIdString = user.SubjectId.Value;

        var identityServerUser = new IdentityServerUser(subjectIdString)
        {
            AdditionalClaims =
            [
                new Claim(JwtClaimTypes.AuthenticationMethod, "passkey"),
                new Claim(JwtClaimTypes.AuthenticationTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
            ]
        };

        if (email is not null)
        {
            identityServerUser.AdditionalClaims.Add(new Claim(JwtClaimTypes.Email, email));
        }

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            IssuedUtc = DateTimeOffset.UtcNow,
            AllowRefresh = true
        };

        await context.SignInAsync(identityServerUser, authProperties);

        return new PasskeyCompleteAuthenticationResult(userVerified, backedUp);
    }
}
