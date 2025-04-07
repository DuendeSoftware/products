// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Internal;

/// <summary>
/// Nop implementation.
/// </summary>
public class NopConcurrencyLock<T> : IConcurrencyLock<T>
{
    /// <inheritdoc/>
    public Task<bool> LockAsync(int millisecondsTimeout) => Task.FromResult(true);

    /// <inheritdoc/>
    public void Unlock()
    {
    }
}
