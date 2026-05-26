// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Membership.Internal.Storage;

internal sealed record GroupNameDskV1 : IDataStorageKey
{
    private GroupNameDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(GroupRepository.Keys.GroupName, 1);

    public string Value { get; }

    public static GroupNameDskV1 Create(GroupName name) => new(name.Value.ToUpperInvariant());
}
