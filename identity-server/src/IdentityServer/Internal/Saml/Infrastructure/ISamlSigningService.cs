// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

/// <summary>
/// Service for obtaining signing credentials for SAML operations.
/// </summary>
internal interface ISamlSigningService
{
    /// <summary>
    /// Gets the X509 certificate used for signing SAML messages.
    /// </summary>
    /// <returns>The signing certificate with private key.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no signing credential is available, when the credential is not an X509 certificate,
    /// or when the certificate does not have a private key.
    /// </exception>
    Task<X509Certificate2> GetSigningCertificateAsync();

    /// <summary>
    /// Gets the X509 certificate as a base64-encoded string for inclusion in SAML metadata.
    /// </summary>
    /// <returns>Base64-encoded certificate bytes.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no signing credential is available or when the credential is not an X509 certificate.
    /// </exception>
    Task<string> GetSigningCertificateBase64Async();
}
