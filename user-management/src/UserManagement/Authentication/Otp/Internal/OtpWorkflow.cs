// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.UserManagement.Authentication.Internal;

namespace Duende.UserManagement.Authentication.Otp.Internal;

internal sealed class OtpWorkflow
{
    private const int MaxAttempts = 5;

    // https://pages.nist.gov/800-63-4/sp800-63b.html#issued-recovery-codes
    private static readonly TimeSpan OtpExpiresAfter = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan MinTimeBetweenOtpCreation = TimeSpan.FromMinutes(1);

    private readonly List<DateTimeOffset> _attempts;

    private OtpWorkflow(
        OtpWorkflowId id,
        OtpAddress address,
        Pbkdf2HashedPassword? hashedOtp,
        OtpToken? token,
        DateTimeOffset? otpExpiresAt,
        DateTimeOffset? otpCreationBlockedUntil,
        List<DateTimeOffset> attempts)
    {
        Id = id;
        Address = address;
        HashedOtp = hashedOtp;
        Token = token;
        OtpExpiresAt = otpExpiresAt;
        OtpCreationBlockedUntil = otpCreationBlockedUntil;
        _attempts = attempts;
        Attempts = _attempts.AsReadOnly();
    }

    internal OtpWorkflow(OtpAddress address)
    {
        Id = OtpWorkflowId.New();
        Address = address;
        _attempts = [];
        Attempts = _attempts.AsReadOnly();
    }

    // immutable
    internal OtpWorkflowId Id { get; }
    internal OtpAddress Address { get; }

    // mutable
    internal Pbkdf2HashedPassword? HashedOtp { get; private set; }
    internal OtpToken? Token { get; private set; }
    internal DateTimeOffset? OtpExpiresAt { get; private set; }
    internal DateTimeOffset? OtpCreationBlockedUntil { get; private set; }
    internal IReadOnlyCollection<DateTimeOffset> Attempts { get; }

    internal bool TryCreateOtp(
        TimeProvider timeProvider,
        [NotNullWhen(true)] out PlainTextOtp? otp,
        [NotNullWhen(true)] out OtpToken? token,
        [NotNullWhen(true)] out TimeSpan? expiresAfter,
        [NotNullWhen(true)] out DateTimeOffset? expiresAtUtc,
        out TimeSpan creationBlockedFor,
        out DateTimeOffset creationBlockedUntilUtc)
    {
        // init
        otp = null;
        token = null;
        expiresAfter = null;
        expiresAtUtc = null;

        var now = timeProvider.GetUtcNow();

        // time consistency
        otp = PlainTextOtp.New();
        var hashedOtp = Pbkdf2HashedPassword.From(otp.Text);

        if (now < OtpCreationBlockedUntil)
        {
            creationBlockedFor = OtpCreationBlockedUntil.Value - now;
            creationBlockedUntilUtc = OtpCreationBlockedUntil.Value;
            return false;
        }

        token = OtpToken.New();
        expiresAfter = OtpExpiresAfter;
        expiresAtUtc = now + expiresAfter.Value;
        creationBlockedFor = MinTimeBetweenOtpCreation;
        creationBlockedUntilUtc = now + creationBlockedFor;

        HashedOtp = hashedOtp;
        Token = token;
        OtpExpiresAt = expiresAtUtc;
        OtpCreationBlockedUntil = creationBlockedUntilUtc;
        _attempts.Clear();

        return true;
    }

    internal static OtpAddress? TryAuthenticate(OtpWorkflow? authenticator, PlainTextOtp otp, TimeProvider timeProvider)
    {
        // time consistency
        if (authenticator?.HashedOtp is null)
        {
            var masterKey = Pbkdf2MasterKey.DeriveFrom(otp.Text, new Pbkdf2Inputs());
            _ = masterKey.Equals(masterKey);
            return null;
        }
        else
        {
            var now = timeProvider.GetUtcNow();

            var masterKey = Pbkdf2MasterKey.DeriveFrom(otp.Text, authenticator.HashedOtp.Inputs);
            var otpIsValid = masterKey.Equals(authenticator.HashedOtp.MasterKey);

            authenticator._attempts.Add(now);

            if (now >= authenticator.OtpExpiresAt)
            {
                return null;
            }

            if (authenticator._attempts.Count >= MaxAttempts)
            {
                return null;
            }

            if (!otpIsValid)
            {
                return null;
            }

            authenticator.HashedOtp = null;
            authenticator.Token = null;
            authenticator.OtpExpiresAt = null;
            authenticator.OtpCreationBlockedUntil = null;
            authenticator._attempts.Clear();

            return authenticator.Address;
        }
    }

    internal static OtpWorkflow Load(
        OtpWorkflowId id,
        OtpAddress address,
        Pbkdf2HashedPassword? hashedOtp,
        OtpToken? token,
        DateTimeOffset? otpExpiresAt,
        DateTimeOffset? otpCreationBlockedUntil,
        List<DateTimeOffset> attempts) =>
        new(id, address, hashedOtp, token, otpExpiresAt, otpCreationBlockedUntil, attempts);
}
