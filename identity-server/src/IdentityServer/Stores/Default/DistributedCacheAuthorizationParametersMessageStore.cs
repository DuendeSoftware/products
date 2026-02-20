// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace Duende.IdentityServer.Stores.Default;

/// <summary>
/// Implementation of IAuthorizationParametersMessageStore that uses the IDistributedCache.
/// </summary>
public class DistributedCacheAuthorizationParametersMessageStore : IAuthorizationParametersMessageStore
{
    private readonly IDistributedCache _distributedCache;
    private readonly IHandleGenerationService _handleGenerationService;

    /// <summary>
    /// Ctor.
    /// </summary>
    /// <param name="distributedCache"></param>
    /// <param name="handleGenerationService"></param>
    public DistributedCacheAuthorizationParametersMessageStore(IDistributedCache distributedCache, IHandleGenerationService handleGenerationService)
    {
        _distributedCache = distributedCache;
        _handleGenerationService = handleGenerationService;
    }

    private static string CacheKeyPrefix => "DistributedCacheAuthorizationParametersMessageStore";

    /// <inheritdoc/>
    public virtual async Task<string> WriteAsync(Message<IDictionary<string, string[]>> message, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DistributedCacheAuthorizationParametersMessageStore.Write");

        // since this store is trusted and the JWT request processing has provided redundant entries
        // in the NameValueCollection, we are removing the JWT "request_uri" param so that when they
        // are reloaded/revalidated we don't re-trigger outbound requests. we could possibly do the
        // same for the "request" param, but it's less of a concern (as it's just a signature check).
        message.Data.Remove(OidcConstants.AuthorizeRequest.RequestUri);

        var key = await _handleGenerationService.GenerateAsync(ct);
        var cacheKey = $"{CacheKeyPrefix}-{key}";

        var json = ObjectSerializer.ToString(message);

        var options = new DistributedCacheEntryOptions();
        options.SetSlidingExpiration(Constants.DefaultCacheDuration);

        await _distributedCache.SetStringAsync(cacheKey, json, options, ct);

        return key;
    }

    /// <inheritdoc/>
    public virtual async Task<Message<IDictionary<string, string[]>>> ReadAsync(string id, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DistributedCacheAuthorizationParametersMessageStore.Read");

        var cacheKey = $"{CacheKeyPrefix}-{id}";
        var json = await _distributedCache.GetStringAsync(cacheKey, ct);

        if (json == null)
        {
            return new Message<IDictionary<string, string[]>>(new Dictionary<string, string[]>());
        }

        return ObjectSerializer.FromString<Message<IDictionary<string, string[]>>>(json);
    }

    /// <inheritdoc/>
    public virtual Task DeleteAsync(string id, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("DistributedCacheAuthorizationParametersMessageStore.Delete");

        var cacheKey = $"{CacheKeyPrefix}-{id}";
        return _distributedCache.RemoveAsync(cacheKey, ct);
    }
}
