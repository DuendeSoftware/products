// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Represents the result of attempting to send an OTP code.
/// </summary>
public abstract record SendOtpResult
{
    private SendOtpResult() { }

    /// <summary>
    /// The OTP was successfully created and dispatched.
    /// </summary>
    public sealed record Sent : SendOtpResult
    {
        internal Sent(
            OtpToken token,
            TimeSpan expiresAfter,
            DateTimeOffset expiresAtUtc,
            TimeSpan sendingBlockedFor,
            DateTimeOffset sendingBlockedUntilUtc)
        {
            Token = token;
            ExpiresAfter = expiresAfter;
            ExpiresAtUtc = expiresAtUtc;
            SendingBlockedFor = sendingBlockedFor;
            SendingBlockedUntilUtc = sendingBlockedUntilUtc;
        }

        /// <summary>Gets the OTP token identifying the challenge session.</summary>
        public OtpToken Token { get; }

        /// <summary>Gets how long the OTP is valid.</summary>
        public TimeSpan ExpiresAfter { get; }

        /// <summary>Gets the UTC time at which the OTP expires.</summary>
        public DateTimeOffset ExpiresAtUtc { get; }

        /// <summary>Gets how long sending is blocked due to throttling.</summary>
        public TimeSpan SendingBlockedFor { get; }

        /// <summary>Gets the UTC time until which sending is blocked due to throttling.</summary>
        public DateTimeOffset SendingBlockedUntilUtc { get; }
    }

    /// <summary>
    /// The OTP was not sent because sending is currently blocked (throttled).
    /// </summary>
    public sealed record Blocked : SendOtpResult
    {
        internal Blocked(TimeSpan sendingBlockedFor, DateTimeOffset sendingBlockedUntilUtc)
        {
            SendingBlockedFor = sendingBlockedFor;
            SendingBlockedUntilUtc = sendingBlockedUntilUtc;
        }

        /// <summary>Gets how long sending is blocked due to throttling.</summary>
        public TimeSpan SendingBlockedFor { get; }

        /// <summary>Gets the UTC time until which sending is blocked due to throttling.</summary>
        public DateTimeOffset SendingBlockedUntilUtc { get; }
    }

    /// <summary>
    /// The OTP could not be persisted, likely due to a concurrency conflict.
    /// </summary>
    public sealed record SaveFailed : SendOtpResult
    {
        internal static SaveFailed Instance { get; } = new();

        private SaveFailed() { }
    }
}
