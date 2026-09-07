// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Mutable input for a SAML Service Provider certificate, used within
/// <see cref="CreateSamlServiceProvider"/> and <see cref="UpdateSamlServiceProvider"/>.
/// Certificates are managed inline as a full-replace list on update: existing certificates
/// must be submitted with their existing <see cref="Id"/> to be preserved; certificates with
/// an unset (<see cref="Guid.Empty"/>) <see cref="Id"/> are treated as new and are assigned a
/// fresh identifier by the admin implementation.
/// </summary>
public sealed class SamlCertificateInput
{
    /// <summary>
    /// The unique identifier for this certificate. Leave as <see cref="Guid.Empty"/> for new certificates;
    /// the admin implementation assigns a fresh identifier. Submit the existing value to preserve an
    /// existing certificate across an update.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The DER-encoded certificate data as a base64 string. Private key material, if present, is
    /// stripped by the admin implementation.
    /// </summary>
    public required string Base64Data { get; set; }

    /// <summary>
    /// The intended use of the certificate. Defaults to <see cref="KeyUse.Signing"/>.
    /// </summary>
    public KeyUse Use { get; set; } = KeyUse.Signing;
}
