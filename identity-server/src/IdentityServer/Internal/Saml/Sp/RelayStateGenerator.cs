// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Generator of secure random keys..
    /// </summary>
    static class SecureKeyGenerator
    {
        /// <summary>
        /// Create a unique random string with a cryptographically secure
        /// random function.
        /// </summary>
        /// <returns>Random string 56-chars string</returns>
        public static string CreateRelayState()
        {
            // 16 is considered secure, but Base64 pads 16 bytes so
            // use 18 to make it even with Base64 that encodes multiples 
            // of 3 bytes)
            var bytes = new byte[18];
            RandomNumberGenerator.Fill(bytes);

            return Convert.ToBase64String(bytes)
                .Replace('/', '-')
                .Replace('+', '_');
        }


    }
}
