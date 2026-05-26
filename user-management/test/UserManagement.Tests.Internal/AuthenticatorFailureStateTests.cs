// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal;

namespace Duende.Platform.UserManagement;

public class AuthenticatorFailureStateTests
{
    [Fact]
    public void RecordFailure_increments_count_and_sets_timestamp()
    {
        var state = AuthenticatorFailureState.Load(0, null);
        var now = DateTimeOffset.UtcNow;

        state.RecordFailure(now);

        state.FailedAttemptCount.ShouldBe(1);
        state.LastFailedAtUtc.ShouldBe(now);
    }

    [Fact]
    public void Reset_clears_count_and_timestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var state = AuthenticatorFailureState.Load(2, now);

        state.Reset();

        state.FailedAttemptCount.ShouldBe(0);
        state.LastFailedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void RecordFailure_multiple_times_accumulates_count()
    {
        var state = AuthenticatorFailureState.Load(0, null);

        state.RecordFailure(DateTimeOffset.UtcNow.AddMinutes(-1));
        state.RecordFailure(DateTimeOffset.UtcNow);

        state.FailedAttemptCount.ShouldBe(2);
    }

    // --- Velocity timestamp tracking ---

    [Fact]
    public void RecordAttempt_adds_timestamp()
    {
        var state = AuthenticatorFailureState.Load(0, null);
        var now = DateTimeOffset.UtcNow;

        state.RecordAttempt(now, TimeSpan.FromSeconds(10));

        state.RecentAttemptTimestamps.ShouldContain(now);
        state.RecentAttemptTimestamps.Count.ShouldBe(1);
    }

    [Fact]
    public void RecordAttempt_prunes_timestamps_older_than_velocity_window()
    {
        var state = AuthenticatorFailureState.Load(0, null);
        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromSeconds(10);

        state.RecordAttempt(now - TimeSpan.FromSeconds(15), window); // older than window
        state.RecordAttempt(now - TimeSpan.FromSeconds(5), window);  // within window
        state.RecordAttempt(now, window);                             // current

        // After the last RecordAttempt, the 15s-old entry should be pruned
        state.RecentAttemptTimestamps.Count.ShouldBe(2);
        state.RecentAttemptTimestamps.ShouldNotContain(now - TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void ResetFailureCount_clears_failure_state_but_preserves_timestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var state = AuthenticatorFailureState.Load(3, now - TimeSpan.FromMinutes(1));
        state.RecordAttempt(now, TimeSpan.FromSeconds(10));

        state.ResetFailureCount();

        state.FailedAttemptCount.ShouldBe(0);
        state.LastFailedAtUtc.ShouldBeNull();
        state.RecentAttemptTimestamps.Count.ShouldBe(1);
    }

    [Fact]
    public void Reset_clears_all_state_including_timestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var state = AuthenticatorFailureState.Load(2, now);
        state.RecordAttempt(now, TimeSpan.FromSeconds(10));

        state.Reset();

        state.FailedAttemptCount.ShouldBe(0);
        state.LastFailedAtUtc.ShouldBeNull();
        state.RecentAttemptTimestamps.ShouldBeEmpty();
    }

    [Fact]
    public void Load_with_timestamps_restores_all_state()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamps = new List<DateTimeOffset> { now - TimeSpan.FromSeconds(3), now - TimeSpan.FromSeconds(1) };

        var state = AuthenticatorFailureState.Load(2, now, timestamps);

        state.FailedAttemptCount.ShouldBe(2);
        state.LastFailedAtUtc.ShouldBe(now);
        state.RecentAttemptTimestamps.Count.ShouldBe(2);
    }

    [Fact]
    public void Load_with_null_timestamps_defaults_to_empty_list()
    {
        var state = AuthenticatorFailureState.Load(0, null, null);

        state.RecentAttemptTimestamps.ShouldBeEmpty();
    }
}
