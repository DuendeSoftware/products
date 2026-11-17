// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;

namespace PasskeyAuthenticationAs2FA;

internal static class SecondFactorExtensions
{
    extension(ClaimsPrincipal principal)
    {
        internal ClaimsPrincipal WithClaim(string claimType, string claimValue)
        {
            var claims = principal.Claims
                .Where(claim => claim.Type != claimType)
                .ToList();

            claims.Add(new Claim(claimType, claimValue));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, principal.Identity?.AuthenticationType));
        }

        internal bool HasClaimValue(string claimType, string claimValue) =>
            principal.Claims.Any(claim => claim.Type == claimType && claim.Value == claimValue);
    }
}
