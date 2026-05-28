// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.Default;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Caching decorator for IResourceStore that maintains a single authoritative
/// cached <see cref="Resources"/> snapshot. All lookup methods filter this
/// snapshot in memory, ensuring atomic cache population and eliminating
/// cross-entry drift that can occur with per-item caching strategies.
/// </summary>
/// <typeparam name="T">The inner <see cref="IResourceStore"/> implementation.</typeparam>
/// <seealso cref="IdentityServer.Stores.IResourceStore" />
public class CachingResourceStore<T> : IResourceStore
    where T : IResourceStore
{
    private const string AllKey = "__all__";

    private readonly IdentityServerOptions _options;
    private readonly HybridCache _cache;
    private readonly IResourceStore _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingResourceStore{T}"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="inner">The inner.</param>
    /// <param name="cache">The cache.</param>
    public CachingResourceStore(
        IdentityServerOptions options,
        T inner,
        [FromKeyedServices(ServiceProviderKeys.ConfigurationStoreCache)] HybridCache cache)
    {
        _options = options;
        _inner = inner;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<Resources> GetAllResourcesAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.GetAllResources");

        return await GetCachedResourcesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.FindApiResourcesByScopeName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var scopeSet = scopeNames.ToHashSet();
        var all = await GetCachedResourcesAsync(ct);

        return all.ApiResources
            .Where(a => a.Scopes.Any(s => scopeSet.Contains(s)))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.FindApiResourcesByName");
        activity?.SetTag(Tracing.Properties.ApiResourceNames, apiResourceNames.ToSpaceSeparatedString());

        var nameSet = apiResourceNames.ToHashSet();
        var all = await GetCachedResourcesAsync(ct);

        return all.ApiResources
            .Where(a => nameSet.Contains(a.Name))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.FindIdentityResourcesByScopeName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var scopeSet = scopeNames.ToHashSet();
        var all = await GetCachedResourcesAsync(ct);

        return all.IdentityResources
            .Where(i => scopeSet.Contains(i.Name))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.FindApiScopesByName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var scopeSet = scopeNames.ToHashSet();
        var all = await GetCachedResourcesAsync(ct);

        return all.ApiScopes
            .Where(s => scopeSet.Contains(s.Name))
            .ToArray();
    }

    private async Task<Resources> GetCachedResourcesAsync(Ct ct)
    {
        var cacheKey = CacheKey.For<Resources>(AllKey);
        var cacheOptions = CacheKey.WriteOptions(_options.Caching.ResourceStoreExpiration);

        try
        {
            return await _cache.GetOrCreateAsync(
                cacheKey,
                _inner,
                static async (inner, cancel) =>
                {
                    var all = await inner.GetAllResourcesAsync(cancel);
                    return all ?? throw new NotCachedException();
                },
                cacheOptions,
                cancellationToken: ct);
        }
        catch (NotCachedException)
        {
            return new Resources();
        }
    }
}
