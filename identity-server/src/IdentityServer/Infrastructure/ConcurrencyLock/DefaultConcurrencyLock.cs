// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Internal;

/// <summary>
/// Default implementation.
/// </summary>
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
