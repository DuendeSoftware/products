// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Profiles.Internal;

internal readonly record struct UserProfileId
{
    public UserProfileId() => throw new InvalidOperationException();

    private UserProfileId(UuidV7 uuid) => Uuid = uuid;

    internal UuidV7 Uuid { get; }

    public override string ToString() => Uuid.ToString();

    internal static UserProfileId New() => new(UuidV7.New());

    internal static UserProfileId Load(UuidV7 uuid) => new(uuid);
}
