// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;

/// <summary>
/// The in-memory store type (design doc <c>InMemoryStoreProvider</c>): configuration stores per node with every write
/// fanned out (D4), and operational stores shared by the topology. Uses the harness's writable stores, a stand-in for
/// D17 (see <see cref="InMemoryTopologyStores" />). Caching wraps those same writable stores (design doc D34): with
/// <see cref="CachingMode.InMemory" /> each node gets its own cache; with <see cref="CachingMode.Distributed" /> both
/// nodes share one <see cref="IDistributedCache" /> instance.
/// </summary>
internal sealed class InMemoryStoreProvider(InMemoryTopologyStores stores, CachingMode caching, IDistributedCache? distributedCache) : IStoreProvider
{
    public void ConfigureNode(IIdentityServerBuilder builder, int nodeIndex)
    {
        var node = stores.Nodes[nodeIndex];
        var services = builder.Services;

        services.AddSingleton(node.Clients);
        services.AddSingleton(node.Resources);
        services.AddSingleton(node.IdentityProviders);

        if (caching == CachingMode.None)
        {
            builder.AddClientStore<WritableClientStore>()
                .AddIdentityProviderStore<WritableIdentityProviderStore>();
            services.AddSingleton<IResourceStore>(node.Resources);
        }
        else
        {
            services.UseCacheTiers(distributedCache);
            builder.AddInMemoryCaching()
                .AddClientStoreCache<WritableClientStore>()
                .AddResourceStoreCache<WritableResourceStore>()
                .AddIdentityProviderStoreCache<WritableIdentityProviderStore>();
        }

        // Operational data is shared by both nodes, like a database, so a code issued on one node can be redeemed on the other.
        services.RemoveAll<IPersistedGrantStore>();
        services.AddSingleton<IPersistedGrantStore>(stores.PersistedGrants);
        services.RemoveAll<IDeviceFlowStore>();
        services.AddSingleton<IDeviceFlowStore>(stores.DeviceFlow);
        services.RemoveAll<IPushedAuthorizationRequestStore>();
        services.AddSingleton<IPushedAuthorizationRequestStore>(stores.PushedAuthorizationRequests);
    }

    public Task InitializeAsync(IServiceProvider services, HarnessLog log, Ct cancellationToken) => Task.CompletedTask;

    public Task AddClientAsync(Client client, Ct cancellationToken) => Done(() => stores.AddClient(client));

    public Task UpdateClientAsync(Client client, Ct cancellationToken) => Done(() => stores.AddClient(client));

    public Task RemoveClientAsync(string clientId, Ct cancellationToken) => Done(() => stores.RemoveClient(clientId));

    public Task AddIdentityResourceAsync(IdentityResource resource, Ct cancellationToken) =>
        Done(() => stores.AddIdentityResource(resource));

    public Task<bool> HasIdentityResourceAsync(string name, Ct cancellationToken) =>
        Task.FromResult(stores.HasIdentityResource(name));

    public Task AddApiScopeAsync(ApiScope scope, Ct cancellationToken) => Done(() => stores.AddApiScope(scope));

    public Task AddApiResourceAsync(ApiResource resource, Ct cancellationToken) => Done(() => stores.AddApiResource(resource));

    public Task AddIdentityProviderAsync(OidcProvider provider, Ct cancellationToken) =>
        Done(() => stores.AddIdentityProvider(provider));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Task Done(Action write)
    {
        write();
        return Task.CompletedTask;
    }
}
