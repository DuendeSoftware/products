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
    /// <summary>
    /// Create the value of a thumbprint-based cnf claim
    /// </summary>
    /// <param name="certificate"></param>
    /// <returns></returns>
    public static string CreateThumbprintCnf(this X509Certificate2 certificate)
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
    public static string GetSha256Thumbprint(this X509Certificate2 certificate) => Base64Url.EncodeToString(certificate.GetCertHash(HashAlgorithmName.SHA256));
}
