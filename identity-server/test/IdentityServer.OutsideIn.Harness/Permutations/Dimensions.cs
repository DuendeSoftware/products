// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// The configuration dimensions a test declares it's sensitive to (design doc D34). Undeclared dimensions stay at
/// their default. Node count is deliberately not a dimension: every environment runs 2 nodes (D30, D36).
/// </summary>
[Flags]
public enum Dimensions
{
    None = 0,

    /// <summary>Which store type backs IdentityServer's stores. See <see cref="StoreType" />.</summary>
    Stores = 1,

    /// <summary>Which caching mode the nodes use. See <see cref="CachingMode" />.</summary>
    Caching = 2,

    /// <summary>Whether users are IdentityServer test users or User Management users. See <see cref="UserStore" />.</summary>
    Users = 4,

    /// <summary>Whether the server runs several spaces. See <see cref="SpacesMode" />.</summary>
    Spaces = 8,
}

/// <summary>
/// Store types in their split layouts (D29). Duende.Storage appears twice because its schema option (kept in memory
/// or stored in the database) only exists for Duende.Storage (D32), so it's folded into this dimension.
/// </summary>
public enum StoreType
{
    InMemory,
    EntityFramework,
    DuendeStorageSchemasInMemory,
    DuendeStorageSchemasInDatabase,
}

public enum CachingMode
{
    None,
    InMemory,
    Distributed,
}

public enum UserStore
{
    TestUsers,
    UserManagement,
}

public enum SpacesMode
{
    Off,
    On,
}
