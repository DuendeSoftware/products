// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Authentication.Otp;

public readonly record struct OtpToken
{
    public OtpToken() => throw new InvalidOperationException();

    private OtpToken(UuidV7 uuid) => Uuid = uuid;

    internal UuidV7 Uuid { get; }

    public override string ToString() => Uuid.ToString();

    public static OtpToken Parse(string input) => new(UuidV7.From(Guid.Parse(input)));

    internal static OtpToken New() => new(UuidV7.New());

    internal static OtpToken Load(UuidV7 uuid) => new(uuid);
}
