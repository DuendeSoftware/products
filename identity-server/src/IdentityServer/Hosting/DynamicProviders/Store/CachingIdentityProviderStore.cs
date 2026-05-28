// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.Default;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

/// <summary>
/// Caching decorator for IIdentityProviderStore
/// </summary>
/// <typeparam name="T"></typeparam>
public class CachingIdentityProviderStore<T> : IIdentityProviderStore
    where T : IIdentityProviderStore
{
    private readonly IIdentityProviderStore _inner;
    private readonly HybridCache _cache;
    private readonly IdentityServerOptions _options;
    private readonly IdentityProviderOptionsMonitorCache _optionsMonitorCache;
    private readonly ILogger<CachingIdentityProviderStore<T>> _logger;

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="inner"></param>
    /// <param name="cache"></param>
    /// <param name="options"></param>
    /// <param name="optionsMonitorCache"></param>
    /// <param name="logger"></param>
    public CachingIdentityProviderStore(
        T inner,
        [FromKeyedServices(ServiceProviderKeys.ConfigurationStoreCache)] HybridCache cache,
        IdentityServerOptions options,
        IdentityProviderOptionsMonitorCache optionsMonitorCache,
        ILogger<CachingIdentityProviderStore<T>> logger)
    {
        _inner = inner;
        _cache = cache;
        _options = options;
        _optionsMonitorCache = optionsMonitorCache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingIdentityProviderStore.GetAllSchemeNames");

        var cacheOptions = CacheKey.WriteOptions(_options.Caching.IdentityProviderCacheDuration);
        var result = await _cache.GetOrCreateAsync(
            CacheKey.For<IReadOnlyCollection<IdentityProviderName>>("__all__"),
            (inner: _inner, unused: 0),
            static async (state, cancel) => await state.inner.GetAllSchemeNamesAsync(cancel),
            cacheOptions,
            cancellationToken: ct);

        return result;
    }

    /// <inheritdoc/>
    public async Task<IdentityProvider> GetBySchemeAsync(string scheme, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingIdentityProviderStore.GetByScheme");

        var cacheOptions = CacheKey.WriteOptions(_options.Caching.IdentityProviderCacheDuration);

        try
        {
            var item = await _cache.GetOrCreateAsync(
                CacheKey.For<IdentityProvider>(scheme),
                (inner: _inner, scheme),
                static async (state, cancel) =>
                {
                    var result = await state.inner.GetBySchemeAsync(state.scheme, cancel);
                    return result ?? throw new NotCachedException();
                },
                cacheOptions,
                cancellationToken: ct);

            _optionsMonitorCache.EnsureCacheUpdated(item);

            return item;
        }
        catch (NotCachedException)
        {
            return null;
        }
    }
}
