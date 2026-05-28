// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.KeyManagement;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Saml.Infrastructure;

/// <summary>
/// Default implementation of <see cref="ISamlSigningService"/>.
/// </summary>
internal sealed class SamlSigningService(
    IKeyMaterialService keyMaterialService,
    ISaml2IssuerNameService saml2IssuerNameService,
    IKeyManager keyManager,
    RsaCertificateFactory rsaCertificateFactory) : ISamlSigningService
{
    /// <inheritdoc/>
    /// <remarks>
    /// When the signing credential is a raw <see cref="RsaSecurityKey"/>, this method returns
    /// a cached <see cref="X509Certificate2"/> wrapping the key. The caller must NOT dispose
    /// the returned certificate — it is owned by the cache.
    /// </remarks>
    public async Task<X509Certificate2> GetSigningCertificateAsync(Ct ct)
    {
        var credential = await GetSigningCredentialsAsync(ct);

        if (credential.Key is X509SecurityKey x509Key)
        {
            var cert = x509Key.Certificate;
            if (!cert.HasPrivateKey)
            {
                throw new InvalidOperationException("Signing certificate must have a private key.");
            }

            return cert;
        }

        if (credential.Key is RsaSecurityKey rsaKey)
        {
            return await GetOrCreateCertificateAsync(rsaKey, credential, ct);
        }

        throw new InvalidOperationException(
            "Signing credential must be an X509 certificate or RSA key with private key.");
    }

    /// <inheritdoc/>
    public async Task<string> GetSigningCertificateBase64Async(Ct ct)
    {
        var cert = await GetSigningCertificateAsync(ct);
        var certBytes = cert.Export(X509ContentType.Cert);
        return Convert.ToBase64String(certBytes);
    }

    private async Task<X509Certificate2> GetOrCreateCertificateAsync(
        RsaSecurityKey rsaKey, SigningCredentials credential, Ct ct)
    {
        var issuerName = await saml2IssuerNameService.GetCurrentAsync(ct);

        var keyId = credential.Kid ?? rsaKey.KeyId;
        if (string.IsNullOrEmpty(keyId))
        {
            throw new InvalidOperationException(
                "Cannot generate an X509 certificate for SAML signing: the signing key has no KeyId. " +
                "Configure a KeyId on the RsaSecurityKey or use an X509 certificate directly.");
        }

        var keys = await keyManager.GetCurrentKeysAsync(ct);
        var container = keys.FirstOrDefault(k => k.Id == keyId);
        if (container is null)
        {
            throw new InvalidOperationException(
                "Cannot auto-wrap a manually registered RSA key as an X509 certificate for SAML signing. " +
                "Use an X509 certificate directly or enable automatic key management.");
        }

        return rsaCertificateFactory.GetCertificate(rsaKey, keyId, issuerName, container.Created);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<X509Certificate2>> GetAllSigningCertificatesAsync(Ct ct)
    {
        var allCredentials = await keyMaterialService.GetAllSigningCredentialsAsync(ct);
        var certificates = new List<X509Certificate2>();

        foreach (var credential in allCredentials)
        {
            if (credential.Key is X509SecurityKey x509Key)
            {
                certificates.Add(x509Key.Certificate);
            }
            else if (credential.Key is RsaSecurityKey rsaKey)
            {
                var cert = await GetOrCreateCertificateAsync(rsaKey, credential, ct);
                certificates.Add(cert);
            }
        }

        return certificates;
    }

    private async Task<SigningCredentials> GetSigningCredentialsAsync(Ct ct)
    {
        var credential = await keyMaterialService.GetSigningCredentialsAsync(null, ct);
        return credential ?? throw new InvalidOperationException("No signing credential available. Configure a signing certificate.");
    }
}
