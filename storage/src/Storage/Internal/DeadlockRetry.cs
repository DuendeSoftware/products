// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Whole-operation retry for server engines whose transaction manager can abort a transaction with a
/// deadlock or serialization-failure error (Oracle ORA-00060, SQL Server 1205, PostgreSQL 40P01). Each
/// engine wraps its whole operation — open transaction, do the work, commit — in <see cref="RunAsync{T}"/>
/// so a losing transaction is retried from the start, including any outbox writes, rather than retried at
/// the statement level.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal static class DeadlockRetry
{
    /// <summary>The maximum number of attempts, including the first one.</summary>
    internal const int MaxAttempts = 3;

    private const int MinBackoffMilliseconds = 10;
    private const int MaxBackoffMilliseconds = 50;

    /// <summary>
    /// Runs <paramref name="operation"/>, retrying it from the start when <paramref name="isDeadlock"/>
    /// says the exception it threw was a deadlock or serialization failure. Non-deadlock exceptions are
    /// never retried. Up to <see cref="MaxAttempts"/> attempts are made in total; between attempts, this
    /// waits a short, randomly growing backoff (about 10–50 ms times the attempt number), honouring
    /// <paramref name="ct"/>. After the final attempt fails, the exception that attempt threw is
    /// rethrown (preserving its original stack trace).
    /// </summary>
    /// <param name="operation">
    /// The operation to run, given the 1-based attempt number. It must be fully retryable: every
    /// statement it issues (including opening the transaction and committing it) runs again on retry.
    /// </param>
    /// <param name="isDeadlock">Returns whether an exception the operation threw is a deadlock/serialization-failure error worth retrying.</param>
    /// <param name="onRetry">Called with the attempt number that just failed (1-based) and the exception, before waiting and retrying. Use it to log.</param>
    /// <param name="ct">Cancellation token honoured both by <paramref name="operation"/> and by the backoff delay.</param>
    public static async Task<T> RunAsync<T>(
        Func<int, Task<T>> operation,
        Func<Exception, bool> isDeadlock,
        Action<int, Exception> onRetry,
        Ct ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(isDeadlock);
        ArgumentNullException.ThrowIfNull(onRetry);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await operation(attempt).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < MaxAttempts && isDeadlock(ex))
            {
                onRetry(attempt, ex);

                // Not security-sensitive: this only spaces out retries of a transient transaction conflict.
#pragma warning disable CA5394
                var backoffMilliseconds = Random.Shared.Next(MinBackoffMilliseconds, MaxBackoffMilliseconds + 1) * attempt;
#pragma warning restore CA5394
                await Task.Delay(backoffMilliseconds, ct).ConfigureAwait(false);
            }
        }

        // Unreachable: the loop either returns or, on its final iteration, lets a deadlock exception
        // propagate because the `when` guard's `attempt < MaxAttempts` is false.
        throw new InvalidOperationException("DeadlockRetry.RunAsync exhausted its attempts without returning or rethrowing.");
    }
}
