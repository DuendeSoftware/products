// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Sdk;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework.Storage.Stores;

public class ClientStoreTests : IntegrationTest<ClientStoreTests, ConfigurationDbContext, ConfigurationStoreOptions>
{
    private readonly CT _ct = TestContext.Current.CancellationToken;

    public ClientStoreTests(DatabaseProviderFixture<ConfigurationDbContext> fixture) : base(fixture)
    {
        foreach (var options in TestDatabaseProviders)
        {
            using var context = new ConfigurationDbContext(options);
            context.Database.EnsureCreated();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindClientByIdAsync_WhenClientDoesNotExist_ExpectNull(DbContextOptions<ConfigurationDbContext> options)
    {
        await using var context = new ConfigurationDbContext(options);
        var store = new ClientStore(context, new NullLogger<ClientStore>(), new NoneCancellationTokenProvider());
        var client = await store.FindClientByIdAsync(Guid.NewGuid().ToString(), _ct);
        client.ShouldBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindClientByIdAsync_WhenClientExists_ExpectClientReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            ClientName = "Test Client"
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            context.Clients.Add(testClient.ToEntity());
            await context.SaveChangesAsync(_ct);
        }

        Client client;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ClientStore(context, new NullLogger<ClientStore>(), new NoneCancellationTokenProvider());
            client = await store.FindClientByIdAsync(testClient.ClientId, _ct);
        }

        client.ShouldNotBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindClientByIdAsync_WhenClientExistsWithCollections_ExpectClientReturnedCollections(DbContextOptions<ConfigurationDbContext> options)
    {
        var testClient = new Client
        {
            ClientId = "properties_test_client",
            ClientName = "Properties Test Client",
            AllowedCorsOrigins = { "https://localhost" },
            AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
            AllowedScopes = { "openid", "profile", "api1" },
            Claims = { new ClientClaim("test", "value") },
            ClientSecrets = { new Secret("secret".Sha256()) },
            IdentityProviderRestrictions = { "AD" },
            PostLogoutRedirectUris = { "https://locahost/signout-callback" },
            Properties = { { "foo1", "bar1" }, { "foo2", "bar2" }, },
            RedirectUris = { "https://locahost/signin" }
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            context.Clients.Add(testClient.ToEntity());
            await context.SaveChangesAsync(_ct);
        }

        Client client;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ClientStore(context, new NullLogger<ClientStore>(), new NoneCancellationTokenProvider());
            client = await store.FindClientByIdAsync(testClient.ClientId, _ct);
        }

        client.ShouldSatisfyAllConditions(c =>
        {
            c.ClientId.ShouldBe(testClient.ClientId);
            c.ClientName.ShouldBe(testClient.ClientName);
            c.AllowedCorsOrigins.ShouldBe(testClient.AllowedCorsOrigins);
            c.AllowedGrantTypes.ShouldBe(testClient.AllowedGrantTypes, true);
            c.AllowedScopes.ShouldBe(testClient.AllowedScopes, true);
            c.Claims.ShouldBe(testClient.Claims);
            c.ClientSecrets.ShouldBe(testClient.ClientSecrets, true);
            c.IdentityProviderRestrictions.ShouldBe(testClient.IdentityProviderRestrictions);
            c.PostLogoutRedirectUris.ShouldBe(testClient.PostLogoutRedirectUris);
            c.Properties.ShouldBe(testClient.Properties);
            c.RedirectUris.ShouldBe(testClient.RedirectUris);
        });
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindClientByIdAsync_WhenClientsExistWithManyCollections_ExpectClientReturnedInUnderFiveSeconds(DbContextOptions<ConfigurationDbContext> options)
    {
        var testClient = new Client
        {
            ClientId = "test_client_with_uris",
            ClientName = "Test client with URIs",
            AllowedScopes = { "openid", "profile", "api1" },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials
        };

        for (var i = 0; i < 50; i++)
        {
            testClient.RedirectUris.Add($"https://localhost/{i}");
            testClient.PostLogoutRedirectUris.Add($"https://localhost/{i}");
            testClient.AllowedCorsOrigins.Add($"https://localhost:{i}");
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            context.Clients.Add(testClient.ToEntity());

            for (var i = 0; i < 50; i++)
            {
                context.Clients.Add(new Client
                {
                    ClientId = testClient.ClientId + i,
                    ClientName = testClient.ClientName,
                    AllowedScopes = testClient.AllowedScopes,
                    AllowedGrantTypes = testClient.AllowedGrantTypes,
                    RedirectUris = testClient.RedirectUris,
                    PostLogoutRedirectUris = testClient.PostLogoutRedirectUris,
                    AllowedCorsOrigins = testClient.AllowedCorsOrigins,
                }.ToEntity());
            }

            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ClientStore(context, new NullLogger<ClientStore>(), new NoneCancellationTokenProvider());

            const int timeout = 5000;
            var task = Task.Run(() => store.FindClientByIdAsync(testClient.ClientId, _ct));

            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
#pragma warning disable xUnit1031 // Do not use blocking task operations in test method, suppressed because the task must have completed to enter this block
                var client = task.Result;
#pragma warning restore xUnit1031 // Do not use blocking task operations in test method
                client.ShouldSatisfyAllConditions(c =>
                {
                    c.ClientId.ShouldBe(testClient.ClientId);
                    c.ClientName.ShouldBe(testClient.ClientName);
                    c.AllowedScopes.ShouldBe(testClient.AllowedScopes, true);
                    c.AllowedGrantTypes.ShouldBe(testClient.AllowedGrantTypes);
                });
            }
            else
            {
                throw TestTimeoutException.ForTimedOutTest(timeout);
            }
        }
    }

    [Fact]
    public async Task GetAllClientsAsync_WhenNoClientsExist_ExpectEmptyCollection()
    {
        // Use a fresh isolated database so data inserted by other tests in this class doesn't interfere.
        var freshOptions = DatabaseProviderBuilder.BuildSqlite<ConfigurationDbContext, ConfigurationStoreOptions>(
            nameof(GetAllClientsAsync_WhenNoClientsExist_ExpectEmptyCollection), StoreOptions);
        await using var context = new ConfigurationDbContext(freshOptions);
        await context.Database.EnsureCreatedAsync(_ct);

        var store = new ClientStore(context, new NullLogger<ClientStore>(), new NoneCancellationTokenProvider());

        var clients = new List<Client>();
        await foreach (var client in store.GetAllClientsAsync(_ct))
        {
            clients.Add(client);
        }

        clients.ShouldNotBeNull();
        clients.ShouldBeEmpty();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetAllClientsAsync_WhenClientsExist_ExpectAllClientsReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var testClients = new List<Client>
        {
            new Client { ClientId = "enum_client1", ClientName = "Enum Client 1" },
            new Client { ClientId = "enum_client2", ClientName = "Enum Client 2" },
            new Client { ClientId = "enum_client3", ClientName = "Enum Client 3" }
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            foreach (var client in testClients)
            {
                context.Clients.Add(client.ToEntity());
            }
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ClientStore(context, new NullLogger<ClientStore>(), new NoneCancellationTokenProvider());

            var clients = new List<Client>();
            await foreach (var client in store.GetAllClientsAsync(_ct))
            {
                clients.Add(client);
            }

            clients.ShouldNotBeNull();
            clients.Count.ShouldBeGreaterThanOrEqualTo(3);
            clients.ShouldContain(c => c.ClientId == "enum_client1");
            clients.ShouldContain(c => c.ClientId == "enum_client2");
            clients.ShouldContain(c => c.ClientId == "enum_client3");
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetAllClientsAsync_WhenClientsExistWithCollections_ExpectCollectionsIncluded(DbContextOptions<ConfigurationDbContext> options)
    {
        var testClient = new Client
        {
            ClientId = "enum_collections_client",
            ClientName = "Enum Collections Client",
            AllowedCorsOrigins = { "https://localhost" },
            AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
            AllowedScopes = { "openid", "profile", "api1" },
            Claims = { new ClientClaim("test", "value") },
            ClientSecrets = { new Secret("secret".Sha256()) },
            IdentityProviderRestrictions = { "AD" },
            PostLogoutRedirectUris = { "https://localhost/signout-callback" },
            Properties = { { "foo1", "bar1" } },
            RedirectUris = { "https://localhost/signin" }
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            context.Clients.Add(testClient.ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ClientStore(context, new NullLogger<ClientStore>(), new NoneCancellationTokenProvider());

            var clients = new List<Client>();
            await foreach (var c in store.GetAllClientsAsync(_ct))
            {
                clients.Add(c);
            }
            var client = clients.FirstOrDefault(c => c.ClientId == testClient.ClientId);

            client.ShouldSatisfyAllConditions(c =>
            {
                c.ShouldNotBeNull();
                c.ClientId.ShouldBe(testClient.ClientId);
                c.ClientName.ShouldBe(testClient.ClientName);
                c.AllowedCorsOrigins.ShouldBe(testClient.AllowedCorsOrigins);
                c.AllowedGrantTypes.ShouldBe(testClient.AllowedGrantTypes, true);
                c.AllowedScopes.ShouldBe(testClient.AllowedScopes, true);
                c.Claims.ShouldBe(testClient.Claims);
                c.ClientSecrets.ShouldBe(testClient.ClientSecrets, true);
                c.IdentityProviderRestrictions.ShouldBe(testClient.IdentityProviderRestrictions);
                c.PostLogoutRedirectUris.ShouldBe(testClient.PostLogoutRedirectUris);
                c.Properties.ShouldBe(testClient.Properties);
                c.RedirectUris.ShouldBe(testClient.RedirectUris);
            });
        }
    }
}
