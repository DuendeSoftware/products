// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extensions methods for X509Certificate2
/// </summary>
public static class X509CertificateExtensions
{
    extension(X509Certificate2 certificate)
    {
        /// <summary>
        /// Create the value of a thumbprint-based cnf claim
        /// </summary>
        /// <returns></returns>
        public string CreateThumbprintCnf()
        {
            var hash = certificate.GetSha256Thumbprint();

            var values = new Dictionary<string, string>
            {
                { "x5t#S256", hash }
            };

            return JsonSerializer.Serialize(values);
        }

        /// <summary>
        /// Returns the SHA256 thumbprint of the certificate as a base64url encoded string
        /// </summary>
        /// <returns></returns>
        public string GetSha256Thumbprint() => Base64Url.EncodeToString(certificate.GetCertHash(HashAlgorithmName.SHA256));
    }
}
