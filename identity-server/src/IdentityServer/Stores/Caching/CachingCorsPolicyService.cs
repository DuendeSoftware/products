// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Caching decorator for ICorsPolicyService
/// </summary>
/// <seealso cref="IdentityServer.Services.ICorsPolicyService" />
public class CachingCorsPolicyService<T> : ICorsPolicyService
    where T : ICorsPolicyService
{
    /// <summary>
    /// Class to model entries in CORS origin cache.
    /// </summary>
    public class CorsCacheEntry
    {
        /// <summary>
        /// Ctor.
        /// </summary>
        public CorsCacheEntry(bool allowed) => Allowed = allowed;

        /// <summary>
        /// Is origin allowed.
        /// </summary>
        public bool Allowed { get; }
    }

    private readonly IdentityServerOptions Options;
    private readonly ICache<CorsCacheEntry> CorsCache;
    private readonly ICorsPolicyService Inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingResourceStore{T}"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="inner">The inner.</param>
    /// <param name="corsCache">The CORS origin cache.</param>
    public CachingCorsPolicyService(IdentityServerOptions options,
        T inner,
        ICache<CorsCacheEntry> corsCache)
    {
        Options = options;
        Inner = inner;
        CorsCache = corsCache;
    }

    /// <inheritdoc/>
    public virtual async Task<bool> IsOriginAllowedAsync(string origin, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingCorsPolicyService.IsOriginAllowed");
        activity?.SetTag(Tracing.Properties.Origin, origin);

        var entry = await CorsCache.GetOrAddAsync(origin,
            Options.Caching.CorsExpiration,
            async () => new CorsCacheEntry(await Inner.IsOriginAllowedAsync(origin, ct)),
            ct);

        return entry.Allowed;
    }
}
