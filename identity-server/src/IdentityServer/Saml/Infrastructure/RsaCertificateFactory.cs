// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Concurrent;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Duende.IdentityServer.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Saml.Infrastructure;

/// <summary>
/// Singleton factory that creates and caches X509 certificates generated from RSA keys.
/// Caching avoids repeated RSA self-sign operations on every SAML request.
/// </summary>
/// <remarks>
/// The cached <see cref="X509Certificate2"/> instances are never disposed — they live for the
/// lifetime of the application. This is intentional: the cache holds a small, bounded number
/// of entries (one per signing key), and the native handles are needed for the entire
/// application lifetime. This mirrors the existing <c>X509KeyContainer</c> pattern.
/// The cache is keyed by key ID only (not issuer name) to prevent unbounded growth from
/// varying Host headers. The issuer name only affects the certificate's CN, which is cosmetic —
/// SAML SPs validate signatures against key material, not the certificate subject.
/// A <see cref="MaxCacheSize"/> bound is retained as defense-in-depth against misconfiguration.
/// </remarks>
#pragma warning disable CA1001 // Type owns disposable field(s) — X509Certificate2 instances are intentionally long-lived
internal sealed class RsaCertificateFactory(KeyManagementOptions keyManagementOptions)
#pragma warning restore CA1001
{
    /// <summary>
    /// Maximum number of cached certificates. Defense-in-depth against misconfiguration —
    /// under normal operation the cache holds one entry per active signing key (typically 1-2).
    /// </summary>
    internal const int MaxCacheSize = 10;

    private readonly ConcurrentDictionary<string, X509Certificate2> _cache = new();

    /// <summary>
    /// Gets an X509 certificate wrapping the given RSA key.
    /// Thread-safe. The returned certificate must NOT be disposed by the caller.
    /// </summary>
    /// <param name="rsaKey">The RSA security key to wrap.</param>
    /// <param name="keyId">The key identifier (used for cache key and certificate serial).</param>
    /// <param name="issuerName">The issuer name (used for the certificate subject DN).</param>
    /// <param name="created">The key's creation time, used as the certificate's NotBefore date.</param>
    public X509Certificate2 GetCertificate(RsaSecurityKey rsaKey, string keyId, string issuerName, DateTime created)
    {
        if (_cache.Count >= MaxCacheSize)
        {
            // Don't cache — create a one-off certificate to avoid unbounded memory growth.
            return CreateCertificate(rsaKey, keyId, issuerName, created);
        }

        return _cache.GetOrAdd(keyId, _ => CreateCertificate(rsaKey, keyId, issuerName, created));
    }

    private X509Certificate2 CreateCertificate(RsaSecurityKey rsaKey, string keyId, string issuerName, DateTime created)
    {
        var lifetime = keyManagementOptions.KeyRetirementAge;

        var (rsa, owned) = GetRsaFromKey(rsaKey);
        try
        {
            using var certWithoutKey = CreateCertificateFromRsaKey(rsa, keyId, issuerName, created, lifetime);
            // CertificateRequest.Create() returns a cert without a private key.
            // Attach the private key so the cert can be used for signing.
            return certWithoutKey.CopyWithPrivateKey(rsa);
        }
        finally
        {
            if (owned)
            {
                rsa.Dispose();
            }
        }
    }

    private static (RSA Rsa, bool Owned) GetRsaFromKey(RsaSecurityKey key)
    {
        if (key.Rsa is not null)
        {
            return (key.Rsa, Owned: false);
        }

        var rsa = RSA.Create();
        rsa.ImportParameters(key.Parameters);
        return (rsa, Owned: true);
    }

    private static byte[] DeriveSerial(string keyId)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(keyId), hash);
        var serial = hash[..8].ToArray();
        serial[0] &= 0x7F; // RFC 5280 §4.1.2.2: serial MUST be a positive integer
        return serial;
    }

    private static X509Certificate2 CreateCertificateFromRsaKey(
        RSA rsa, string keyId, string issuerName, DateTime created, TimeSpan lifetime)
    {
        // Build the Subject DN using ASN.1 directly to avoid DN injection via string parsing.
        // The X500DistinguishedName string constructor is vulnerable to injection when the
        // issuer name contains quotes, commas, or other RFC 2253 special characters.
        var distinguishedName = BuildDistinguishedName(issuerName);

        var request = new CertificateRequest(
            distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // DigitalSignature only — no EKU needed for SAML signing certs.
        // Marked critical per RFC 5280 §4.2.1.3 so relying parties enforce the constraint.
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        var generator = X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1);
        var serial = DeriveSerial(keyId);

        return request.Create(
            distinguishedName,
            generator,
            new DateTimeOffset(created, TimeSpan.Zero),
            new DateTimeOffset(created.Add(lifetime), TimeSpan.Zero),
            serial);
    }

    /// <summary>
    /// Builds an X500DistinguishedName with a single CN attribute using ASN.1 encoding directly,
    /// avoiding the string-based constructor which is vulnerable to DN injection.
    /// </summary>
    private static X500DistinguishedName BuildDistinguishedName(string commonName)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();                                          // RDNSequence
        writer.PushSetOf();                                             // RelativeDistinguishedName
        writer.PushSequence();                                          // AttributeTypeAndValue
        writer.WriteObjectIdentifier("2.5.4.3");                        // OID for CN
        writer.WriteCharacterString(UniversalTagNumber.UTF8String, commonName);
        writer.PopSequence();
        writer.PopSetOf();
        writer.PopSequence();
        return new X500DistinguishedName(writer.Encode());
    }
}
