// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Configuration options for the default per-authenticator throttling policy.
/// </summary>
public sealed class AuthenticationThrottlingOptions
{
    /// <summary>
    /// Maximum number of failed attempts before throttling kicks in. Default: 5.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Window after the last failure in which the failure count is relevant.
    /// If <c>LastFailedAtUtc + FailureWindow</c> has elapsed, the count is treated as zero.
    /// Default: 15 minutes.
    /// </summary>
    public TimeSpan FailureWindow { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long to block after exceeding the threshold, measured from <c>LastFailedAtUtc</c>.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan ThrottleDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of total attempts (successes and failures combined) allowed within
    /// <see cref="VelocityWindow"/> before velocity throttling kicks in. Default: 5.
    /// </summary>
    public int MaxAttemptsPerWindow { get; set; } = 5;

    /// <summary>
    /// Sliding time window used to count recent attempts for velocity throttling.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan VelocityWindow { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long to block after exceeding the velocity threshold, measured from the most
    /// recent attempt timestamp. Default: 30 seconds.
    /// </summary>
    public TimeSpan VelocityThrottleDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Per-lockout throttle durations for escalating lockout behavior.
    /// When set, the duration used after each lockout is determined by indexing into this list
    /// using the lockout count (clamped to the last entry). When <c>null</c> or empty,
    /// <see cref="ThrottleDuration"/> is used for all lockouts (flat behavior).
    /// </summary>
    public IReadOnlyList<TimeSpan>? EscalatingThrottleDurations { get; set; }

    /// <summary>
    /// The effective window used for retaining velocity timestamps — the greater of
    /// <see cref="VelocityWindow"/> and <see cref="VelocityThrottleDuration"/>.
    /// Timestamps must be retained for the full throttle duration so that the block
    /// remains in effect even after the counting window elapses.
    /// </summary>
    internal TimeSpan EffectiveVelocityRetentionWindow =>
        VelocityWindow > VelocityThrottleDuration ? VelocityWindow : VelocityThrottleDuration;
}
