// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Shared helpers for generating test certificates.
/// </summary>
internal static class SamlTestCertificates
{
    /// <summary>
    /// Generates a self-signed X.509 certificate and returns the DER-encoded public cert as base64.
    /// </summary>
    internal static string GenerateSelfSignedCertBase64(string subject = "CN=Test")
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        return Convert.ToBase64String(cert.Export(X509ContentType.Cert));
    }
}
