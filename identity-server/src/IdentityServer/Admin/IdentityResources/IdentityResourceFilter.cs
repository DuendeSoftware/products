// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.IdentityResources;

/// <summary>
/// Filter criteria for identity resource queries.
/// </summary>
public sealed record IdentityResourceFilter
{
    /// <summary>Filter by name (contains match).</summary>
    public string? Name { get; init; }

    /// <summary>Filter by enabled status.</summary>
    public bool? Enabled { get; init; }
}
