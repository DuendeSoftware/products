// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Membership.Internal.Storage;

internal sealed record GroupIdDskV1 : IDataStorageKey
{
    private GroupIdDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(GroupRepository.Keys.GroupId, 1);

    public string Value { get; }

    public static GroupIdDskV1 Create(GroupId id) => new(id.Value.ToUpperInvariant());
}
