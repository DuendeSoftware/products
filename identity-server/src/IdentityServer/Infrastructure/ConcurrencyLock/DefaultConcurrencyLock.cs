// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Internal;

/// <summary>
/// Default implementation.
/// </summary>
#pragma warning disable CS0618 // IConcurrencyLock<T> is obsolete but retained for source compatibility
[Obsolete("DefaultConcurrencyLock<T> is no longer used by IdentityServer. It is retained only for source compatibility with code that still injects IConcurrencyLock<T>. It will be removed in a future major version.")]
public class DefaultConcurrencyLock<T> : IConcurrencyLock<T>, IDisposable
{
    private bool _isDisposed;
    private readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

    /// <inheritdoc/>
    public Task<bool> LockAsync(int millisecondsTimeout)
    {
        if (millisecondsTimeout <= 0)
        {
            throw new ArgumentException("millisecondsTimeout must be greater than zero.");
        }

        return Lock.WaitAsync(millisecondsTimeout);
    }

    /// <inheritdoc/>
    public void Unlock() => Lock.Release();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            Lock?.Dispose();
        }

        _isDisposed = true;
    }
}
#pragma warning restore CS0618
