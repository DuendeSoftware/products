// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Snapshot of the persisted attempt state for a single authenticator.
/// </summary>
/// <param name="Authenticator">The authenticator the attempt state belongs to.</param>
/// <param name="FailedAttemptCount">The number of tracked failed attempts for the authenticator.</param>
/// <param name="LastFailedAtUtc">When the authenticator most recently failed, if ever.</param>
/// <param name="RecentAttemptTimestamps">
/// Timestamps of recent authentication attempts (successes and failures) within the velocity
/// tracking window. Used by <see cref="IAuthenticationAttemptPolicy"/> to enforce velocity
/// throttling. Older entries are pruned when a new attempt is recorded.
/// </param>
/// <param name="LockoutCount">
/// The number of times the user has been locked out for this authenticator since the last
/// successful authentication. Used by <see cref="IAuthenticationAttemptPolicy"/> to apply
/// escalating throttle durations.
/// </param>
public sealed record AuthenticatorAttemptInfo(
    AuthenticatorKey Authenticator,
    int FailedAttemptCount,
    DateTimeOffset? LastFailedAtUtc,
    IReadOnlyList<DateTimeOffset> RecentAttemptTimestamps,
    int LockoutCount)
{
    /// <summary>
    /// Initializes an <see cref="AuthenticatorAttemptInfo"/> without velocity timestamps
    /// (backward-compatible overload).
    /// </summary>
    public AuthenticatorAttemptInfo(
        AuthenticatorKey authenticator,
        int failedAttemptCount,
        DateTimeOffset? lastFailedAtUtc)
        : this(authenticator, failedAttemptCount, lastFailedAtUtc, [], 0)
    {
    }

    /// <summary>
    /// Initializes an <see cref="AuthenticatorAttemptInfo"/> without lockout count
    /// (backward-compatible overload).
    /// </summary>
    public AuthenticatorAttemptInfo(
        AuthenticatorKey authenticator,
        int failedAttemptCount,
        DateTimeOffset? lastFailedAtUtc,
        IReadOnlyList<DateTimeOffset> recentAttemptTimestamps)
        : this(authenticator, failedAttemptCount, lastFailedAtUtc, recentAttemptTimestamps, 0)
    {
    }
}
