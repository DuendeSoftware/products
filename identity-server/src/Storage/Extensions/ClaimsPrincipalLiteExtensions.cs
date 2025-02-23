// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityModel;
using System.Linq;
using System.Security.Claims;

namespace Duende.IdentityServer.Extensions;

/// <summary>
///  Extension methods for ClaimsPrincipalLite
/// </summary>
public static class ClaimsPrincipalLiteExtensions
{
    /// <summary>
    /// Converts a ClaimsPrincipalLite to ClaimsPrincipal
    /// </summary>
    public static ClaimsPrincipal ToClaimsPrincipal(this ClaimsPrincipalLite principal)
    {
        var claims = principal.Claims.Select(x => new Claim(x.Type, x.Value, x.ValueType ?? ClaimValueTypes.String, x.Issuer ?? ClaimsIdentity.DefaultIssuer)).ToArray();
        var id = new ClaimsIdentity(claims, principal.AuthenticationType, JwtClaimTypes.Name, JwtClaimTypes.Role);

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
                    // Leave out default values, to avoid bloat
                    ValueType = x.ValueType == ClaimValueTypes.String ? null : x.ValueType,
                    Issuer = x.Issuer == ClaimsIdentity.DefaultIssuer ? null : x.Issuer
                }).ToArray();

        return new ClaimsPrincipalLite
        {
            AuthenticationType = principal.Identity!.AuthenticationType!,
            Claims = claims
        };
    }
}
