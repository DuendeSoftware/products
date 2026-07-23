// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.IdentityResources;

/// <summary>
/// Fields available for sorting identity resource queries.
/// </summary>
public enum IdentityResourceSortField
{
    /// <summary>Sort by name.</summary>
    Name,

    /// <summary>Sort by enabled status.</summary>
    Enabled
}
