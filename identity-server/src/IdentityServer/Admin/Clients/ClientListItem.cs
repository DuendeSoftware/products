// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// Summary representation for list/query operations.
/// </summary>
public sealed record ClientListItem
{
    /// <summary>Storage identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>OAuth client_id.</summary>
    public required string ClientId { get; init; }

    /// <summary>Display name.</summary>
    public string? ClientName { get; init; }

    /// <summary>Whether the client is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Allowed grant types.</summary>
    public IReadOnlyList<string> AllowedGrantTypes { get; init; } = [];

    /// <summary>Number of configured scopes.</summary>
    public int AllowedScopeCount { get; init; }

    /// <summary>Number of configured redirect URIs.</summary>
    public int RedirectUriCount { get; init; }
}
