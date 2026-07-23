// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.SamlServiceProviders;

/// <summary>
/// Filter criteria for SAML Service Provider queries.
/// </summary>
public sealed record SamlServiceProviderFilter
{
    /// <summary>Filter by entity ID (contains match).</summary>
    public string? EntityId { get; init; }

    /// <summary>Filter by display name (contains match).</summary>
    public string? DisplayName { get; init; }

    /// <summary>Filter by enabled status.</summary>
    public bool? Enabled { get; init; }
}
