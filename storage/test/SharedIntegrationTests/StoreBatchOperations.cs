// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.Storage.IntegrationTests;
/// <summary>
/// Tests for batch operations across all store implementations.
/// </summary>
public partial class StoreBatchOperations
{

    private static readonly EntityType EntityType = TestDso.DsoVersion.EntityType;
    private static readonly EntityType EntityType2 = TestDso2.DsoVersion.EntityType;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CanExecuteEmptyBatchAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var result = await store.ExecuteBatchAsync([], [], _ct);
        result.Success.ShouldBeTrue();
        result.Results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_create_single_entity()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var id = UuidV7.New();
        var testValue = new TestDso($"value-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(id, testValue, [], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeTrue();
        result.Results.Count.ShouldBe(1);
        result.Results[0].Index.ShouldBe(0);
        result.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
        var readResult = await store.TryReadAsync(EntityType, id, _ct);
        readResult.Found.ShouldBeTrue();
        ((TestDso)readResult.Dso!).Value.ShouldBe(testValue.Value);
    }

    [Fact]
    public async Task Can_create_multiple_entities_of_same_type()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        var id3 = UuidV7.New();
        var value1 = new TestDso($"value1-{Guid.NewGuid()}");
        var value2 = new TestDso($"value2-{Guid.NewGuid()}");
        var value3 = new TestDso($"value3-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(id1, value1, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            CreateOperation.For(id2, value2, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            CreateOperation.For(id3, value3, [], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeTrue();
        result.Results.Count.ShouldBe(3);
        result.Results.ShouldAllBe(r => r.Outcome == OperationOutcome.Success);
        (await store.TryReadAsync(EntityType, id1, _ct)).Found.ShouldBeTrue();
        (await store.TryReadAsync(EntityType, id2, _ct)).Found.ShouldBeTrue();
        (await store.TryReadAsync(EntityType, id3, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_create_different_entity_types()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        var value1 = new TestDso($"testdso-{Guid.NewGuid()}");
        var value2 = new TestDso2($"testdso2-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(id1, value1, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            CreateOperation.For(id2, value2, [], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeTrue();
        result.Results.Count.ShouldBe(2);
        result.Results.ShouldAllBe(r => r.Outcome == OperationOutcome.Success);
        (await store.TryReadAsync(EntityType, id1, _ct)).Found.ShouldBeTrue();
        (await store.TryReadAsync(EntityType2, id2, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task CanMixCreateUpdateDeleteAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        // Pre-create entities to update and delete
        var updateId = UuidV7.New();
        var deleteId = UuidV7.New();
        var originalValue = new TestDso($"original-{Guid.NewGuid()}");
        var deleteValue = new TestDso($"delete-{Guid.NewGuid()}");
        (await store.CreateAsync(updateId, originalValue, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);
        (await store.CreateAsync(deleteId, deleteValue, [], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);
        var updateVersion = (await store.TryReadAsync(EntityType, updateId, _ct)).Version!.Value;
        // Now execute batch with create + update + delete
        var createId = UuidV7.New();
        var createValue = new TestDso($"created-{Guid.NewGuid()}");
        var updatedValue = new TestDso($"updated-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(createId, createValue, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            UpdateOperation.For(updateId, updatedValue, updateVersion, [], SearchFieldCollection.Empty, null),
            DeleteOperation.ById(EntityType, deleteId)
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeTrue();
        result.Results.Count.ShouldBe(3);
        result.Results.ShouldAllBe(r => r.Outcome == OperationOutcome.Success);
        // Verify create
        var createRead = await store.TryReadAsync(EntityType, createId, _ct);
        createRead.Found.ShouldBeTrue();
        ((TestDso)createRead.Dso).Value.ShouldBe(createValue.Value);
        // Verify update
        var updateRead = await store.TryReadAsync(EntityType, updateId, _ct);
        updateRead.Found.ShouldBeTrue();
        ((TestDso)updateRead.Dso).Value.ShouldBe(updatedValue.Value);
        // Verify delete
        (await store.TryReadAsync(EntityType, deleteId, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task RollsBackOnCreateAlreadyExists()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        // Pre-create an entity that will cause conflict
        var existingId = UuidV7.New();
        var existingValue = new TestDso($"existing-{Guid.NewGuid()}");
        (await store.CreateAsync(existingId, existingValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        // Batch: create new entity + create with existing ID (should fail)
        var newId = UuidV7.New();
        var newValue = new TestDso($"new-{Guid.NewGuid()}");
        var conflictValue = new TestDso($"conflict-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(newId, newValue, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            CreateOperation.For(existingId, conflictValue, [], SearchFieldCollection.Empty, Expiration.NoExpiration) // This will fail
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeFalse();
        result.Results.Count.ShouldBe(2);
        result.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
        result.Results[1].Outcome.ShouldBe(OperationOutcome.AlreadyExists);
        // Verify rollback - newId should NOT exist
        (await store.TryReadAsync(EntityType, newId, _ct)).Found.ShouldBeFalse();
        // Original entity should be unchanged
        var readResult = await store.TryReadAsync(EntityType, existingId, _ct);
        readResult.Found.ShouldBeTrue();
        ((TestDso)readResult.Dso).Value.ShouldBe(existingValue.Value);
    }

    [Fact]
    public async Task RollsBackOnKeyConflict()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        // Pre-create an entity with a key
        var existingId = UuidV7.New();
        var existingValue = new TestDso($"existing-{Guid.NewGuid()}");
        var conflictKey = new TestJsonKeyDsk($"conflict-key-{Guid.NewGuid()}");
        (await store.CreateAsync(existingId, existingValue, [DataStorageKey.Create(conflictKey)], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult
            .Success);
        // Batch: create new entity + create with same key (should fail)
        var newId1 = UuidV7.New();
        var newId2 = UuidV7.New();
        var newValue1 = new TestDso($"new1-{Guid.NewGuid()}");
        var newValue2 = new TestDso($"new2-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(newId1, newValue1, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            CreateOperation.For(newId2, newValue2, [DataStorageKey.Create(conflictKey)], SearchFieldCollection.Empty, Expiration.NoExpiration) // Key conflict
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeFalse();
        result.Results.Count.ShouldBe(2);
        result.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
        result.Results[1].Outcome.ShouldBe(OperationOutcome.KeyConflict);
        // Verify rollback - neither new entity should exist
        (await store.TryReadAsync(EntityType, newId1, _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, newId2, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task RollsBackOnUpdateVersionMismatch()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        // Pre-create an entity
        var existingId = UuidV7.New();
        var existingValue = new TestDso($"existing-{Guid.NewGuid()}");
        (await store.CreateAsync(existingId, existingValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var correctVersion = (await store.TryReadAsync(EntityType, existingId, _ct)).Version!.Value;
        // Batch: create new entity + update with wrong version (should fail)
        var newId = UuidV7.New();
        var newValue = new TestDso($"new-{Guid.NewGuid()}");
        var updatedValue = new TestDso($"updated-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(newId, newValue, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            UpdateOperation.For(existingId, updatedValue, correctVersion + 999, [], SearchFieldCollection.Empty, null) // Wrong version
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeFalse();
        result.Results.Count.ShouldBe(2);
        result.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
        result.Results[1].Outcome.ShouldBe(OperationOutcome.UnexpectedVersion);
        // Verify rollback - new entity should NOT exist
        (await store.TryReadAsync(EntityType, newId, _ct)).Found.ShouldBeFalse();
        // Original entity should be unchanged
        var readResult = await store.TryReadAsync(EntityType, existingId, _ct);
        ((TestDso)readResult.Dso!).Value.ShouldBe(existingValue.Value);
    }
    [Fact]
    public async Task RollsBackOnUpdateDoesNotExist()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var nonExistentId = UuidV7.New();
        var newId = UuidV7.New();
        var newValue = new TestDso($"new-{Guid.NewGuid()}");
        var updateValue = new TestDso($"update-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(newId, newValue, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            UpdateOperation.For(nonExistentId, updateValue, 1, [], SearchFieldCollection.Empty, null) // Does not exist
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeFalse();
        result.Results.Count.ShouldBe(2);
        result.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
        result.Results[1].Outcome.ShouldBe(OperationOutcome.DoesNotExist);
        // Verify rollback - new entity should NOT exist
        (await store.TryReadAsync(EntityType, newId, _ct)).Found.ShouldBeFalse();
    }
    [Fact]
    public async Task StopsOnFirstFailure()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        // Pre-create an entity for ID conflict
        var existingId = UuidV7.New();
        var existingValue = new TestDso($"existing-{Guid.NewGuid()}");
        (await store.CreateAsync(existingId, existingValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var nonExistentId = UuidV7.New();
        var conflictValue = new TestDso($"conflict-{Guid.NewGuid()}");
        var updateValue = new TestDso($"update-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(existingId, conflictValue, [], SearchFieldCollection.Empty, Expiration.NoExpiration), // AlreadyExists
            UpdateOperation.For(nonExistentId, updateValue, 1, [], SearchFieldCollection.Empty, null) // DoesNotExist
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeFalse();
        result.Results.Count.ShouldBe(1);
        result.Results[0].Index.ShouldBe(0);
        result.Results[0].Outcome.ShouldBe(OperationOutcome.AlreadyExists);
    }
    [Fact]
    public async Task Can_delete_by_id()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        // Pre-create entities to delete
        var id1 = UuidV7.New();
        var id2 = UuidV7.New();
        var value1 = new TestDso($"delete1-{Guid.NewGuid()}");
        var value2 = new TestDso($"delete2-{Guid.NewGuid()}");
        (await store.CreateAsync(id1, value1, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(id2, value2, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var operations = new IStoreOperation[]
        {
            DeleteOperation.ById(EntityType, id1),
            DeleteOperation.ById(EntityType, id2)
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeTrue();
        result.Results.Count.ShouldBe(2);
        result.Results.ShouldAllBe(r => r.Outcome == OperationOutcome.Success);
        (await store.TryReadAsync(EntityType, id1, _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, id2, _ct)).Found.ShouldBeFalse();
    }
    [Fact]
    public async Task Can_delete_by_key()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        // Pre-create entity with a key
        var id = UuidV7.New();
        var value = new TestDso($"delete-{Guid.NewGuid()}");
        var key = new TestJsonKeyDsk($"delete-key-{Guid.NewGuid()}");
        (await store.CreateAsync(id, value, [DataStorageKey.Create(key)], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var operations = new IStoreOperation[]
        {
            DeleteOperation.ByKey(EntityType, DataStorageKey.Create(key))
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeTrue();
        result.Results.Count.ShouldBe(1);
        result.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
        (await store.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(key), _ct)).Found.ShouldBeFalse();
    }
    [Fact]
    public async Task DeleteByIdSucceedsWhenNotFoundAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var newId = UuidV7.New();
        var newValue = new TestDso($"new-{Guid.NewGuid()}");
        var nonExistentId = UuidV7.New();
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(newId, newValue, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            DeleteOperation.ById(EntityType, nonExistentId) // Does not exist - should still succeed
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        // Delete of non-existent entity should NOT be treated as a failure
        result.Success.ShouldBeTrue();
        result.Results.Count.ShouldBe(2);
        result.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
        result.Results[1].Outcome.ShouldBe(OperationOutcome.Success);
        // The create should have been committed (no rollback)
        (await store.TryReadAsync(EntityType, newId, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteByKeySucceedsWhenNotFoundAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var newId = UuidV7.New();
        var newValue = new TestDso($"new-{Guid.NewGuid()}");
        var nonExistentKey = new TestJsonKeyDsk($"nonexistent-key-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(newId, newValue, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
            DeleteOperation.ByKey(EntityType, DataStorageKey.Create(nonExistentKey)) // Does not exist - should still succeed
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        // Delete of non-existent entity should NOT be treated as a failure
        result.Success.ShouldBeTrue();
        result.Results.Count.ShouldBe(2);
        result.Results[0].Outcome.ShouldBe(OperationOutcome.Success);
        result.Results[1].Outcome.ShouldBe(OperationOutcome.Success);
        // The create should have been committed (no rollback)
        (await store.TryReadAsync(EntityType, newId, _ct)).Found.ShouldBeTrue();
    }
    [Fact]
    public async Task ConcurrentBatchesAreIsolatedAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var concurrencyLevel = Math.Min(Environment.ProcessorCount, 5);
        var tasks = new Task<BatchResult>[concurrencyLevel];
        for (var i = 0; i < concurrencyLevel; i++)
        {
            var batchIndex = i;
            tasks[i] = Task.Run(async () =>
            {
                var id1 = UuidV7.New();
                var id2 = UuidV7.New();
                var value1 = new TestDso($"batch{batchIndex}-entity1-{Guid.NewGuid()}");
                var value2 = new TestDso($"batch{batchIndex}-entity2-{Guid.NewGuid()}");
                var operations = new IStoreOperation[]
                {
                    CreateOperation.For(id1, value1, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
                    CreateOperation.For(id2, value2, [], SearchFieldCollection.Empty, Expiration.NoExpiration)
                };
                return await store.ExecuteBatchAsync(operations, [], _ct);
            }, _ct);
        }
        var results = await Task.WhenAll(tasks);
        // All batches should succeed
        results.ShouldAllBe(r => r.Success);
        results.ShouldAllBe(r => r.Results.Count == 2);
        results.ShouldAllBe(r => r.Results.All(op => op.Outcome == OperationOutcome.Success));
    }
    [Fact]
    public async Task BatchWithKeysCreatesAndDeletesKeysCorrectlyAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var id = UuidV7.New();
        var value = new TestDso($"value-{Guid.NewGuid()}");
        var key1 = new TestJsonKeyDsk($"key1-{Guid.NewGuid()}");
        var key2 = new TestUuidV7KeyDsk(Guid.CreateVersion7());
        var operations = new IStoreOperation[]
        {
            CreateOperation.For(id, value, [DataStorageKey.Create(key1), DataStorageKey.Create(key2)], SearchFieldCollection.Empty, Expiration.NoExpiration)
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeTrue();
        // Verify entity can be read by all keys
        (await store.TryReadAsync(EntityType, id, _ct)).Found.ShouldBeTrue();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(key1), _ct)).Found.ShouldBeTrue();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(key2), _ct)).Found.ShouldBeTrue();
    }
    [Fact]
    public async Task UpdateInBatchUpdatesVersionAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        // Pre-create entity
        var id = UuidV7.New();
        var originalValue = new TestDso($"original-{Guid.NewGuid()}");
        (await store.CreateAsync(id, originalValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var version1 = (await store.TryReadAsync(EntityType, id, _ct)).Version!.Value;
        // Update in batch
        var updatedValue = new TestDso($"updated-{Guid.NewGuid()}");
        var operations = new IStoreOperation[]
        {
            UpdateOperation.For(id, updatedValue, version1, [], SearchFieldCollection.Empty, null)
        };
        var result = await store.ExecuteBatchAsync(operations, [], _ct);
        result.Success.ShouldBeTrue();
        // Verify version incremented
        var readResult = await store.TryReadAsync(EntityType, id, _ct);
        readResult.Version.ShouldBe(version1 + 1);
        ((TestDso)readResult.Dso!).Value.ShouldBe(updatedValue.Value);
    }
    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
}
