// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Represents a certificate associated with a SAML Service Provider.
/// Unlike client secrets, certificates are public key material and full data is exposed.
/// </summary>
public class SamlCertificateConfiguration
{
    /// <summary>
    /// The unique identifier for this certificate. Assigned on creation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The DER-encoded certificate data as a base64 string.
    /// </summary>
    public required string Base64Data { get; set; }

    /// <summary>
    /// The intended use of the certificate. Defaults to <see cref="KeyUse.Signing"/>.
    /// </summary>
    public KeyUse Use { get; set; } = KeyUse.Signing;

    /// <summary>
    /// The certificate subject. Read-only metadata populated by the admin on Get operations.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// The certificate thumbprint. Read-only metadata populated by the admin on Get operations.
    /// </summary>
    public string? Thumbprint { get; init; }

    /// <summary>
    /// The certificate expiration date. Read-only metadata populated by the admin on Get operations.
    /// </summary>
    public DateTime? NotAfter { get; init; }
}
