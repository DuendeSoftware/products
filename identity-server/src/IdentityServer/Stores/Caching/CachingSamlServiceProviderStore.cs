// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.Default;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Cache decorator for <see cref="ISamlServiceProviderStore"/>.
/// </summary>
/// <typeparam name="T">The inner store type.</typeparam>
public class CachingSamlServiceProviderStore<T> : ISamlServiceProviderStore
    where T : ISamlServiceProviderStore
{
    private readonly IdentityServerOptions _options;
    private readonly HybridCache _cache;
    private readonly ISamlServiceProviderStore _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingSamlServiceProviderStore{T}"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="inner">The inner store.</param>
    /// <param name="cache">The cache.</param>
    public CachingSamlServiceProviderStore(
        IdentityServerOptions options,
        T inner,
        [FromKeyedServices(ServiceProviderKeys.ConfigurationStoreCache)] HybridCache cache)
    {
        _options = options;
        _inner = inner;
        _cache = cache;
    }

    /// <summary>
    /// Finds a SAML service provider by entity ID, using the cache.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The service provider, or null if not found.</returns>
    public async Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingSamlServiceProviderStore.FindByEntityId");

        var cacheKey = CacheKey.For<SamlServiceProvider>(entityId);
        var cacheOptions = CacheKey.WriteOptions(_options.Caching.SamlServiceProviderStoreExpiration);

        try
        {
            return await _cache.GetOrCreateAsync(
                cacheKey,
                (inner: _inner, entityId),
                static async (state, cancel) =>
                {
                    var sp = await state.inner.FindByEntityIdAsync(state.entityId, cancel);
                    return sp ?? throw new NotCachedException();
                },
                cacheOptions,
                cancellationToken: ct);
        }
        catch (NotCachedException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingSamlServiceProviderStore.GetAllSamlServiceProviders");
        return _inner.GetAllSamlServiceProvidersAsync(ct);
    }
}
