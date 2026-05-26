// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal readonly record struct PasskeyRegistrationChallengeId
{
    public PasskeyRegistrationChallengeId() => throw new InvalidOperationException();

    private PasskeyRegistrationChallengeId(UuidV7 uuid) => Uuid = uuid;

    internal UuidV7 Uuid { get; }

    public override string ToString() => Uuid.ToString();

    internal static PasskeyRegistrationChallengeId New() => new(UuidV7.New());

    public static PasskeyRegistrationChallengeId From(Guid value) => new(UuidV7.From(value));
}
