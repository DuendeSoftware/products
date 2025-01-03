// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq;
using System.Security.Claims;

namespace Duende.Bff;

internal static class ClaimsLiteExtensions
{
    /// <summary>
    /// Converts a ClaimsPrincipalLite to ClaimsPrincipal
    /// </summary>
    public static ClaimsPrincipal ToClaimsPrincipal(this ClaimsPrincipalLite principal)
    {
        var claims = principal.Claims.Select(x => new Claim(x.Type, x.Value, x.ValueType ?? ClaimValueTypes.String))
            .ToArray();
        var id = new ClaimsIdentity(claims, principal.AuthenticationType, principal.NameClaimType,
            principal.RoleClaimType);

        return new ClaimsPrincipal(id);
    }

    /// <summary>
    /// Converts a ClaimsPrincipal to ClaimsPrincipalLite
    /// </summary>
    public static ClaimsPrincipalLite ToClaimsPrincipalLite(this ClaimsPrincipal principal)
    {
        var claims = principal.Claims.Select(
            x => new ClaimLite
            {
                Type = x.Type,
                Value = x.Value,
                ValueType = x.ValueType == ClaimValueTypes.String ? null : x.ValueType
            }).ToArray();

        return new ClaimsPrincipalLite
        {
            AuthenticationType = principal.Identity!.AuthenticationType,
            NameClaimType = principal.Identities.First().NameClaimType,
            RoleClaimType = principal.Identities.First().RoleClaimType,
            Claims = claims
        };
    }
}