// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.Default;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Cache decorator for IClientStore
/// </summary>
/// <typeparam name="T"></typeparam>
/// <seealso cref="IdentityServer.Stores.IClientStore" />
public class CachingClientStore<T> : IClientStore
    where T : IClientStore
{
    private readonly IdentityServerOptions _options;
    private readonly HybridCache _cache;
    private readonly IClientStore _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingClientStore{T}"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="inner">The inner.</param>
    /// <param name="cache">The cache.</param>
    public CachingClientStore(
        IdentityServerOptions options,
        T inner,
        [FromKeyedServices(ServiceProviderKeys.ConfigurationStoreCache)] HybridCache cache)
    {
        _options = options;
        _inner = inner;
        _cache = cache;
    }

    /// <summary>
    /// Finds a client by id
    /// </summary>
    /// <param name="clientId">The client id</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The client
    /// </returns>
    public async Task<Client> FindClientByIdAsync(string clientId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingClientStore.FindClientById");
        activity?.SetTag(Tracing.Properties.ClientId, clientId);

        var cacheKey = CacheKey.For<Client>(clientId);
        var cacheOptions = CacheKey.WriteOptions(_options.Caching.ClientStoreExpiration);

        try
        {
            return await _cache.GetOrCreateAsync(
                cacheKey,
                (inner: _inner, clientId),
                static async (state, cancel) =>
                {
                    var client = await state.inner.FindClientByIdAsync(state.clientId, cancel);
                    return client ?? throw new NotCachedException();
                },
                cacheOptions,
                cancellationToken: ct);
        }
        catch (NotCachedException)
        {
            return null;
        }
    }

#if NET10_0_OR_GREATER
    /// <inheritdoc/>
    public IAsyncEnumerable<Client> GetAllClientsAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("CachingClientStore.GetAllClients");
        return _inner.GetAllClientsAsync(ct);
    }
#endif
}
