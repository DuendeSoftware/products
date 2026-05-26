// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Membership.Internal.Storage;

internal sealed record RoleIdDskV1 : IDataStorageKey
{
    private RoleIdDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(RoleRepository.Keys.RoleId, 1);

    public string Value { get; }

    public static RoleIdDskV1 Create(RoleId id) => new(id.Value.ToUpperInvariant());
}
