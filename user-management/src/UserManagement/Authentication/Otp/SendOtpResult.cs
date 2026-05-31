// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Represents the result of attempting to send an OTP code, indicating whether the code was sent
/// or sending is currently throttled.
/// </summary>
public sealed record SendOtpResult
{
    private SendOtpResult() { }

    /// <summary>Gets the OTP token identifying the challenge session, if the OTP was sent.</summary>
    public OtpToken? Token { get; private init; }

    /// <summary>Gets how long the OTP is valid, if the OTP was sent.</summary>
    public TimeSpan? ExpiresAfter { get; private init; }

    /// <summary>Gets the UTC time at which the OTP expires, if the OTP was sent.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the OTP was successfully sent.
    /// When <c>true</c>, <see cref="Token"/>, <see cref="ExpiresAfter"/>, and <see cref="ExpiresAtUtc"/> are non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Token), nameof(ExpiresAfter), nameof(ExpiresAtUtc))]
    public bool Sent { get; private init; }

    /// <summary>Gets how long sending is blocked due to throttling.</summary>
    public TimeSpan SendingBlockedFor { get; private init; }

    /// <summary>Gets the UTC time until which sending is blocked due to throttling.</summary>
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
