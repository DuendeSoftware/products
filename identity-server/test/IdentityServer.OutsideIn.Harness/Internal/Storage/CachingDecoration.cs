// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;

/// <summary>
/// Wraps Duende.Storage's configuration stores in IdentityServer's HybridCache-backed caching decorators (design doc
/// D34). <c>AddClientStoreCache&lt;T&gt;</c> and its siblings need a concrete store type they can construct through DI;
/// Duende.Storage's own store classes are internal to the IdentityServer assembly, so this captures whatever store was
/// already registered (the real one, reading from the database) behind a small forwarding wrapper the harness owns,
/// and registers that wrapper as the thing to cache.
/// </summary>
internal static class CachingDecoration
{
    /// <summary>
    /// Chooses the cache tiers for a node's HybridCaches. With a <paramref name="distributedCache" /> (the topology's
    /// shared instance), that instance is every HybridCache's second tier, shared by both nodes. Without one, the
    /// distributed tier is turned off, so each node only has its own in-memory cache. IdentityServer's own per-node
    /// <see cref="IDistributedCache" /> stays registered for the other services that need it.
    /// </summary>
    public static void UseCacheTiers(this IServiceCollection services, IDistributedCache? distributedCache)
    {
        if (distributedCache is not null)
        {
            services.AddSingleton(distributedCache);
        }
        else
        {
            services.ConfigureAll<HybridCacheOptions>(options =>
                options.DefaultEntryOptions = new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableDistributedCache });
        }
    }

    /// <summary>Adds caching for the client, resource, identity provider and CORS stores already registered.</summary>
    public static IIdentityServerBuilder AddCachingOverRegisteredStores(this IIdentityServerBuilder builder)
    {
        var services = builder.Services;

        // Captured before any caching decorator is registered, so these point at the real, database-backed stores,
        // not at whatever caching wrapper AddClientStoreCache etc. add below.
        var clientStore = FindLast<IClientStore>(services);
        var resourceStore = FindLast<IResourceStore>(services);
        var identityProviderStore = FindLast<IIdentityProviderStore>(services);
        var corsPolicyService = FindLast<ICorsPolicyService>(services);

        services.AddTransient(provider => new ForwardingClientStore((IClientStore)Build(clientStore, provider)));
        services.AddTransient(provider => new ForwardingResourceStore((IResourceStore)Build(resourceStore, provider)));
        services.AddTransient(provider => new ForwardingIdentityProviderStore((IIdentityProviderStore)Build(identityProviderStore, provider)));
        services.AddTransient(provider => new ForwardingCorsPolicyService((ICorsPolicyService)Build(corsPolicyService, provider)));

        return builder
            .AddInMemoryCaching()
            .AddClientStoreCache<ForwardingClientStore>()
            .AddResourceStoreCache<ForwardingResourceStore>()
            .AddIdentityProviderStoreCache<ForwardingIdentityProviderStore>()
            .AddCorsPolicyCache<ForwardingCorsPolicyService>();
    }

    private static ServiceDescriptor FindLast<TService>(IServiceCollection services) =>
        services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException($"No {typeof(TService).Name} is registered to add caching over.");

    // Builds the service the way the container would have: the real registration, not a cached decorator.
    private static object Build(ServiceDescriptor descriptor, IServiceProvider provider) =>
        descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
}

/// <summary>Forwards to whatever <see cref="IClientStore" /> was registered before caching was added.</summary>
internal sealed class ForwardingClientStore(IClientStore inner) : IClientStore
{
    public Task<Client?> FindClientByIdAsync(string clientId, Ct ct) => inner.FindClientByIdAsync(clientId, ct);

    public IAsyncEnumerable<Client> GetAllClientsAsync(Ct ct) => inner.GetAllClientsAsync(ct);
}

/// <summary>Forwards to whatever <see cref="IResourceStore" /> was registered before caching was added.</summary>
internal sealed class ForwardingResourceStore(IResourceStore inner) : IResourceStore
{
    public Task<IReadOnlyCollection<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct) =>
        inner.FindIdentityResourcesByScopeNameAsync(scopeNames, ct);

    public Task<IReadOnlyCollection<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, Ct ct) =>
        inner.FindApiScopesByNameAsync(scopeNames, ct);

    public Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct) =>
        inner.FindApiResourcesByScopeNameAsync(scopeNames, ct);

    public Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, Ct ct) =>
        inner.FindApiResourcesByNameAsync(apiResourceNames, ct);

    public Task<Resources> GetAllResourcesAsync(Ct ct) => inner.GetAllResourcesAsync(ct);
}

/// <summary>Forwards to whatever <see cref="IIdentityProviderStore" /> was registered before caching was added.</summary>
internal sealed class ForwardingIdentityProviderStore(IIdentityProviderStore inner) : IIdentityProviderStore
{
    public Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(Ct ct) => inner.GetAllSchemeNamesAsync(ct);

    public Task<IdentityProvider?> GetBySchemeAsync(string scheme, Ct ct) => inner.GetBySchemeAsync(scheme, ct);
}

/// <summary>Forwards to whatever <see cref="ICorsPolicyService" /> was registered before caching was added.</summary>
internal sealed class ForwardingCorsPolicyService(ICorsPolicyService inner) : ICorsPolicyService
{
    public Task<bool> IsOriginAllowedAsync(string origin, Ct ct) => inner.IsOriginAllowedAsync(origin, ct);
}
