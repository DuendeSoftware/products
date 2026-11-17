// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.Storage;

public sealed class DeadlockRetryTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private sealed class DeadlockException : Exception;

    private sealed class OtherException : Exception;

    [Fact]
    public async Task Succeeds_on_first_attempt_without_retryingAsync()
    {
        var attempts = new List<int>();
        var retries = 0;

        var result = await DeadlockRetry.RunAsync(
            attempt =>
            {
                attempts.Add(attempt);
                return Task.FromResult(42);
            },
            _ => true,
            (_, _) => retries++,
            _ct);

        result.ShouldBe(42);
        attempts.ShouldBe([1]);
        retries.ShouldBe(0);
    }

    [Fact]
    public async Task Retries_up_to_the_attempt_cap_then_succeedsAsync()
    {
        var attempts = new List<int>();
        var retryLog = new List<int>();

        var result = await DeadlockRetry.RunAsync(
            attempt =>
            {
                attempts.Add(attempt);
                if (attempt < DeadlockRetry.MaxAttempts)
                {
                    throw new DeadlockException();
                }
                return Task.FromResult("done");
            },
            ex => ex is DeadlockException,
            (attempt, _) => retryLog.Add(attempt),
            _ct);

        result.ShouldBe("done");
        attempts.ShouldBe(Enumerable.Range(1, DeadlockRetry.MaxAttempts).ToList());
        retryLog.ShouldBe(Enumerable.Range(1, DeadlockRetry.MaxAttempts - 1).ToList());
    }

    [Fact]
    public async Task Rethrows_the_last_exception_after_exhausting_all_attemptsAsync()
    {
        var attempts = 0;
        var retries = 0;

        var thrown = await Should.ThrowAsync<DeadlockException>(async () =>
            await DeadlockRetry.RunAsync<int>(
                _ =>
                {
                    attempts++;
                    throw new DeadlockException();
                },
                ex => ex is DeadlockException,
                (_, _) => retries++,
                _ct));

        _ = thrown.ShouldNotBeNull();
        attempts.ShouldBe(DeadlockRetry.MaxAttempts);
        retries.ShouldBe(DeadlockRetry.MaxAttempts - 1);
    }

    [Fact]
    public async Task Does_not_retry_an_exception_that_is_not_a_deadlockAsync()
    {
        var attempts = 0;
        var retries = 0;

        _ = await Should.ThrowAsync<OtherException>(async () =>
            await DeadlockRetry.RunAsync<int>(
                _ =>
                {
                    attempts++;
                    throw new OtherException();
                },
                ex => ex is DeadlockException,
                (_, _) => retries++,
                _ct));

        attempts.ShouldBe(1);
        retries.ShouldBe(0);
    }

    [Fact]
    public async Task Honours_cancellation_during_the_backoff_delayAsync()
    {
        using var cts = new CancellationTokenSource();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await DeadlockRetry.RunAsync<int>(
                attempt =>
                {
                    if (attempt == 1)
                    {
                        cts.Cancel();
                        throw new DeadlockException();
                    }
                    return Task.FromResult(1);
                },
                ex => ex is DeadlockException,
                (_, _) => { },
                cts.Token));
    }
}
