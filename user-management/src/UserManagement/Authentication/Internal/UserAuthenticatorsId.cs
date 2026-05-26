// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Authentication.Internal;

internal readonly record struct UserAuthenticatorsId
{
    public UserAuthenticatorsId() => throw new InvalidOperationException();

    private UserAuthenticatorsId(UuidV7 uuid) => Uuid = uuid;

    internal UuidV7 Uuid { get; }

    public override string ToString() => Uuid.ToString();

    internal static UserAuthenticatorsId New() => new(UuidV7.New());

    internal static UserAuthenticatorsId Load(UuidV7 uuid) => new(uuid);
}
