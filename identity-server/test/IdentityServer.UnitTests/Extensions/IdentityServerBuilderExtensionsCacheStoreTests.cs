// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Extensions;

public class IdentityServerBuilderExtensionsCacheStoreTests
{
    private class CustomClientStore : IClientStore
    {
        public Task<Client> FindClientByIdAsync(string clientId, Ct _) => throw new System.NotImplementedException();

#if NET10_0_OR_GREATER
        public IAsyncEnumerable<Client> GetAllClientsAsync(Ct _) => throw new System.NotImplementedException();
#endif
    }

    private class CustomResourceStore : IResourceStore
    {
        public Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct) => throw new System.NotImplementedException();

        public Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct) => throw new System.NotImplementedException();

        public Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> names, Ct ct) => throw new System.NotImplementedException();

        public Task<Resources> GetAllResourcesAsync(Ct ct) => throw new System.NotImplementedException();

        public Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, Ct ct) => throw new System.NotImplementedException();
    }

    [Fact]
    public void AddClientStoreCache_should_add_concrete_iclientstore_implementation()
    {
        var services = new ServiceCollection();
        var identityServerBuilder = new IdentityServerBuilder(services);

        identityServerBuilder.AddClientStoreCache<CustomClientStore>();

        services.Any(x => x.ImplementationType == typeof(CustomClientStore)).ShouldBeTrue();
    }

    [Fact]
    public void AddResourceStoreCache_should_attempt_to_register_iresourcestore_implementation()
    {
        var services = new ServiceCollection();
        var identityServerBuilder = new IdentityServerBuilder(services);

        identityServerBuilder.AddResourceStoreCache<CustomResourceStore>();

        services.Any(x => x.ImplementationType == typeof(CustomResourceStore)).ShouldBeTrue();
    }
}
