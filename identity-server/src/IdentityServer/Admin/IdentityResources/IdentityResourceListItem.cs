// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.IdentityResources;

/// <summary>
/// Summary representation of an identity resource for list/query operations.
/// </summary>
public sealed record IdentityResourceListItem
{
    /// <summary>Storage identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The unique name of the identity resource.</summary>
    public required string Name { get; init; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the identity resource is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }
}
