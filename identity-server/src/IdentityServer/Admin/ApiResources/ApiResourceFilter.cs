// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.ApiResources;

/// <summary>
/// Filter criteria for API resource queries.
/// </summary>
public sealed record ApiResourceFilter
{
    /// <summary>Filter by name (contains match).</summary>
    public string? Name { get; init; }

    /// <summary>Filter by enabled status.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Filter by scope name (API resources containing this scope).</summary>
    public string? Scope { get; init; }
}
