// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Testing;

namespace Duende.UserManagement;

public static class FakeLogCollectorExtensions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Waits until the collector holds at least <paramref name="count"/> records matching <paramref name="predicate"/>.
    /// Records already collected count towards the total, and the wait is woken by each new record rather than polling.
    /// </summary>
    /// <exception cref="TimeoutException">The records did not arrive within 30 seconds.</exception>
    public static async Task WaitForLogAsync(
        this FakeLogCollector collector,
        Func<FakeLogRecord, bool> predicate,
        Ct ct,
        int count = 1)
    {
        ArgumentNullException.ThrowIfNull(collector);
        ArgumentNullException.ThrowIfNull(predicate);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(DefaultTimeout);

        var matches = 0;
        try
        {
            await foreach (var record in collector.GetLogsAsync(timeout.Token))
            {
                if (predicate(record) && ++matches >= count)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Expected {count} matching log record(s) within {DefaultTimeout}, but found {matches}.");
        }
    }
}
