// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.ApiScopes;

/// <summary>
/// Summary representation of an API scope for list/query operations.
/// </summary>
public sealed record ApiScopeListItem
{
    /// <summary>Storage identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The unique name of the API scope.</summary>
    public required string Name { get; init; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the API scope is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }
}
