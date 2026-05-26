// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Membership.Internal;

internal sealed class Group
{
    private Group(UuidV7 storeId, GroupId id, GroupName name, GroupDescription? description)
    {
        StoreId = storeId;
        Id = id;
        Name = name;
        Description = description;
    }

    internal UuidV7 StoreId { get; }

    internal GroupId Id { get; }

    internal GroupName Name { get; private set; }

    internal GroupDescription? Description { get; private set; }

    internal void SetName(GroupName name) => Name = name;

    internal void SetDescription(GroupDescription? description) => Description = description;

    internal static Group Create(GroupName name) =>
        new(UuidV7.New(), GroupId.New(), name, null);

    internal static Group Load(UuidV7 storeId, GroupId id, GroupName name, GroupDescription? description) =>
        new(storeId, id, name, description);
}
