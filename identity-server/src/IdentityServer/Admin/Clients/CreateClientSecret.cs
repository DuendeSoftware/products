// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// Represents a client secret with a plaintext value.
/// <c>SharedSecret</c> and custom secret types are hashed before storage; the built-in types
/// <see cref="IdentityServerConstants.SecretTypes.X509CertificateThumbprint"/>, <see cref="IdentityServerConstants.SecretTypes.X509CertificateName"/>,
/// <see cref="IdentityServerConstants.SecretTypes.JsonWebKey"/>, and <see cref="IdentityServerConstants.SecretTypes.X509CertificateBase64"/> are stored verbatim.
/// Read APIs are metadata-only and never expose the secret value back to callers.
/// Used by <see cref="IClientAdmin.CreateSecretAsync"/> and when creating a client with initial secrets.
/// </summary>
public sealed class CreateClientSecret
{
    /// <summary>
    /// The plaintext secret value. See the storage policy described on <see cref="CreateClientSecret"/>.
    /// </summary>
    public required string PlaintextValue { get; set; }

    /// <summary>
    /// The hash algorithm to use. Defaults to <see cref="SecretHashAlgorithm.Sha256" />.
    /// Applies only to hashed secret types; ignored for the verbatim types described on
    /// <see cref="CreateClientSecret"/>.
    /// </summary>
    public SecretHashAlgorithm? HashAlgorithm { get; set; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional expiration date.
    /// </summary>
    public DateTime? Expiration { get; set; }

    /// <summary>
    /// Secret type. Defaults to <c>SharedSecret</c>.
    /// </summary>
    public string? Type { get; set; }
}
