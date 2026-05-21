// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.IntegrationTests;

public partial class Stores
{

    private static readonly EntityType EntityType = TestDso.DsoVersion.EntityType;

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly UuidV7 _id = UuidV7.New();
    private readonly UuidV7 _id2 = UuidV7.New();
    private readonly TestJsonKeyDsk _jKey = new($"{nameof(_jKey)}-{Guid.NewGuid()}");
    private readonly TestJsonKeyDsk _jKey2 = new($"{nameof(_jKey2)}-{Guid.NewGuid()}");
    private readonly TestDso _testValue = new($"{nameof(_testValue)}-{Guid.NewGuid()}");
    private readonly TestDso _testValue2 = new($"{nameof(_testValue2)}-{Guid.NewGuid()}");
    private readonly TestUuidV4KeyDsk _uuidV4Key = new(Guid.NewGuid());
    private readonly TestUuidV4KeyDsk _uuidV4Key2 = new(Guid.NewGuid());
    private readonly TestUuidV7KeyDsk _uuidV7Key = new(Guid.CreateVersion7());
    private readonly TestUuidV7KeyDsk _uuidV7Key2 = new(Guid.CreateVersion7());

    private static void ShouldBeFound(StoreGetResult result, IDataStorageObject expectedDso, Guid expectedId, int expectedVersion)
    {
        result.Found.ShouldBeTrue();
        result.Dso.ShouldBe((object)expectedDso);
        result.Id.ShouldBe(expectedId);
        result.Version.ShouldBe(expectedVersion);
        result.CreatedAt.ShouldNotBe(default);
        result.LastUpdatedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task Can_create()
    {
        await using var fixture = await CreateProviderAsync();

        var store = fixture.Store;

        var result = await store.CreateAsync(
            _id,
            _testValue,
            [DataStorageKey.Create(_jKey), DataStorageKey.Create(_uuidV7Key), DataStorageKey.Create(_uuidV4Key)],
            [],
            Expiration.NoExpiration,
            [],
            _ct);

        result.ShouldBe(CreateResult.Success);
        ShouldBeFound(await store.TryReadAsync(EntityType, _id, _ct), _testValue, _id.Value, 1);
        ShouldBeFound(await store.TryReadAsync(EntityType, DataStorageKey.Create(_jKey), _ct), _testValue, _id.Value, 1);
        ShouldBeFound(await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV7Key), _ct), _testValue, _id.Value, 1);
        ShouldBeFound(await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV4Key), _ct), _testValue, _id.Value, 1);
    }

    [Fact]
    public async Task CannotCreateWhenAlreadyExistsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var result = await store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct);

        result.ShouldBe(CreateResult.AlreadyExists);
    }

    [Fact]
    public async Task ConcurrentCreateReturnsAlreadyExistsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var concurrencyLevel = Math.Min(Environment.ProcessorCount, 10);
        var tasks = new Task<CreateResult>[concurrencyLevel];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct), _ct);
        }

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r == CreateResult.Success);
        var alreadyExistsCount = results.Count(r => r == CreateResult.AlreadyExists);

        successCount.ShouldBe(1);
        alreadyExistsCount.ShouldBe(concurrencyLevel - 1);

    }

    [Fact]
    public async Task CannotCreateWhenJsonKeyAlreadyExistsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [DataStorageKey.Create(_jKey)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        var result = await store.CreateAsync(_id2, _testValue, [DataStorageKey.Create(_jKey)], [], Expiration.NoExpiration, [], _ct);

        result.ShouldBe(CreateResult.KeyConflict);
    }

    [Fact]
    public async Task CannotCreateWhenUuidV7KeyAlreadyExistsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [DataStorageKey.Create(_uuidV7Key)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        var result = await store.CreateAsync(_id2, _testValue, [DataStorageKey.Create(_uuidV7Key)], [], Expiration.NoExpiration, [], _ct);

        result.ShouldBe(CreateResult.KeyConflict);
    }

    [Fact]
    public async Task CannotCreateWhenUuidV4KeyAlreadyExistsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [DataStorageKey.Create(_uuidV4Key)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        var result = await store.CreateAsync(_id2, _testValue, [DataStorageKey.Create(_uuidV4Key)], [], Expiration.NoExpiration, [], _ct);

        result.ShouldBe(CreateResult.KeyConflict);
    }

    [Fact]
    public async Task Can_update()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(
            _id,
            _testValue,
            [DataStorageKey.Create(_jKey), DataStorageKey.Create(_uuidV7Key), DataStorageKey.Create(_uuidV4Key)],
            [],
            Expiration.NoExpiration,
            [],
            _ct)).ShouldBe(CreateResult.Success);
        var valueVersion = (await store.TryReadAsync(EntityType, _id, _ct)).Version.ShouldNotBeNull();

        var result = await store.UpdateAsync(
            _id,
            _testValue2,
            valueVersion,
            [DataStorageKey.Create(_jKey2), DataStorageKey.Create(_uuidV7Key2), DataStorageKey.Create(_uuidV4Key2)],
            [],
            expiration: null,
            [],
            _ct);

        result.ShouldBe(UpdateResult.Success);
        ShouldBeFound(await store.TryReadAsync(EntityType, _id, _ct), _testValue2, _id.Value, valueVersion + 1);
        ShouldBeFound(await store.TryReadAsync(EntityType, DataStorageKey.Create(_jKey2), _ct), _testValue2, _id.Value, valueVersion + 1);
        ShouldBeFound(await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV7Key2), _ct), _testValue2, _id.Value, valueVersion + 1);
        ShouldBeFound(await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV4Key2), _ct), _testValue2, _id.Value, valueVersion + 1);
    }

    [Fact]
    public async Task CannotUpdateWhenDoesNotExistAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var result = await store.UpdateAsync(_id, _testValue2, 1, [], [], expiration: null, [], _ct);

        result.ShouldBe(UpdateResult.DoesNotExist);
    }

    [Fact]
    public async Task CannotUpdateWithUnexpectedVersionAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var valueVersion = (await store.TryReadAsync(EntityType, _id, _ct)).Version.ShouldNotBeNull();
        (await store.UpdateAsync(_id, _testValue, valueVersion, [], [], expiration: null, [], _ct))
            .ShouldBe(UpdateResult.Success);

        var result = await store.UpdateAsync(_id, _testValue, valueVersion, [], [], expiration: null, [], _ct);

        result.ShouldBe(UpdateResult.UnexpectedVersion);
    }

    [Fact]
    public async Task ConcurrentUpdateReturnsUnexpectedVersionAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var valueVersion = (await store.TryReadAsync(EntityType, _id, _ct)).Version.ShouldNotBeNull();

        var concurrencyLevel = Math.Min(Environment.ProcessorCount, 10);
        var tasks = new Task<UpdateResult>[concurrencyLevel];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => store.UpdateAsync(_id, _testValue2, valueVersion, [], [], expiration: null, [], _ct), _ct);
        }

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r == UpdateResult.Success);
        var unexpectedVersionCount = results.Count(r => r == UpdateResult.UnexpectedVersion);

        successCount.ShouldBe(1);
        unexpectedVersionCount.ShouldBe(concurrencyLevel - 1);
    }

    [Fact]
    public async Task WhenKeyConflictJsonKeysAreNotUpdatedAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var valueVersion = (await store.TryReadAsync(EntityType, _id, _ct)).Version.ShouldNotBeNull();
        (await store.UpdateAsync(_id, _testValue, valueVersion, [], [], expiration: null, [], _ct))
            .ShouldBe(UpdateResult.Success);

        var result = await store.UpdateAsync(_id, _testValue, valueVersion, [DataStorageKey.Create(_jKey)], [], expiration: null, [], _ct);

        result.ShouldBe(UpdateResult.UnexpectedVersion);

        var getResult = await store.TryReadAsync(EntityType, DataStorageKey.Create(_jKey), _ct);
        getResult.Found.ShouldBe(false);
    }

    [Fact]
    public async Task CannotUpdateWhenJsonKeyAlreadyExistsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [DataStorageKey.Create(_jKey)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);
        var valueVersion = (await store.TryReadAsync(EntityType, _id, _ct)).Version.ShouldNotBeNull();
        (await store.CreateAsync(_id2, _testValue2, [DataStorageKey.Create(_jKey2)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        var result = await store.UpdateAsync(_id, _testValue, valueVersion, [DataStorageKey.Create(_jKey2)], [], expiration: null, [], _ct);

        result.ShouldBe(UpdateResult.KeyConflict);
    }

    [Fact]
    public async Task CannotUpdateWhenUuidV7KeyAlreadyExistsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [DataStorageKey.Create(_uuidV7Key)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);
        var valueVersion = (await store.TryReadAsync(EntityType, _id, _ct)).Version.ShouldNotBeNull();
        (await store.CreateAsync(_id2, _testValue2, [DataStorageKey.Create(_uuidV7Key2)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        var result = await store.UpdateAsync(_id, _testValue, valueVersion, [DataStorageKey.Create(_uuidV7Key2)], [], expiration: null, [], _ct);

        result.ShouldBe(UpdateResult.KeyConflict);
    }

    [Fact]
    public async Task CannotUpdateWhenUuidV4KeyAlreadyExistsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [DataStorageKey.Create(_uuidV4Key)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);
        var valueVersion = (await store.TryReadAsync(EntityType, _id, _ct)).Version.ShouldNotBeNull();
        (await store.CreateAsync(_id2, _testValue2, [DataStorageKey.Create(_uuidV4Key2)], [], Expiration.NoExpiration, [], _ct))
            .ShouldBe(CreateResult.Success);

        var result = await store.UpdateAsync(_id, _testValue, valueVersion, [DataStorageKey.Create(_uuidV4Key2)], [], expiration: null, [], _ct);

        result.ShouldBe(UpdateResult.KeyConflict);
    }

    [Fact]
    public async Task Can_delete_by_id()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(
            _id,
            _testValue,
            [DataStorageKey.Create(_jKey), DataStorageKey.Create(_uuidV7Key), DataStorageKey.Create(_uuidV4Key)],
            [],
            Expiration.NoExpiration,
            [],
            _ct)).ShouldBe(CreateResult.Success);

        var result = await store.DeleteAsync(EntityType, _id, [], _ct);

        result.ShouldBe(DeleteResult.Success);
        (await store.TryReadAsync(EntityType, _id, _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_jKey), _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV7Key), _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV4Key), _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_delete_by_json_key()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(
            _id,
            _testValue,
            [DataStorageKey.Create(_jKey), DataStorageKey.Create(_uuidV7Key), DataStorageKey.Create(_uuidV4Key)],
            [],
            Expiration.NoExpiration,
            [],
            _ct)).ShouldBe(CreateResult.Success);

        var result = await store.DeleteAsync(EntityType, DataStorageKey.Create(_jKey), [], _ct);

        result.ShouldBe(DeleteResult.Success);
        (await store.TryReadAsync(EntityType, _id, _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_jKey), _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV7Key), _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV4Key), _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_delete_by_UuidV7_key()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(
            _id,
            _testValue,
            [DataStorageKey.Create(_jKey), DataStorageKey.Create(_uuidV7Key), DataStorageKey.Create(_uuidV4Key)],
            [],
            Expiration.NoExpiration,
            [],
            _ct)).ShouldBe(CreateResult.Success);

        var result = await store.DeleteAsync(EntityType, DataStorageKey.Create(_uuidV7Key), [], _ct);

        result.ShouldBe(DeleteResult.Success);
        (await store.TryReadAsync(EntityType, _id, _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_jKey), _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV7Key), _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV4Key), _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_delete_by_UuidV4_key()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(
            _id,
            _testValue,
            [DataStorageKey.Create(_jKey), DataStorageKey.Create(_uuidV7Key), DataStorageKey.Create(_uuidV4Key)],
            [],
            Expiration.NoExpiration,
            [],
            _ct)).ShouldBe(CreateResult.Success);

        var result = await store.DeleteAsync(EntityType, DataStorageKey.Create(_uuidV4Key), [], _ct);

        result.ShouldBe(DeleteResult.Success);
        (await store.TryReadAsync(EntityType, _id, _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_jKey), _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV7Key), _ct)).Found.ShouldBeFalse();
        (await store.TryReadAsync(EntityType, DataStorageKey.Create(_uuidV4Key), _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task TryReadManyReturnsAllExistingEntitiesAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(_id2, _testValue2, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { _id, _id2 }, 100, _ct);
        results.ShouldContain(r => r.Found && r.Id == _id2.Value && r.Dso.Equals(_testValue2));
    }

    [Fact]
    public async Task TryReadManySkipsMissingIdsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        (await store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var missingId = UuidV7.New();

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { _id, missingId }, 100, _ct);

        results.Count.ShouldBe(1);
        results.ShouldContain(r => r.Found && r.Id == _id.Value);
    }

    [Fact]
    public async Task TryReadManyReturnsEmptyListWhenNoIdsExistAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var missingId1 = UuidV7.New();
        var missingId2 = UuidV7.New();

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { missingId1, missingId2 }, 100, _ct);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task TryReadManyReturnsEmptyListForEmptyInputAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7>(), 100, _ct);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task TryReadManyThrowsWhenExceedingMaximumAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var ids = new HashSet<UuidV7> { _id, _id2 };

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => store.TryReadManyAsync(EntityType, ids, 1, _ct));
    }

    [Fact]
    public async Task TryReadManyDoesNotReturnEntitiesFromDifferentEntityTypeAsync()
    {
        await using var fixture = await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
        var store = fixture.Store;
        var dso2 = new TestDso2($"value-{Guid.NewGuid()}");
        (await store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await store.CreateAsync(_id2, dso2, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);

        var results = await store.TryReadManyAsync(EntityType, new HashSet<UuidV7> { _id, _id2 }, 100, _ct);

        results.Count.ShouldBe(1);
        results.ShouldContain(r => r.Found && r.Id == _id.Value);
    }

    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
        });

    private async Task<IStoreFixture> CreateProviderAsync(FakeTimeProvider tp) =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            _ = services.AddSingleton(tp);
            _ = services.AddSingleton<TimeProvider>(tp);
            services.AddDsoRegistration<TestDso>();
        });

    [Fact]
    public async Task Created_is_stable_and_LastUpdated_advances_on_update()
    {
        var createTime = new DateTimeOffset(2025, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var updateTime = new DateTimeOffset(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var tp = new FakeTimeProvider(createTime);
        await using var fixture = await CreateProviderAsync(tp);
        var store = fixture.Store;

        // Create entity at createTime
        (await store.CreateAsync(_id, _testValue, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        var afterCreate = await store.TryReadAsync(EntityType, _id, _ct);
        afterCreate.Found.ShouldBeTrue();
        afterCreate.CreatedAt.ShouldBe(createTime);
        afterCreate.LastUpdatedAt.ShouldBe(createTime);

        // Advance time and update
        tp.SetUtcNow(updateTime);
        var updatedDso = new TestDso($"updated-{Guid.NewGuid()}");
        (await store.UpdateAsync(_id, updatedDso, afterCreate.Version!.Value, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(UpdateResult.Success);

        var afterUpdate = await store.TryReadAsync(EntityType, _id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.CreatedAt.ShouldBe(createTime);
        afterUpdate.LastUpdatedAt.ShouldBe(updateTime);
    }
}
