// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// Abstract contract tests for IPersistedGrantStore implementations.
/// Each derived class provides a specific store backend (EF, Storage, InMemory).
/// </summary>
public abstract class PersistedGrantStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance that operates against the shared test database.
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<IPersistedGrantStore> CreateStore();

    /// <summary>
    /// Creates a store instance with guaranteed empty state (no previously seeded data).
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract Task<StoreHandle<IPersistedGrantStore>> CreateIsolatedStoreAsync();

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected static PersistedGrant CreateTestObject(string? sub = null, string? clientId = null, string? sid = null, string? type = null) => new()
    {
        Key = Guid.NewGuid().ToString(),
        Type = type ?? "authorization_code",
        ClientId = clientId ?? Guid.NewGuid().ToString(),
        SubjectId = sub ?? Guid.NewGuid().ToString(),
        SessionId = sid ?? Guid.NewGuid().ToString(),
        CreationTime = DateTime.UtcNow,
        Expiration = DateTime.UtcNow.AddDays(30),
        Data = Guid.NewGuid().ToString()
    };

    [Fact]
    public async Task StoreAsync_WhenPersistedGrantStored_ExpectSuccess()
    {
        var persistedGrant = CreateTestObject();

        await using var handle = CreateStore();
        await handle.Store.StoreAsync(persistedGrant, _ct);

        var foundGrant = await handle.Store.GetAsync(persistedGrant.Key, _ct);
        foundGrant.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAsync_WithKeyAndPersistedGrantExists_ExpectPersistedGrantReturned()
    {
        var persistedGrant = CreateTestObject();

        await using var handle = CreateStore();
        await handle.Store.StoreAsync(persistedGrant, _ct);

        var foundGrant = await handle.Store.GetAsync(persistedGrant.Key, _ct);
        foundGrant.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_WithSubAndTypeAndPersistedGrantExists_ExpectPersistedGrantReturned()
    {
        var persistedGrant = CreateTestObject();

        await using var handle = CreateStore();
        await handle.Store.StoreAsync(persistedGrant, _ct);

        var foundGrants = (await handle.Store.GetAllAsync(new PersistedGrantFilter { SubjectId = persistedGrant.SubjectId }, _ct)).ToList();

        foundGrants.ShouldNotBeNull();
        foundGrants.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_Should_Filter()
    {
        await using var handle = await CreateIsolatedStoreAsync();
        var store = handle.Store;

        const string sub1 = "sub1";
        await store.StoreAsync(CreateTestObject(sub: sub1, clientId: "c1", sid: "s1", type: "t1"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub1, clientId: "c1", sid: "s1", type: "t2"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub1, clientId: "c1", sid: "s2", type: "t1"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub1, clientId: "c1", sid: "s2", type: "t2"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub1, clientId: "c2", sid: "s1", type: "t1"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub1, clientId: "c2", sid: "s1", type: "t2"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub1, clientId: "c2", sid: "s2", type: "t1"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub1, clientId: "c2", sid: "s2", type: "t2"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub1, clientId: "c3", sid: "s3", type: "t3"), _ct);
        await store.StoreAsync(CreateTestObject(), _ct);

        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(9);
        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = "sub2" }, _ct)).Count.ShouldBe(0);
        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c1" }, _ct)).Count.ShouldBe(4);
        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c2" }, _ct)).Count.ShouldBe(4);
        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c3" }, _ct)).Count.ShouldBe(1);
        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c4" }, _ct)).Count.ShouldBe(0);
        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c1", SessionId = "s1" }, _ct)).Count.ShouldBe(2);
        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c3", SessionId = "s1" }, _ct)).Count.ShouldBe(0);
        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c1", SessionId = "s1", Type = "t1" }, _ct)).Count.ShouldBe(1);
        (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c1", SessionId = "s1", Type = "t3" }, _ct)).Count.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAsync_WhenKeyOfExistingReceived_ExpectGrantDeleted()
    {
        var persistedGrant = CreateTestObject();

        await using var handle = CreateStore();
        await handle.Store.StoreAsync(persistedGrant, _ct);
        await handle.Store.RemoveAsync(persistedGrant.Key, _ct);

        var foundGrant = await handle.Store.GetAsync(persistedGrant.Key, _ct);
        foundGrant.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAllAsync_WhenSubIdAndClientIdOfExistingReceived_ExpectGrantDeleted()
    {
        var persistedGrant = CreateTestObject();

        await using var handle = CreateStore();
        await handle.Store.StoreAsync(persistedGrant, _ct);
        await handle.Store.RemoveAllAsync(new PersistedGrantFilter
        {
            SubjectId = persistedGrant.SubjectId,
            ClientId = persistedGrant.ClientId
        }, _ct);

        var foundGrant = await handle.Store.GetAsync(persistedGrant.Key, _ct);
        foundGrant.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAllAsync_WhenSubIdClientIdAndTypeOfExistingReceived_ExpectGrantDeleted()
    {
        var persistedGrant = CreateTestObject();

        await using var handle = CreateStore();
        await handle.Store.StoreAsync(persistedGrant, _ct);
        await handle.Store.RemoveAllAsync(new PersistedGrantFilter
        {
            SubjectId = persistedGrant.SubjectId,
            ClientId = persistedGrant.ClientId,
            Type = persistedGrant.Type
        }, _ct);

        var foundGrant = await handle.Store.GetAsync(persistedGrant.Key, _ct);
        foundGrant.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAllAsync_Should_Filter()
    {
        const string sub1 = "sub1";

        // Scenario: RemoveAll by SubjectId only — removes 9 of 10
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(0);
        }

        // Scenario: RemoveAll by non-existent SubjectId — removes nothing
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = "sub2" }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(9);
        }

        // Scenario: RemoveAll by SubjectId + ClientId "c1" — removes 4
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c1" }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(5);
        }

        // Scenario: RemoveAll by SubjectId + ClientId "c2" — removes 4
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c2" }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(5);
        }

        // Scenario: RemoveAll by SubjectId + ClientId "c3" — removes 1
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c3" }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(8);
        }

        // Scenario: RemoveAll by SubjectId + ClientId "c4" (non-existent) — removes 0
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c4" }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(9);
        }

        // Scenario: RemoveAll by SubjectId + ClientId + SessionId "s1" — removes 2
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c1", SessionId = "s1" }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(7);
        }

        // Scenario: RemoveAll by SubjectId + ClientId "c3" + SessionId "s1" (no match) — removes 0
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c3", SessionId = "s1" }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(9);
        }

        // Scenario: RemoveAll by SubjectId + ClientId + SessionId + Type "t1" — removes 1
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c1", SessionId = "s1", Type = "t1" }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(8);
        }

        // Scenario: RemoveAll by SubjectId + ClientId + SessionId + Type "t3" (no match) — removes 0
        {
            await using var handle = await CreateIsolatedStoreAsync();
            var store = handle.Store;
            await SeedFilterGrants(store, sub1);
            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = sub1, ClientId = "c1", SessionId = "s1", Type = "t3" }, _ct);
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = sub1 }, _ct)).Count.ShouldBe(9);
        }
    }

    [Fact]
    public async Task Store_should_create_new_record_if_key_does_not_exist()
    {
        var persistedGrant = CreateTestObject();

        await using var handle = CreateStore();
        var foundBefore = await handle.Store.GetAsync(persistedGrant.Key, _ct);
        foundBefore.ShouldBeNull();

        await handle.Store.StoreAsync(persistedGrant, _ct);

        var foundAfter = await handle.Store.GetAsync(persistedGrant.Key, _ct);
        foundAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task Store_should_update_record_if_key_already_exists()
    {
        var persistedGrant = CreateTestObject();

        await using var handle = CreateStore();
        await handle.Store.StoreAsync(persistedGrant, _ct);

        var newDate = persistedGrant.Expiration!.Value.AddHours(1);
        persistedGrant.Expiration = newDate;
        await handle.Store.StoreAsync(persistedGrant, _ct);

        var foundGrant = await handle.Store.GetAsync(persistedGrant.Key, _ct);
        foundGrant.ShouldNotBeNull();
        foundGrant.Expiration.ShouldBe(newDate);
    }

    private async Task SeedFilterGrants(IPersistedGrantStore store, string sub)
    {
        await store.StoreAsync(CreateTestObject(sub: sub, clientId: "c1", sid: "s1", type: "t1"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub, clientId: "c1", sid: "s1", type: "t2"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub, clientId: "c1", sid: "s2", type: "t1"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub, clientId: "c1", sid: "s2", type: "t2"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub, clientId: "c2", sid: "s1", type: "t1"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub, clientId: "c2", sid: "s1", type: "t2"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub, clientId: "c2", sid: "s2", type: "t1"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub, clientId: "c2", sid: "s2", type: "t2"), _ct);
        await store.StoreAsync(CreateTestObject(sub: sub, clientId: "c3", sid: "s3", type: "t3"), _ct);
        await store.StoreAsync(CreateTestObject(), _ct);
    }
}
