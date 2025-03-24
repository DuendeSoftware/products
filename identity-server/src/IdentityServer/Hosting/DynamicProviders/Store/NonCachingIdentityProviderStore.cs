// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

/// <summary>
/// Decorator for IIdentityProviderStore that will purge the IOptionsMonitor so that the options are not cached.
/// </summary>
/// <typeparam name="T"></typeparam>
public class NonCachingIdentityProviderStore<T> : IIdentityProviderStore
    where T : IIdentityProviderStore
{
    private readonly IIdentityProviderStore _inner;
    private readonly IdentityServerOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<NonCachingIdentityProviderStore<T>> _logger;

    /// <summary>
    /// Ctor
    /// </summary>
    public NonCachingIdentityProviderStore(T inner,
        IdentityServerOptions options,
        IHttpContextAccessor httpContextAccessor,
        ILogger<NonCachingIdentityProviderStore<T>> logger)
    {
        _inner = inner;
        _options = options;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<IEnumerable<IdentityProviderName>> GetAllSchemeNamesAsync()
    {
        return _inner.GetAllSchemeNamesAsync();
    }

    /// <inheritdoc/>
    public async Task<IdentityProvider> GetBySchemeAsync(string scheme)
    {
        if (_httpContextAccessor.HttpContext == null)
        {
            _logger.LogDebug("Failed to retrieve the dynamic authentication scheme \"{scheme}\" because there is no current HTTP request", scheme);
            return null;
        }
        var item = await _inner.GetBySchemeAsync(scheme);
        RemoveCacheEntry(item);
        return item;
    }

    // when we load these items, we remove the corresponding options from the 
    // options monitor since those instances are cached my the authentication handler plumbing
    // this keeps theirs in sync with ours when we re-load from the DB
    private void RemoveCacheEntry(IdentityProvider idp)
    {
        if (idp != null)
        {
            var provider = _options.DynamicProviders.FindProviderType(idp.Type);
            if (provider != null)
            {
                var optionsMonitorType = typeof(IOptionsMonitorCache<>).MakeGenericType(provider.OptionsType);
                // need to resolve the provide type dynamically, thus the need for the http context accessor
                // this will throw if attempted outside an http request, but that is checked in the caller
                var optionsCache = _httpContextAccessor.HttpContext.RequestServices.GetService(optionsMonitorType);
                if (optionsCache != null)
                {
                    var mi = optionsMonitorType.GetMethod("TryRemove");
                    if (mi != null)
                    {
                        _logger.LogDebug($"Notice: The {provider.OptionsType.Name} object for scheme: {{scheme}} is not being cached. Consider enabling caching for the IIdentityProviderStore with AddIdentityProviderStoreCache<T>() on IdentityServer if you do not want the options to be reinitialized on each request.", idp.Scheme);
                        mi.Invoke(optionsCache, new object[] { idp.Scheme });
                    }
                }
            }
        }
    }
}
