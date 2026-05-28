// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Associates an X.509 certificate with its intended use for a SAML Service Provider.
/// </summary>
public sealed class ServiceProviderCertificate
{
    /// <summary>
    /// Gets or sets the X.509 certificate.
    /// </summary>
    public X509Certificate2 Certificate { get; set; } = default!;

    /// <summary>
    /// Gets or sets the intended use of the certificate.
    /// Defaults to <see cref="KeyUse.Signing"/>.
    /// </summary>
    public KeyUse Use { get; set; } = KeyUse.Signing;
}
