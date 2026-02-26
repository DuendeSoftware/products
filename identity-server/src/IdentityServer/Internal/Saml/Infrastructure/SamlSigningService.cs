// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

/// <summary>
/// Default implementation of <see cref="ISamlSigningService"/>.
/// </summary>
internal class SamlSigningService(
    IKeyMaterialService keyMaterialService,
    ILogger<SamlSigningService> logger) : ISamlSigningService
{
    /// <inheritdoc/>
    public async Task<X509Certificate2> GetSigningCertificateAsync(Ct ct)
    {
        var credential = await GetSigningCredentialsAsync(ct);
        if (!TryExtractCertificateFromCredential(credential, out var certificate))
        {
            throw new InvalidOperationException(
                "Signing credential must be an X509 certificate with private key.");
        }

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "Signing certificate must have a private key.");
        }

        return certificate;
    }

    /// <inheritdoc/>
    public async Task<string> GetSigningCertificateBase64Async(Ct ct)
    {
        var credential = await GetSigningCredentialsAsync(ct);
        if (TryExtractCertificateFromCredential(credential, out var certificate))
        {
            var certBytes = certificate.Export(X509ContentType.Cert);
            return Convert.ToBase64String(certBytes);
        }

        throw new InvalidOperationException(
            "Signing credential key is not an X509SecurityKey and cannot be used to extract an X509 certificate for SAML metadata.");
    }

    private async Task<SigningCredentials> GetSigningCredentialsAsync(Ct ct)
    {
        var credential = await keyMaterialService.GetSigningCredentialsAsync(null, ct);
        return credential ?? throw new InvalidOperationException("No signing credential available. Configure a signing certificate.");
    }

    private bool TryExtractCertificateFromCredential(SigningCredentials credential, [NotNullWhen(returnValue: true)] out X509Certificate2? certificate)
    {
        certificate = null;
        if (credential.Key is X509SecurityKey x509Key)
        {
            certificate = x509Key.Certificate;
            return true;
        }

        logger.SigningCredentialIsNotX509Certificate(LogLevel.Warning, credential.Key);

        return false;
    }
}
