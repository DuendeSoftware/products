// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

/// <summary>
/// Decorator for IIdentityProviderStore that will purge the IOptionsMonitor so that the options are not cached.
/// </summary>
/// <typeparam name="T"></typeparam>
public class NonCachingIdentityProviderStore<T> : IIdentityProviderStore
    where T : IIdentityProviderStore
{
    private readonly IIdentityProviderStore _inner;
    private readonly IdentityProviderOptionsMonitorCache _optionsMonitorCache;
    private readonly ILogger<NonCachingIdentityProviderStore<T>> _logger;

    /// <summary>
    /// Ctor
    /// </summary>
    public NonCachingIdentityProviderStore(
        T inner,
        IdentityProviderOptionsMonitorCache optionsMonitorCache,
        ILogger<NonCachingIdentityProviderStore<T>> logger)
    {
        _inner = inner;
        _optionsMonitorCache = optionsMonitorCache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(Ct ct) => _inner.GetAllSchemeNamesAsync(ct);

    /// <inheritdoc/>
    public async Task<IdentityProvider> GetBySchemeAsync(string scheme, Ct ct)
    {
        var item = await _inner.GetBySchemeAsync(scheme, ct);
        if (item != null)
        {
            if (_optionsMonitorCache.EnsureCacheUpdated(item))
            {
                _logger.LogDebug("The authentication handler options for scheme {scheme} were evicted because the identity provider configuration changed. Consider enabling caching for the IIdentityProviderStore with AddIdentityProviderStoreCache<T>() on IdentityServer if you do not want the options to be reinitialized on each request.", scheme);
            }
        }
        return item;
    }
}
