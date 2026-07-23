// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// Fields available for sorting client queries.
/// </summary>
public enum ClientSortField
{
    /// <summary>Sort by client_id.</summary>
    ClientId,

    /// <summary>Sort by client name.</summary>
    ClientName,

    /// <summary>Sort by enabled status.</summary>
    Enabled
}
