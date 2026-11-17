// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// An in-process, reference-counted mutual exclusion gate with one <see cref="SemaphoreSlim"/> per key.
/// Entries for a key exist only while at least one caller holds or waits for it; they are removed as soon
/// as the last reference goes away (on release, timeout, or cancellation), so the dictionary never grows
/// with keys that are no longer in use.
/// </summary>
internal sealed class KeyedSemaphore
{
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    internal KeyedSemaphore() : this(TimeProvider.System)
    {
    }

    /// <param name="timeProvider">The clock that bounded waits are measured on.</param>
    internal KeyedSemaphore(TimeProvider timeProvider) => _timeProvider = timeProvider;

    /// <summary>Gets the number of keys that currently have at least one holder or waiter.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Tries to enter the gate for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to acquire exclusive access to.</param>
    /// <param name="timeout">
    /// How long to wait. Must be non-negative, <see cref="Timeout.InfiniteTimeSpan"/>, or at most
    /// <see cref="int.MaxValue"/> milliseconds.
    /// </param>
    /// <param name="ct">Cancels the wait. Cancellation throws and leaves no entry for <paramref name="key"/>.</param>
    /// <returns>
    /// A releaser to dispose when done, or <see langword="null"/> if <paramref name="timeout"/> elapsed first.
    /// Disposing the releaser more than once releases the gate only once.
    /// </returns>
    internal async Task<IAsyncDisposable?> TryEnterAsync(string key, TimeSpan timeout, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        if ((timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan) || timeout > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        Entry entry;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new Entry();
                _entries[key] = entry;
            }
            entry.References++;
        }

        var acquired = false;
        try
        {
            acquired = await WaitAsync(entry.Semaphore, timeout, ct);
        }
        finally
        {
            if (!acquired)
            {
                ReleaseReference(key, entry);
            }
        }

        return acquired ? new Releaser(this, key, entry) : null;
    }

    private async Task<bool> WaitAsync(SemaphoreSlim semaphore, TimeSpan timeout, Ct ct)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return await semaphore.WaitAsync(timeout, ct);
        }

        using var timeoutCts = new CancellationTokenSource(timeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            await semaphore.WaitAsync(linked.Token);
            return true;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private void ReleaseReference(string key, Entry entry)
    {
        lock (_gate)
        {
            entry.References--;
            if (entry.References == 0)
            {
                _entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        internal SemaphoreSlim Semaphore { get; } = new(1, 1);
        internal int References { get; set; }
    }

    private sealed class Releaser(KeyedSemaphore owner, string key, Entry entry) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return ValueTask.CompletedTask;
            }

            entry.Semaphore.Release();
            owner.ReleaseReference(key, entry);
            return ValueTask.CompletedTask;
        }
    }
}
