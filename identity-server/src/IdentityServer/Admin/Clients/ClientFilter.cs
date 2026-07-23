// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// Filter criteria for client queries.
/// </summary>
public sealed record ClientFilter
{
    /// <summary>Filter by client_id (contains match).</summary>
    public string? ClientId { get; init; }

    /// <summary>Filter by client name (contains match).</summary>
    public string? ClientName { get; init; }

    /// <summary>Filter by enabled status.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Filter by grant type (clients containing this grant type).</summary>
    public string? GrantType { get; init; }

    /// <summary>Filter by allowed scope (clients containing this scope).</summary>
    public string? AllowedScope { get; init; }
}
