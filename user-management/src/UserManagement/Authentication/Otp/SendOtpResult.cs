// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.UserManagement.Authentication.Otp;

public sealed record SendOtpResult
{
    private SendOtpResult() { }

    public OtpToken? Token { get; private init; }
    public TimeSpan? ExpiresAfter { get; private init; }
    public DateTimeOffset? ExpiresAtUtc { get; private init; }

    [MemberNotNullWhen(true, nameof(Token), nameof(ExpiresAfter), nameof(ExpiresAtUtc))]
    public bool Sent { get; private init; }
    public TimeSpan SendingBlockedFor { get; private init; }
    public DateTimeOffset SendingBlockedUntilUtc { get; private init; }

    internal static SendOtpResult CreateSent(
        OtpToken token,
        TimeSpan expiresAfter,
        DateTimeOffset expiresAtUtc,
        TimeSpan sendingBlockedFor,
        DateTimeOffset sendingBlockedUntilUtc) =>
        new()
        {
            Token = token,
            ExpiresAfter = expiresAfter,
            ExpiresAtUtc = expiresAtUtc,
            Sent = true,
            SendingBlockedFor = sendingBlockedFor,
            SendingBlockedUntilUtc = sendingBlockedUntilUtc,
        };

    internal static SendOtpResult CreateNotSent(
        TimeSpan sendingBlockedFor,
        DateTimeOffset sendingBlockedUntilUtc) =>
        new()
        {
            Sent = false,
            SendingBlockedFor = sendingBlockedFor,
            SendingBlockedUntilUtc = sendingBlockedUntilUtc,
        };
}
