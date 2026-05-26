// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Membership.Internal;

internal sealed class Role
{
    private Role(UuidV7 storeId, RoleId id, RoleName name, RoleDescription? description)
    {
        StoreId = storeId;
        Id = id;
        Name = name;
        Description = description;
    }

    internal UuidV7 StoreId { get; }

    internal RoleId Id { get; }

    internal RoleName Name { get; private set; }

    internal RoleDescription? Description { get; private set; }

    internal void SetName(RoleName name) => Name = name;

    internal void SetDescription(RoleDescription? description) => Description = description;

    internal static Role Create(RoleName name) =>
        new(UuidV7.New(), RoleId.New(), name, null);

    internal static Role Load(UuidV7 storeId, RoleId id, RoleName name, RoleDescription? description) =>
        new(storeId, id, name, description);
}
