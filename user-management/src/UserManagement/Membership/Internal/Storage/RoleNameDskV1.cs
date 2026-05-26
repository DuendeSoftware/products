// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Membership.Internal.Storage;

internal sealed record RoleNameDskV1 : IDataStorageKey
{
    private RoleNameDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(RoleRepository.Keys.RoleName, 1);

    public string Value { get; }

    public static RoleNameDskV1 Create(RoleName name) => new(name.Value.ToUpperInvariant());
}
