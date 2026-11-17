// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Hosting.DynamicProviders.Store;

/// <summary>
/// Allows a dynamic scheme's cached options to be evicted for the current space.
/// </summary>
internal interface ISpaceAwareDynamicOptionsCache
{
    bool TryRemoveDynamic(string name);

    /// <summary>
    /// Evicts every dynamic entry idle for at least the cache's configured idle timeout, as of
    /// <paramref name="now"/>. Unlike the cache's own opportunistic sweep, this is not throttled:
    /// the caller (the single sweep coordinator) is responsible for not calling this too often.
    /// </summary>
    void SweepIdle(DateTimeOffset now);
}
