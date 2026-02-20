// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Caching decorator for IResourceStore
/// </summary>
/// <typeparam name="T"></typeparam>
/// <seealso cref="IdentityServer.Stores.IResourceStore" />
public class CachingResourceStore<T> : IResourceStore
    where T : IResourceStore
{
    private const string AllKey = "__all__";

    private readonly IdentityServerOptions _options;

    private readonly ICache<IdentityResource> _identityCache;
    private readonly ICache<ApiScope> _apiScopeCache;
    private readonly ICache<ApiResource> _apiResourceCache;
    private readonly ICache<Resources> _allCache;
    private readonly ICache<ApiResourceNames> _apiResourceNames;

    private readonly IResourceStore _inner;

    /// <summary>
    /// Used to cache the ApiResource names for ApiScopes requested.
    /// </summary>
    public class ApiResourceNames
    {
        /// <summary>
        /// The ApiResource names.
        /// </summary>
        public IEnumerable<string> Names { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingResourceStore{T}"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="inner">The inner.</param>
    /// <param name="identityCache">The IdentityResource cache.</param>
    /// <param name="apisCache">The ApiResource cache.</param>
    /// <param name="scopeCache">The ApiScope cache.</param>
    /// <param name="allCache">All Resources cache.</param>
    /// <param name="apiResourceNames"></param>
    public CachingResourceStore(IdentityServerOptions options,
        T inner,
        ICache<IdentityResource> identityCache,
        ICache<ApiResource> apisCache,
        ICache<ApiScope> scopeCache,
        ICache<Resources> allCache,
        ICache<ApiResourceNames> apiResourceNames)
    {
        _options = options;
        _inner = inner;
        _identityCache = identityCache;
        _apiResourceCache = apisCache;
        _apiScopeCache = scopeCache;
        _allCache = allCache;
        _apiResourceNames = apiResourceNames;
    }

    private static string GetKey(IEnumerable<string> names)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.GetKey");

        if (names == null || !names.Any())
        {
            return string.Empty;
        }

        return "sha256-" + names.OrderBy(x => x).Aggregate((x, y) => x + ',' + y).Sha256();
    }

    /// <inheritdoc/>
    public async Task<Resources> GetAllResourcesAsync(CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.GetAllResources");

        var key = AllKey;

        var all = await _allCache.GetOrAddAsync(key,
            _options.Caching.ResourceStoreExpiration,
            async () => await _inner.GetAllResourcesAsync(ct),
            ct);

        return all;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.FindApiResourcesByScopeName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var apiResourceNames = new HashSet<string>();
        var uncachedScopes = new List<string>();
        foreach (var scope in scopeNames)
        {
            var apiResourceName = await _apiResourceNames.GetAsync(scope, ct);
            if (apiResourceName != null)
            {
                foreach (var name in apiResourceName.Names)
                {
                    apiResourceNames.Add(name);
                }
            }
            else
            {
                uncachedScopes.Add(scope);
            }
        }

        if (uncachedScopes.Count > 0)
        {
            // now we need to lookup the remaining items. it's possible this is happening concurrently, so
            // we're going to use the "allcache" to throttle this lookup since the cache has concurrency lock.
            // also, the "allcache" conveniently holds Resources objects so it can handle all three of our resource types.
            // the results will then be put into the correct and specific cache as individual items for subsequent lookups.
            // this means the cache item in the "allcache" should not really be used again and thus can have a very short lifetime.
            // as the cache key we'll derive a key from the remaining names, and then hash it to not confuse admins with a meaningful name.
            //
            // create a key based on the names we're about to lookup
            var allCacheItemsKey = "ApiResourcesByScopeName-" + GetKey(uncachedScopes);
            // expire this entry much faster than the normal items
            var itemsDuration = _options.Caching.ResourceStoreExpiration / 20;
            // do the cache/DB lookup
            var resources = await _allCache.GetOrAddAsync(allCacheItemsKey, itemsDuration, async () =>
            {
                var results = await _inner.FindApiResourcesByScopeNameAsync(uncachedScopes, ct);
                return new Resources(null, results, null);
            }, ct);

            // get the specific items from the Resources object
            var uncachedItems = resources.ApiResources;

            // add the ApiResource names for each scope we didn't have cached above
            foreach (var scope in uncachedScopes)
            {
                var names = uncachedItems.Where(x => x.Scopes.Contains(scope)).Select(x => x.Name).ToArray();
                var apiResourceNamesCacheItem = new ApiResourceNames { Names = names };
                await _apiResourceNames.SetAsync(scope, apiResourceNamesCacheItem, _options.Caching.ResourceStoreExpiration, ct);
            }

            // add each one to the specific cache
            foreach (var item in uncachedItems)
            {
                // this adds to the ApiResource cache in the same way when FindApiResourcesByNameAsync is used
                await _apiResourceCache.SetAsync(item.Name, item, _options.Caching.ResourceStoreExpiration, ct);

                // add this name
                apiResourceNames.Add(item.Name);
            }
        }

        // now that we have all the ApiResource names, just use our other API (that should find the cacted items)
        return await FindApiResourcesByNameAsync(apiResourceNames, ct);
    }


    /// <inheritdoc/>
    public async Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.FindApiResourcesByName");
        activity?.SetTag(Tracing.Properties.ApiResourceNames, apiResourceNames.ToSpaceSeparatedString());

        return await FindItemsAsync(apiResourceNames, _apiResourceCache,
            async (names, innerCt) => new Resources(null, await _inner.FindApiResourcesByNameAsync(names, innerCt), null),
            x => x.ApiResources, x => x.Name, "ApiResources-", ct);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.FindIdentityResourcesByScopeName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        return await FindItemsAsync(scopeNames, _identityCache,
            async (names, innerCt) => new Resources(await _inner.FindIdentityResourcesByScopeNameAsync(names, innerCt), null, null),
            x => x.IdentityResources, x => x.Name, "IdentityResources-", ct);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingResourceStore.FindApiScopesByName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        return await FindItemsAsync(scopeNames, _apiScopeCache,
            async (names, innerCt) => new Resources(null, null, await _inner.FindApiScopesByNameAsync(names, innerCt)),
            x => x.ApiScopes, x => x.Name, "ApiScopes-", ct);
    }


    private async Task<IEnumerable<TItem>> FindItemsAsync<TItem>(
        IEnumerable<string> names,
        ICache<TItem> cache,
        Func<IEnumerable<string>, CT, Task<Resources>> getResourcesFunc,
        Func<Resources, IEnumerable<TItem>> getFromResourcesFunc,
        Func<TItem, string> getNameFunc,
        string allCachePrefix,
        CT ct
    )
        where TItem : class
    {
        var uncachedNames = new List<string>();
        var cachedItems = new List<TItem>();
        foreach (var name in names)
        {
            var item = await cache.GetAsync(name, ct);
            if (item != null)
            {
                cachedItems.Add(item);
            }
            else
            {
                uncachedNames.Add(name);
            }
        }

        if (uncachedNames.Count > 0)
        {
            // now we need to lookup the remaining items. it's possible this is happening concurrently, so
            // we're going to use the "allcache" to throttle this lookup since the cache has concurrency lock.
            // also, the "allcache" conveniently holds Resources objects so it can handle all three of our resource types.
            // the results will then be put into the correct and specific cache as individual items for subsequent lookups.
            // this means the cache item in the "allcache" should not really be used again and thus can have a very short lifetime.
            // as the cache key we'll derive a key from the remaining names, and then hash it to not confuse admins with a meaningful name.

            // create a key based on the names we're about to lookup
            var allCacheItemsKey = allCachePrefix + GetKey(uncachedNames);
            // expire this entry much faster than the normal items
            var itemsDuration = _options.Caching.ResourceStoreExpiration / 20;
            // do the cache/DB lookup
            var resources = await _allCache.GetOrAddAsync(allCacheItemsKey, itemsDuration, async () => await getResourcesFunc(uncachedNames, ct), ct);

            // get the specific items from the Resources object
            var uncachedItems = getFromResourcesFunc(resources);
            // add each one to the specific cache
            foreach (var item in uncachedItems)
            {
                await cache.SetAsync(getNameFunc(item), item, _options.Caching.ResourceStoreExpiration, ct);
            }

            // add these to our result
            cachedItems.AddRange(uncachedItems);
        }

        return cachedItems;
    }
}
