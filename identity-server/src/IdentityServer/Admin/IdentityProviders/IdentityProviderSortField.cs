// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.IdentityProviders;

/// <summary>
/// Defines the fields by which identity providers can be sorted in query results.
/// </summary>
public enum IdentityProviderSortField
{
    /// <summary>Sort by scheme name.</summary>
    Scheme,

    /// <summary>Sort by display name.</summary>
    DisplayName,

    /// <summary>Sort by enabled status.</summary>
    Enabled,

    /// <summary>Sort by protocol type.</summary>
    Type,
}
