// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Cache decorator for ISamlServiceProviderStore
/// </summary>
/// <typeparam name="T"></typeparam>
public class CachingSamlServiceProviderStore<T> : ISamlServiceProviderStore
    where T : ISamlServiceProviderStore
{
    private readonly IdentityServerOptions _options;
    private readonly ICache<SamlServiceProvider> _cache;
    private readonly ISamlServiceProviderStore _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingSamlServiceProviderStore{T}"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="inner">The inner.</param>
    /// <param name="cache">The cache.</param>
    public CachingSamlServiceProviderStore(IdentityServerOptions options, T inner, ICache<SamlServiceProvider> cache)
    {
        _options = options;
        _inner = inner;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity(
            "CachingSamlServiceProviderStore.FindByEntityId");
        activity?.SetTag(Tracing.Properties.SamlEntityId, entityId);

        var sp = await _cache.GetOrAddAsync(entityId,
            _options.Caching.SamlServiceProviderStoreExpiration,
#pragma warning disable CS8603 // Possible null reference return. Returning a null is ok here based on the method signature, but ICache<T> has not been updated for nullables
            async () => await _inner.FindByEntityIdAsync(entityId, ct),
#pragma warning restore CS8603 // Possible null reference return.
            ct);
        return sp;
    }

#if NET10_0_OR_GREATER
    /// <inheritdoc/>
    public IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity(
            "CachingSamlServiceProviderStore.GetAllSamlServiceProviders");
        return _inner.GetAllSamlServiceProvidersAsync(ct);
    }
#endif
}
