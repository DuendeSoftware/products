// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Internal;

/// <summary>
/// Nop implementation.
/// </summary>
#pragma warning disable CS0618 // IConcurrencyLock<T> is obsolete but retained for source compatibility
[Obsolete("NopConcurrencyLock<T> is no longer used by IdentityServer. It is retained only for source compatibility with code that still injects IConcurrencyLock<T>. It will be removed in a future major version.")]
public class NopConcurrencyLock<T> : IConcurrencyLock<T>
{
    /// <inheritdoc/>
    public Task<bool> LockAsync(int millisecondsTimeout) => Task.FromResult(true);

    /// <inheritdoc/>
    public void Unlock()
    {
    }
}
#pragma warning restore CS0618
