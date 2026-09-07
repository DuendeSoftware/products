// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Represents a certificate associated with a SAML Service Provider, as returned by admin read operations.
/// Unlike client secrets, certificates are public key material and full data is exposed.
/// Immutable read model -- to change certificates, use <see cref="SamlCertificateInput"/> via
/// <see cref="CreateSamlServiceProvider"/>/<see cref="UpdateSamlServiceProvider"/>.
/// </summary>
public sealed class SamlCertificateConfiguration
{
    /// <summary>
    /// The unique identifier for this certificate. Assigned on creation.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The DER-encoded certificate data as a base64 string.
    /// </summary>
    public required string Base64Data { get; init; }

    /// <summary>
    /// The intended use of the certificate. Defaults to <see cref="KeyUse.Signing"/>.
    /// </summary>
    public KeyUse Use { get; init; } = KeyUse.Signing;

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

    /// <summary>
    /// Creates a mutable input model from this certificate, preserving its identifier so that
    /// updates can be correlated with the existing stored certificate.
    /// </summary>
    public SamlCertificateInput ToInput() => new()
    {
        Id = Id,
        Base64Data = Base64Data,
        Use = Use
    };
}
