// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extensions methods for JsonWebKey
/// </summary>
internal static class JsonWebKeyExtensions
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
    public static string CreateThumbprint(this JsonWebKey jwk)
    {
        var jkt = Base64Url.Encode(jwk.ComputeJwkThumbprint());
        return jkt;
    }
}
