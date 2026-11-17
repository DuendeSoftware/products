// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement;

namespace SamplesShared;

internal static class Extensions
{
    extension(ClaimsPrincipal)
    {
        internal static ClaimsPrincipal Create(
            UserSubjectId subjectId,
            EmailAddress emailAddress,
            string authenticationType,
            string? name)
        {
            List<Claim> claims =
            [
                new(JwtClaimTypes.Subject, subjectId.ToString()),
                new(JwtClaimTypes.Email, emailAddress.ToString()),
                new(JwtClaimTypes.EmailVerified, "true")
            ];

            if (name is not null)
            {
                claims.Add(new Claim(JwtClaimTypes.Name, name));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
        }
    }

    extension(ClaimsPrincipal principal)
    {
        internal ClaimsPrincipal WithName(string? name)
        {
            var claims = principal.Claims.Where(claim => claim.Type != JwtClaimTypes.Name).ToList();
            if (name is not null)
            {
                claims.Add(new Claim(JwtClaimTypes.Name, name));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, principal.Identity?.AuthenticationType));
        }

        internal UserSubjectId GetSubjectId() =>
            UserSubjectId.Create(principal.FindFirstValue(JwtClaimTypes.Subject)!);

        internal EmailAddress GetEmailAddress() =>
            EmailAddress.Create(principal.FindFirstValue(JwtClaimTypes.Email)!);

        internal string GetDisplayName() =>
            principal.FindFirst(JwtClaimTypes.Name)?.Value ?? principal.FindFirst(JwtClaimTypes.Email)!.Value;
    }
}
