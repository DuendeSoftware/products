// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// Secret metadata. The secret value is NEVER exposed.
/// To add a new secret, use <c>IClientAdmin.CreateSecretAsync</c>.
/// To change or remove a secret, delete it and create a new one.
/// </summary>
public sealed class ClientSecretConfiguration
{
    /// <summary>
    /// The unique storage identifier for this secret.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// An optional description for this secret.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The optional expiration date for this secret.
    /// </summary>
    public DateTime? Expiration { get; init; }

    /// <summary>
    /// The secret type (e.g., <c>"SharedSecret"</c>, <c>"X509Thumbprint"</c>).
    /// </summary>
    public required string Type { get; init; }
}
