// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Extensions methods for DPoP
/// </summary>
internal static class DPoPExtensions
{
    /// <summary>
    /// Create the value of a thumbprint-based cnf claim
    /// </summary>
    public static string CreateThumbprintCnf(this JsonWebKey jwk)
    {
        var jkt = jwk.CreateThumbprint();
        var values = new Dictionary<string, string>
        {
            { JwtClaimTypes.ConfirmationMethods.JwkThumbprint, jkt }
        };
        return JsonSerializer.Serialize(values);
    }

    /// <summary>
    /// Create the value of a thumbprint
    /// </summary>
    public static string CreateThumbprint(this JsonWebKey jwk) => Base64Url.EncodeToString(jwk.ComputeJwkThumbprint());
}
