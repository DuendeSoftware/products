// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal readonly record struct PasskeyAuthenticationChallengeId
{
    public PasskeyAuthenticationChallengeId() => throw new InvalidOperationException();

    private PasskeyAuthenticationChallengeId(UuidV7 uuid) => Uuid = uuid;

    internal UuidV7 Uuid { get; }

    public override string ToString() => Uuid.ToString();

    internal static PasskeyAuthenticationChallengeId New() => new(UuidV7.New());

    public static PasskeyAuthenticationChallengeId From(Guid value) => new(UuidV7.From(value));
}
