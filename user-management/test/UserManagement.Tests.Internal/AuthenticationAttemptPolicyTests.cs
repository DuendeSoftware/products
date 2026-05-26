// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Internal;
using Microsoft.Extensions.Options;

namespace Duende.Platform.UserManagement;

public class AuthenticationAttemptPolicyTests
{
    [Fact]
    public async Task EvaluateAsync_under_threshold_returns_allow()
    {
        var policy = CreatePolicy();
        var context = CreateContext(4, DateTimeOffset.UtcNow);

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Allow>();
    }

    [Fact]
    public async Task EvaluateAsync_at_threshold_within_window_returns_reject()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now);
        var context = CreateContext(5, now - TimeSpan.FromMinutes(1));

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Reject>();
    }

    [Fact]
    public async Task EvaluateAsync_after_throttle_duration_returns_allow()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now);
        var context = CreateContext(5, now - TimeSpan.FromMinutes(6));

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Allow>();
    }

    [Fact]
    public async Task EvaluateAsync_after_failure_window_returns_allow()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now);
        var context = CreateContext(100, now - TimeSpan.FromMinutes(16));

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Allow>();
    }

    [Fact]
    public async Task EvaluateAsync_with_zero_max_failed_attempts_returns_reject()
    {
        var options = new UserAuthenticationOptions();
        options.Throttling.MaxFailedAttempts = 0;

        var policy = new DefaultAuthenticationAttemptPolicy(
            Options.Create(options),
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await policy.EvaluateAsync(CreateContext(0, DateTimeOffset.UtcNow), CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Reject>();
    }

    [Fact]
    public async Task EvaluateAsync_with_zero_max_failed_attempts_and_null_timestamp_returns_reject()
    {
        var options = new UserAuthenticationOptions();
        options.Throttling.MaxFailedAttempts = 0;

        var policy = new DefaultAuthenticationAttemptPolicy(
            Options.Create(options),
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await policy.EvaluateAsync(CreateContext(0, null), CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Reject>();
    }

    private static AuthenticationAttemptContext CreateContext(int failedAttemptCount, DateTimeOffset? lastFailedAtUtc) =>
        new(
            UserSubjectId.New(),
            new AuthenticatorAttemptInfo(new AuthenticatorKey.Password(), failedAttemptCount, lastFailedAtUtc));

    private static AuthenticationAttemptContext CreateContext(
        int failedAttemptCount,
        DateTimeOffset? lastFailedAtUtc,
        IReadOnlyList<DateTimeOffset> recentAttemptTimestamps) =>
        new(
            UserSubjectId.New(),
            new AuthenticatorAttemptInfo(new AuthenticatorKey.Password(), failedAttemptCount, lastFailedAtUtc, recentAttemptTimestamps));

    private static DefaultAuthenticationAttemptPolicy CreatePolicy(DateTimeOffset? now = null)
    {
        var options = new UserAuthenticationOptions();

        return new DefaultAuthenticationAttemptPolicy(
            Options.Create(options),
            new FakeTimeProvider(now ?? DateTimeOffset.UtcNow));
    }

    private static DefaultAuthenticationAttemptPolicy CreatePolicy(DateTimeOffset now, Action<AuthenticationThrottlingOptions> configure)
    {
        var options = new UserAuthenticationOptions();
        configure(options.Throttling);

        return new DefaultAuthenticationAttemptPolicy(
            Options.Create(options),
            new FakeTimeProvider(now));
    }

    // --- Velocity throttling tests ---

    [Fact]
    public async Task EvaluateAsync_under_velocity_threshold_returns_allow()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now, o => o.MaxAttemptsPerWindow = 5);
        // 4 attempts within the 10s window — under the threshold of 5
        var timestamps = Enumerable.Range(1, 4).Select(i => now - TimeSpan.FromSeconds(i)).ToList();
        var context = CreateContext(0, null, timestamps);

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Allow>();
    }

    [Fact]
    public async Task EvaluateAsync_at_velocity_threshold_within_window_returns_reject()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now, o => o.MaxAttemptsPerWindow = 5);
        // 5 attempts within the 10s window — at the threshold of 5
        var timestamps = Enumerable.Range(1, 5).Select(i => now - TimeSpan.FromSeconds(i)).ToList();
        var context = CreateContext(0, null, timestamps);

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Reject>();
    }

    [Fact]
    public async Task EvaluateAsync_after_velocity_throttle_duration_returns_allow()
    {
        var now = DateTimeOffset.UtcNow;
        var throttleDuration = TimeSpan.FromSeconds(30);
        var policy = CreatePolicy(now, o =>
        {
            o.MaxAttemptsPerWindow = 5;
            // Use a wide velocity window so all timestamps remain in-window
            o.VelocityWindow = TimeSpan.FromMinutes(5);
            o.VelocityThrottleDuration = throttleDuration;
        });
        // 5 attempts within the wide window, but the most recent was 31s ago — past the 30s velocity throttle duration
        var timestamps = Enumerable.Range(31, 5).Select(i => now - TimeSpan.FromSeconds(i)).ToList();
        var context = CreateContext(0, null, timestamps);

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Allow>();
    }

    [Fact]
    public async Task EvaluateAsync_velocity_timestamps_outside_window_are_ignored()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now, o => o.MaxAttemptsPerWindow = 5);
        // 5 attempts, but all older than the effective retention window (max of 10s velocity window, 30s throttle duration)
        var timestamps = Enumerable.Range(31, 5).Select(i => now - TimeSpan.FromSeconds(i)).ToList();
        var context = CreateContext(0, null, timestamps);

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Allow>();
    }

    [Fact]
    public async Task EvaluateAsync_empty_timestamps_velocity_check_passes()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now);
        var context = CreateContext(0, null, []);

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Allow>();
    }

    [Fact]
    public async Task EvaluateAsync_failure_throttle_fires_before_velocity_threshold()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now);
        // Failure threshold exceeded (5 failures, last failure 1 min ago — within 15 min window)
        // Only 2 velocity timestamps — under velocity threshold
        var timestamps = new[] { now - TimeSpan.FromSeconds(2), now - TimeSpan.FromSeconds(4) };
        var context = CreateContext(5, now - TimeSpan.FromMinutes(1), timestamps);

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Reject>();
    }

    [Fact]
    public async Task EvaluateAsync_velocity_fires_before_failure_threshold()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now, o => o.MaxAttemptsPerWindow = 5);
        // Only 1 failure — under failure threshold
        // 5 velocity timestamps within window — at velocity threshold of 5
        var timestamps = Enumerable.Range(1, 5).Select(i => now - TimeSpan.FromSeconds(i)).ToList();
        var context = CreateContext(1, now - TimeSpan.FromSeconds(1), timestamps);

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Reject>();
    }

    [Fact]
    public async Task EvaluateAsync_zero_max_attempts_per_window_rejects_on_any_timestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now, o => o.MaxAttemptsPerWindow = 0);
        // Even a single timestamp within the window should trigger rejection when threshold is 0
        var timestamps = new[] { now - TimeSpan.FromSeconds(1) };
        var context = CreateContext(0, null, timestamps);

        var result = await policy.EvaluateAsync(context, CancellationToken.None);

        _ = result.ShouldBeOfType<AuthenticationAttemptDecision.Reject>();
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
