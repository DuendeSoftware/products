// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.IntegrationTests.TestFramework;

internal static class TestCertificates
{
    public static X509Certificate2 CreateCa()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Test CA " + Guid.NewGuid(), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var ca = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        return WithNamedKey(ca);
    }

    public static X509Certificate2 CreateSelfSigned(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        return WithNamedKey(certificate);
    }

    public static X509Certificate2 CreateIssued(string subject, X509Certificate2 ca)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var caKey = ca.GetRSAPrivateKey()!;
        var serialNumber = RandomNumberGenerator.GetBytes(8);
        using var issued = request.Create(
            ca.SubjectName,
            X509SignatureGenerator.CreateForRSA(caKey, RSASignaturePadding.Pkcs1),
            ca.NotBefore,
            ca.NotAfter,
            serialNumber);
        using var withKey = issued.CopyWithPrivateKey(key);
        return WithNamedKey(withKey);
    }

    // TLS client authentication on Windows cannot use ephemeral keys, so the key must round-trip through PKCS#12
    private static X509Certificate2 WithNamedKey(X509Certificate2 certificate) =>
        X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
}
