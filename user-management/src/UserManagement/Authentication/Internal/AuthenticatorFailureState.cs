// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal;

internal sealed class AuthenticatorFailureState
{
    internal int FailedAttemptCount { get; private set; }
    internal DateTimeOffset? LastFailedAtUtc { get; private set; }
    internal List<DateTimeOffset> RecentAttemptTimestamps { get; private set; } = [];
    internal int LockoutCount { get; private set; }

    internal void RecordFailure(DateTimeOffset now)
    {
        FailedAttemptCount++;
        LastFailedAtUtc = now;
    }

    internal void RecordFailure(DateTimeOffset now, TimeSpan failureWindow)
    {
        if (LastFailedAtUtc is not null && now >= LastFailedAtUtc.Value + failureWindow)
        {
            ResetWindowedFailureCount();
        }

        RecordFailure(now);
    }

    internal void IncrementLockoutCount() => LockoutCount++;

    internal void RecordFailureWithLockout(DateTimeOffset now, TimeSpan failureWindow, int maxFailedAttempts)
    {
        // If the failure count is already at or above the threshold, the lockout period must have
        // expired (otherwise the policy would have rejected this attempt). Reset the count so the
        // next MaxFailedAttempts failures trigger a fresh lockout and increment LockoutCount.
        if (FailedAttemptCount >= maxFailedAttempts)
        {
            ResetWindowedFailureCount();
        }

        RecordFailure(now, failureWindow);
        if (FailedAttemptCount == maxFailedAttempts)
        {
            IncrementLockoutCount();
        }
    }

    /// <summary>
    /// Records a velocity timestamp for the current attempt (success or failure) and prunes
    /// entries older than <paramref name="retentionWindow"/>.
    /// </summary>
    internal void RecordAttempt(DateTimeOffset now, TimeSpan retentionWindow)
    {
        var cutoff = now - retentionWindow;
        _ = RecentAttemptTimestamps.RemoveAll(t => t < cutoff);
        RecentAttemptTimestamps.Add(now);
    }

    /// <summary>
    /// Resets the failure count and last-failed timestamp without clearing velocity timestamps
    /// or lockout count. Called when the failure window expires.
    /// </summary>
    private void ResetWindowedFailureCount()
    {
        FailedAttemptCount = 0;
        LastFailedAtUtc = null;
    }

    /// <summary>
    /// Resets the failure count, last-failed timestamp, and lockout count without clearing
    /// velocity timestamps. Call this on a successful authentication.
    /// </summary>
    internal void ResetFailureCount()
    {
        FailedAttemptCount = 0;
        LastFailedAtUtc = null;
        LockoutCount = 0;
    }

    /// <summary>
    /// Resets all state including velocity timestamps.
    /// </summary>
    internal void Reset()
    {
        FailedAttemptCount = 0;
        LastFailedAtUtc = null;
        RecentAttemptTimestamps = [];
        LockoutCount = 0;
    }

    internal static AuthenticatorFailureState Load(
        int failedAttemptCount,
        DateTimeOffset? lastFailedAtUtc) =>
        Load(failedAttemptCount, lastFailedAtUtc, null);

    internal static AuthenticatorFailureState Load(
        int failedAttemptCount,
        DateTimeOffset? lastFailedAtUtc,
        List<DateTimeOffset>? recentAttemptTimestamps) =>
        Load(failedAttemptCount, lastFailedAtUtc, recentAttemptTimestamps, 0);

    internal static AuthenticatorFailureState Load(
        int failedAttemptCount,
        DateTimeOffset? lastFailedAtUtc,
        List<DateTimeOffset>? recentAttemptTimestamps,
        int lockoutCount) =>
        new()
        {
            FailedAttemptCount = failedAttemptCount,
            LastFailedAtUtc = lastFailedAtUtc,
            RecentAttemptTimestamps = recentAttemptTimestamps ?? [],
            LockoutCount = lockoutCount
        };
}
