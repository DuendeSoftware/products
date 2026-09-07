// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Xunit.Sdk;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// Abstract contract tests for IClientStore implementations.
/// Each derived class provides a specific store backend (EF, Storage, InMemory).
/// </summary>
public abstract class ClientStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance that operates against the shared test database.
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<IClientStore> CreateStore();

    /// <summary>
    /// Creates a store instance with guaranteed empty state (no previously seeded data).
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract Task<StoreHandle<IClientStore>> CreateIsolatedStoreAsync();

    /// <summary>
    /// Seeds a single client into the backing store.
    /// </summary>
    protected abstract Task SeedClientAsync(Client client);

    /// <summary>
    /// Seeds multiple clients into the backing store.
    /// </summary>
    protected abstract Task SeedClientsAsync(IEnumerable<Client> clients);

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task FindClientByIdAsync_WhenClientDoesNotExist_ExpectNull()
    {
        await using var handle = CreateStore();

        var client = await handle.Store.FindClientByIdAsync(Guid.NewGuid().ToString(), _ct);

        client.ShouldBeNull();
    }

    [Fact]
    public async Task FindClientByIdAsync_WhenClientExists_ExpectClientReturned()
    {
        var testClient = new Client
        {
            ClientId = $"test_client_{Guid.NewGuid():N}",
            ClientName = "Test Client"
        };

        await SeedClientAsync(testClient);

        await using var handle = CreateStore();
        var client = await handle.Store.FindClientByIdAsync(testClient.ClientId, _ct);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task FindClientByIdAsync_WhenClientExistsWithCollections_ExpectClientReturnedCollections()
    {
        var testClient = new Client
        {
            ClientId = $"properties_test_client_{Guid.NewGuid():N}",
            ClientName = "Properties Test Client",
            AllowedCorsOrigins = { "https://localhost" },
            AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
            AllowedScopes = { "openid", "profile", "api1" },
            Claims = { new ClientClaim("test", "value") },
            ClientSecrets = { new Secret("secret".Sha256()) },
            IdentityProviderRestrictions = { "AD" },
            PostLogoutRedirectUris = { "https://localhost/signout-callback" },
            Properties = { { "foo1", "bar1" }, { "foo2", "bar2" } },
            RedirectUris = { "https://localhost/signin" }
        };

        await SeedClientAsync(testClient);

        await using var handle = CreateStore();
        var client = await handle.Store.FindClientByIdAsync(testClient.ClientId, _ct);

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

    [Fact]
    public async Task FindClientByIdAsync_WhenClientsExistWithManyCollections_ExpectClientReturnedInUnderFiveSeconds()
    {
        var testClient = new Client
        {
            ClientId = $"test_client_with_uris_{Guid.NewGuid():N}",
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

        var otherClients = Enumerable.Range(0, 50).Select(i => new Client
        {
            ClientId = testClient.ClientId + i,
            ClientName = testClient.ClientName,
            AllowedScopes = testClient.AllowedScopes,
            AllowedGrantTypes = testClient.AllowedGrantTypes,
            RedirectUris = testClient.RedirectUris,
            PostLogoutRedirectUris = testClient.PostLogoutRedirectUris,
            AllowedCorsOrigins = testClient.AllowedCorsOrigins
        });

        await SeedClientAsync(testClient);
        await SeedClientsAsync(otherClients);

        await using var handle = CreateStore();

        const int timeout = 5000;
        var task = Task.Run(() => handle.Store.FindClientByIdAsync(testClient.ClientId, _ct), _ct);

        if (await Task.WhenAny(task, Task.Delay(timeout, _ct)) == task)
        {
#pragma warning disable xUnit1031
            var client = task.Result;
#pragma warning restore xUnit1031
            client.ShouldSatisfyAllConditions(c =>
            {
                c.ShouldNotBeNull();
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

    [Fact]
    public async Task GetAllClientsAsync_WhenNoClientsExist_ExpectEmptyCollection()
    {
        await using var handle = await CreateIsolatedStoreAsync();

        var clients = new List<Client>();
        await foreach (var client in handle.Store.GetAllClientsAsync(_ct))
        {
            clients.Add(client);
        }

        clients.ShouldNotBeNull();
        clients.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllClientsAsync_WhenClientsExist_ExpectAllClientsReturned()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var testClients = new List<Client>
        {
            new() { ClientId = $"enum_client1_{suffix}", ClientName = "Enum Client 1" },
            new() { ClientId = $"enum_client2_{suffix}", ClientName = "Enum Client 2" },
            new() { ClientId = $"enum_client3_{suffix}", ClientName = "Enum Client 3" }
        };

        await SeedClientsAsync(testClients);

        await using var handle = CreateStore();

        var clients = new List<Client>();
        await foreach (var client in handle.Store.GetAllClientsAsync(_ct))
        {
            clients.Add(client);
        }

        clients.ShouldNotBeNull();
        clients.Count.ShouldBeGreaterThanOrEqualTo(3);
        clients.ShouldContain(c => c.ClientId == testClients[0].ClientId);
        clients.ShouldContain(c => c.ClientId == testClients[1].ClientId);
        clients.ShouldContain(c => c.ClientId == testClients[2].ClientId);
    }

    [Fact]
    public async Task GetAllClientsAsync_WhenClientsExistWithCollections_ExpectCollectionsIncluded()
    {
        var testClient = new Client
        {
            ClientId = $"enum_collections_client_{Guid.NewGuid():N}",
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

        await SeedClientAsync(testClient);

        await using var handle = CreateStore();

        var clients = new List<Client>();
        await foreach (var c in handle.Store.GetAllClientsAsync(_ct))
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

/// <summary>
/// Wraps a store instance and any associated resources (scopes, contexts) that
/// need to be disposed when the test is done using the store.
/// </summary>
public sealed class StoreHandle<T>(T store, Func<ValueTask>? disposeAsync = null) : IDisposable, IAsyncDisposable
{
    public T Store { get; } = store;

    public void Dispose() => DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();

    public ValueTask DisposeAsync() => disposeAsync?.Invoke() ?? ValueTask.CompletedTask;
}

/// <summary>
/// Factory methods for creating <see cref="StoreHandle{T}"/> instances.
/// </summary>
public static class StoreHandle
{
    /// <summary>
    /// Creates a handle that will async-dispose the given resource when the handle is disposed.
    /// </summary>
    public static StoreHandle<T> WithAsyncDisposable<T>(T store, IAsyncDisposable resource) =>
        new(store, resource.DisposeAsync);

    /// <summary>
    /// Creates a handle that will dispose the given resource when the handle is disposed.
    /// </summary>
    public static StoreHandle<T> WithDisposable<T>(T store, IDisposable resource) =>
        new(store, () => { resource.Dispose(); return ValueTask.CompletedTask; });
}
