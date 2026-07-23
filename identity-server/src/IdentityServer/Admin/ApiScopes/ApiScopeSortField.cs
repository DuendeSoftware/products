// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.ApiScopes;

/// <summary>
/// Fields available for sorting API scope queries.
/// </summary>
public enum ApiScopeSortField
{
    /// <summary>Sort by name.</summary>
    Name,

    /// <summary>Sort by enabled status.</summary>
    Enabled
}
