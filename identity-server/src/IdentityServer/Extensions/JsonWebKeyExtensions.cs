// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Buffers.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extensions methods for JsonWebKey
/// </summary>
internal static class JsonWebKeyExtensions
{
    extension(JsonWebKey jwk)
    {
        /// <summary>
        /// Create the value of a thumbprint-based cnf claim
        /// </summary>
        public string CreateThumbprintCnf()
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
        public string CreateThumbprint()
        {
            var jkt = Base64Url.EncodeToString(jwk.ComputeJwkThumbprint());
            return jkt;
        }
    }
}
