// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.IntegrationTests.Hosting;

public class CachingClientStoreTests
{
    private GenericHost _host;

    private class SlowClientStore : IClientStore
    {
        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            return new Client
            {
                ClientId = clientId,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "scope1" },
            };
        }
    }

    public CachingClientStoreTests()
    {
        _host = new GenericHost("https://server");
        _host.OnConfigureServices += services =>
        {
            services.AddRouting();

            services.AddIdentityServer(options =>
                {
                    options.Caching.CacheLockTimeout = TimeSpan.FromSeconds(2);
                })
                .AddInMemoryIdentityResources(Array.Empty<IdentityResource>())
                .AddInMemoryCaching()
                .AddClientStoreCache<SlowClientStore>();
        };
        _host.OnConfigure += app =>
        {
            app.UseRouting();
            app.UseIdentityServer();
        };

        _host.InitializeAsync().Wait();
    }

    [Fact]
    public async Task does_not_throw_exception_failed_to_obtain_cache_lock()
    {
        var clientStore = _host.Resolve<IClientStore>();

        // Queue up a good amount of concurrent access
        await Parallel.ForAsync(1, Environment.ProcessorCount * 4, async (i, _) =>
        {
            var expectedClientId = $"client_{i % Environment.ProcessorCount}";
            var client = await clientStore.FindClientByIdAsync(expectedClientId);

            Assert.NotNull(client);
            Assert.Equal(expectedClientId, client.ClientId);
        });
    }
}
