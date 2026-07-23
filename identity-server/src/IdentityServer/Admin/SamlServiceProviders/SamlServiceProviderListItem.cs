// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Summary representation for list/query operations.
/// </summary>
public sealed record SamlServiceProviderListItem
{
    /// <summary>Storage identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>SAML entity ID.</summary>
    public required string EntityId { get; init; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the Service Provider is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Number of configured certificates.</summary>
    public int CertificateCount { get; init; }

    /// <summary>Number of configured scopes.</summary>
    public int AllowedScopeCount { get; init; }
}
