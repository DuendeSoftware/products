// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.ApiResources;

/// <summary>
/// Summary representation of an API resource for list/query operations.
/// </summary>
public sealed record ApiResourceListItem
{
    /// <summary>Storage identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The unique name of the API resource.</summary>
    public required string Name { get; init; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the API resource is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Number of scopes associated with this API resource.</summary>
    public int ScopeCount { get; init; }
}
