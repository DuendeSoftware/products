// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.Default;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

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
        [JsonConstructor]
        public CorsCacheEntry(bool allowed) => Allowed = allowed;

        /// <summary>
        /// Is origin allowed.
        /// </summary>
        public bool Allowed { get; }
    }

    private readonly IdentityServerOptions _options;
    private readonly HybridCache _cache;
    private readonly ICorsPolicyService _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingCorsPolicyService{T}"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="inner">The inner.</param>
    /// <param name="cache">The cache.</param>
    public CachingCorsPolicyService(
        IdentityServerOptions options,
        T inner,
        [FromKeyedServices(ServiceProviderKeys.ConfigurationStoreCache)] HybridCache cache)
    {
        _options = options;
        _inner = inner;
        _cache = cache;
    }

    /// <inheritdoc/>
    public virtual async Task<bool> IsOriginAllowedAsync(string origin, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingCorsPolicyService.IsOriginAllowed");
        activity?.SetTag(Tracing.Properties.Origin, origin);

        var cacheOptions = CacheKey.WriteOptions(_options.Caching.CorsExpiration);
        var entry = await _cache.GetOrCreateAsync(
            CacheKey.For<CorsCacheEntry>(origin),
            (inner: _inner, origin),
            static async (state, cancel) => new CorsCacheEntry(await state.inner.IsOriginAllowedAsync(state.origin, cancel)),
            cacheOptions,
            cancellationToken: ct);

        return entry?.Allowed ?? false;
    }
}
