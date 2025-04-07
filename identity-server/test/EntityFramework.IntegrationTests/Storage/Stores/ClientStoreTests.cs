// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.EntityFrameworkCore;
using Xunit.Sdk;

namespace EntityFramework.IntegrationTests.Storage.Stores;

public class ClientStoreTests : IntegrationTest<ClientStoreTests, ConfigurationDbContext, ConfigurationStoreOptions>
{
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
        var store = new ClientStore(context, FakeLogger<ClientStore>.Create(), new NoneCancellationTokenProvider());
        var client = await store.FindClientByIdAsync(Guid.NewGuid().ToString());
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
            await context.SaveChangesAsync();
        }

        Client client;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ClientStore(context, FakeLogger<ClientStore>.Create(), new NoneCancellationTokenProvider());
            client = await store.FindClientByIdAsync(testClient.ClientId);
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
            await context.SaveChangesAsync();
        }

        Client client;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ClientStore(context, FakeLogger<ClientStore>.Create(), new NoneCancellationTokenProvider());
            client = await store.FindClientByIdAsync(testClient.ClientId);
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

            context.SaveChanges();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new ClientStore(context, FakeLogger<ClientStore>.Create(), new NoneCancellationTokenProvider());

            const int timeout = 5000;
            var task = Task.Run(() => store.FindClientByIdAsync(testClient.ClientId));

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
                throw new TestTimeoutException(timeout);
            }
        }
    }
}
