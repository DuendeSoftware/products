// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// Represents a client secret with a plaintext value that will be hashed before storage.
/// Used both when creating a client with initial secrets and when adding secrets via
/// <see cref="IClientAdmin.CreateSecretAsync"/>.
/// </summary>
public class CreateClientSecret
{
    /// <summary>
    /// The plaintext secret value. The value is hashed before storage and is never exposed by reads.
    /// </summary>
    public required string PlaintextValue { get; set; }

    /// <summary>
    /// The hash algorithm to use. Defaults to <see cref="SecretHashAlgorithm.Sha256" />.
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
