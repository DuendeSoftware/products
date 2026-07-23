// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.IdentityProviders;

/// <summary>
/// Filter criteria for querying identity providers. All properties are optional —
/// <see langword="null"/> means no filter on that field.
/// </summary>
public sealed record IdentityProviderFilter
{
    /// <summary>
    /// Filters by scheme name (contains match, case-insensitive).
    /// </summary>
    public string? Scheme { get; init; }

    /// <summary>
    /// Filters by display name (contains match, case-insensitive).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Filters by enabled status (exact match). <see langword="null"/> returns all providers.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Filters by protocol type (contains match, case-insensitive).
    /// </summary>
    public string? Type { get; init; }
}
