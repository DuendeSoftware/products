// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Duende.IdentityServer.Models;
using Duende.Spaces.Internal;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Telemetry;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PoolId = Duende.Storage.PoolId;

namespace Duende.Spaces;

public sealed class PoolIdAllocationTests : IAsyncLifetime
{
    private static readonly StorageInstanceId SpacesInstance = StorageInstanceId.Create("spaces_instance");
    private static readonly StorageInstanceId ProductInstance = StorageInstanceId.Create("product_instance");
    private static readonly DataCategoryName ProductCategory = DataCategoryName.Create("product_data");

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly string _databaseName = $"pool_id_allocation_{Guid.NewGuid():N}";
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;

    public async ValueTask InitializeAsync()
    {
        _services = await BuildServiceProviderAsync(_databaseName);
        _admin = _services.GetRequiredService<ISpaceAdmin>();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public async Task automatic_pool_id_is_not_reused_after_purge_and_late_data_is_inaccessible_from_another_provider()
    {
        var first = await CreateSpaceAsync(_admin, "late-write-first");
        var purgedPoolId = await GetPoolIdAsync(_admin, first);
        await DeleteAndPurgeAsync(_admin, first);

        var rowId = UuidV7.New();
        var lateWriter = _services.GetRequiredService<DefaultPartitionedStorageFactory>()
            .GetPartitionedStorage(ProductCategory, (PoolId)purgedPoolId);

        (await lateWriter.CreateAsync(
            rowId,
            new TestDso("late data"),
            [],
            SearchFieldCollection.Empty,
            Expiration.NoExpiration,
            [],
            _ct)).ShouldBe(CreateResult.Success);

        await using var secondProvider = await BuildServiceProviderAsync(_databaseName);
        var oldPool = secondProvider.GetRequiredService<DefaultPartitionedStorageFactory>()
            .GetPartitionedStorage(ProductCategory, (PoolId)purgedPoolId);

        var oldRow = await oldPool.TryReadAsync(TestDso.DsoVersion.EntityType, rowId, _ct);
        oldRow.Found.ShouldBeTrue();

        ((TestDso)oldRow.Dso).Value.ShouldBe("late data");

        var secondAdmin = secondProvider.GetRequiredService<ISpaceAdmin>();
        var second = await CreateSpaceAsync(secondAdmin, "late-write-second");
        var secondPoolId = await GetPoolIdAsync(secondAdmin, second);

        secondPoolId.ShouldBeGreaterThan(purgedPoolId);
        using (secondProvider.GetRequiredService<ISpaceContextAccessor>().SetSpace(second))
        {
            var partitionedStorage = await secondProvider.GetRequiredService<IPartitionedStorageFactory>()
                .GetPartitionedStorageAsync(ProductCategory, _ct);
            (await partitionedStorage.TryReadAsync(TestDso.DsoVersion.EntityType, rowId, _ct))
                .Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task higher_explicit_pool_id_sets_high_water_mark_and_lower_purged_id_remains_available()
    {
        const int highPoolId = 8_000;
        const int lowerPoolId = 4_000;

        var lowerSpace = await CreateSpaceAsync(_admin, "lower-explicit-original", (PoolId)lowerPoolId);
        await DeleteAndPurgeAsync(_admin, lowerSpace);

        var highSpace = await CreateSpaceAsync(_admin, "high-explicit", (PoolId)highPoolId);
        await DeleteAndPurgeAsync(_admin, highSpace);

        var reusedLowerSpace = await CreateSpaceAsync(_admin, "lower-explicit-reused", (PoolId)lowerPoolId);
        (await GetPoolIdAsync(_admin, reusedLowerSpace)).ShouldBe(lowerPoolId);

        var automaticSpace = await CreateSpaceAsync(_admin, "after-explicit-purges");
        (await GetPoolIdAsync(_admin, automaticSpace)).ShouldBeGreaterThan(highPoolId);
    }

    [Fact]
    public async Task automatic_first_initialization_includes_existing_active_and_deleted_spaces()
    {
        await SeedLegacySpaceAsync(_services, "legacy-active", 41, isDeleted: false);
        await SeedLegacySpaceAsync(_services, "legacy-deleted", 86, isDeleted: true);

        var automaticSpace = await CreateSpaceAsync(_admin, "legacy-auto-first");
        (await GetPoolIdAsync(_admin, automaticSpace)).ShouldBe(87);

        var existing = await _admin.QueryAsync(QueryRequest.Create<SpaceFilter, SpaceSortField>(), _ct);
        existing.Results.ShouldContain(item => item.Name == "legacy-active" && item.PoolId.Value == 41 && !item.IsDeleted);
        existing.Results.ShouldContain(item => item.Name == "legacy-deleted" && item.PoolId.Value == 86 && item.IsDeleted);
    }

    [Fact]
    public async Task explicit_first_initialization_includes_existing_active_and_deleted_spaces()
    {
        await SeedLegacySpaceAsync(_services, "legacy-active-explicit", 51, isDeleted: false);
        await SeedLegacySpaceAsync(_services, "legacy-deleted-explicit", 96, isDeleted: true);

        var lowerExplicitSpace = await CreateSpaceAsync(_admin, "legacy-explicit-first", (PoolId)25);
        (await GetPoolIdAsync(_admin, lowerExplicitSpace)).ShouldBe(25);

        var automaticSpace = await CreateSpaceAsync(_admin, "legacy-auto-after-explicit");
        (await GetPoolIdAsync(_admin, automaticSpace)).ShouldBe(97);

        var existing = await _admin.QueryAsync(QueryRequest.Create<SpaceFilter, SpaceSortField>(), _ct);
        existing.Results.ShouldContain(item => item.Name == "legacy-active-explicit" && item.PoolId.Value == 51 && !item.IsDeleted);
        existing.Results.ShouldContain(item => item.Name == "legacy-deleted-explicit" && item.PoolId.Value == 96 && item.IsDeleted);
    }

    [Fact]
    public async Task concurrent_first_creates_retry_high_water_mark_initialization()
    {
        await using var secondProvider = await BuildServiceProviderAsync(_databaseName);
        var secondAdmin = secondProvider.GetRequiredService<ISpaceAdmin>();

        var (first, second) = await CreateWithSameHighWaterMarkAsync(
            () => CreateSpaceAsync(_admin, "concurrent-first-auto"),
            () => CreateSpaceAsync(secondAdmin, "concurrent-second-auto"));

        (await GetPoolIdAsync(_admin, first)).ShouldBe(2);
        (await GetPoolIdAsync(secondAdmin, second)).ShouldBe(1);
    }

    [Fact]
    public async Task high_water_mark_key_rejects_another_record_after_creation_and_update()
    {
        var storage = _services.GetRequiredService<DefaultPartitionedStorageFactory>()
            .GetPartitionedStorage(DataCategoryName.Spaces, (PoolId)(-1));
        var key = DataStorageKey.Create(new SpacePoolIdHighWaterMarkDskV1());
        var duplicateId = UuidV7.New();

        async Task<CreateResult> TryCreateAnotherMarkAsync() => await storage.CreateAsync(
            duplicateId,
            new SpacePoolIdHighWaterMarkDso.V1(100),
            [key],
            SearchFieldCollection.Empty,
            Expiration.NoExpiration,
            [],
            _ct);

        await CreateSpaceAsync(_admin, "key-created");
        (await TryCreateAnotherMarkAsync()).ShouldBe(CreateResult.KeyConflict);

        await CreateSpaceAsync(_admin, "key-updated");
        (await TryCreateAnotherMarkAsync()).ShouldBe(CreateResult.KeyConflict);
        (await storage.TryReadAsync(SpacePoolIdHighWaterMarkDso.EntityType, duplicateId, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task conflicting_high_water_mark_key_fails_without_creating_a_space()
    {
        var storage = _services.GetRequiredService<DefaultPartitionedStorageFactory>()
            .GetPartitionedStorage(DataCategoryName.Spaces, (PoolId)(-1));
        var key = DataStorageKey.Create(new SpacePoolIdHighWaterMarkDskV1());

        (await storage.CreateAsync(
            UuidV7.New(),
            new SpacePoolIdHighWaterMarkDso.V1(100),
            [key],
            SearchFieldCollection.Empty,
            Expiration.NoExpiration,
            [],
            _ct)).ShouldBe(CreateResult.Success);

        var result = await _admin.CreateAsync(CreateConfiguration("key-conflict", poolId: null), _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors!.ShouldContain(error =>
            error.Code == "validation_failed" &&
            error.Message.Contains("high-water mark", StringComparison.Ordinal) &&
            error.Message.Contains("unique key", StringComparison.Ordinal));

        (await storage.TryReadAsync(
            SpacePoolIdHighWaterMarkDso.EntityType,
            SpacePoolIdHighWaterMarkDso.Id,
            _ct)).Found.ShouldBeFalse();
        var spaces = await _admin.QueryAsync(QueryRequest.Create<SpaceFilter, SpaceSortField>(), _ct);
        spaces.Results.ShouldBeEmpty();
    }

    [Fact]
    public async Task automatic_create_retries_after_concurrent_higher_explicit_create()
    {
        await CreateSpaceAsync(_admin, "before-version-race");
        await using var secondProvider = await BuildServiceProviderAsync(_databaseName);
        var secondAdmin = secondProvider.GetRequiredService<ISpaceAdmin>();

        var (automatic, explicitSpace) = await CreateWithSameHighWaterMarkAsync(
            () => CreateSpaceAsync(_admin, "concurrent-stale-auto"),
            () => CreateSpaceAsync(secondAdmin, "concurrent-high-explicit", (PoolId)5_000));

        (await GetPoolIdAsync(_admin, automatic)).ShouldBe(5_001);
        (await GetPoolIdAsync(secondAdmin, explicitSpace)).ShouldBe(5_000);
        await DeleteAndPurgeAsync(secondAdmin, explicitSpace);

        var laterAutomatic = await CreateSpaceAsync(_admin, "after-version-race");
        (await GetPoolIdAsync(_admin, laterAutomatic)).ShouldBe(5_002);
    }

    [Fact]
    public async Task higher_explicit_create_retries_after_concurrent_automatic_create()
    {
        await CreateSpaceAsync(_admin, "before-explicit-version-race");
        await using var secondProvider = await BuildServiceProviderAsync(_databaseName);
        var secondAdmin = secondProvider.GetRequiredService<ISpaceAdmin>();

        var (explicitSpace, automatic) = await CreateWithSameHighWaterMarkAsync(
            () => CreateSpaceAsync(_admin, "concurrent-stale-explicit", (PoolId)5_000),
            () => CreateSpaceAsync(secondAdmin, "concurrent-winning-auto"));

        (await GetPoolIdAsync(_admin, explicitSpace)).ShouldBe(5_000);
        (await GetPoolIdAsync(secondAdmin, automatic)).ShouldBe(2);
        var laterAutomatic = await CreateSpaceAsync(_admin, "after-explicit-version-race");
        (await GetPoolIdAsync(_admin, laterAutomatic)).ShouldBe(5_001);
    }

    [Fact]
    public async Task automatic_allocation_fails_without_wrapping_after_explicit_int_max_value()
    {
        var maxSpace = await CreateSpaceAsync(_admin, "int-max", (PoolId)int.MaxValue);
        (await GetPoolIdAsync(_admin, maxSpace)).ShouldBe(int.MaxValue);

        var exhausted = await _admin.CreateAsync(
            CreateConfiguration("after-int-max", poolId: null),
            _ct);

        exhausted.IsSuccess.ShouldBeFalse();
        exhausted.Id.ShouldBeNull();
        exhausted.Errors.ShouldNotBeNull();
        exhausted.Errors!.ShouldContain(error =>
            error.Code == "validation_failed" &&
            error.PropertyNames != null &&
            error.PropertyNames.Contains("PoolId") &&
            error.Message.Contains(int.MaxValue.ToString(), StringComparison.Ordinal));

        var persisted = await _admin.QueryAsync(QueryRequest.Create<SpaceFilter, SpaceSortField>(), _ct);
        persisted.Results.ShouldContain(item => item.PoolId.Value == int.MaxValue);
        persisted.Results.ShouldNotContain(item => item.PoolId.Value <= 0);
    }

    [Fact]
    public async Task purging_an_automatic_space_does_not_lower_the_allocation_history()
    {
        var first = await CreateSpaceAsync(_admin, "history-first");
        var firstPoolId = await GetPoolIdAsync(_admin, first);
        var second = await CreateSpaceAsync(_admin, "history-second");
        var secondPoolId = await GetPoolIdAsync(_admin, second);
        secondPoolId.ShouldBeGreaterThan(firstPoolId);

        await DeleteAndPurgeAsync(_admin, second);

        var third = await CreateSpaceAsync(_admin, "history-third");
        (await GetPoolIdAsync(_admin, third)).ShouldBeGreaterThan(secondPoolId);
    }

    private async Task<ServiceProvider> BuildServiceProviderAsync(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDsoRegistration<TestDso>();
        services.AddStorageInternal(SpacesInstance, builder => builder.AddSqliteInMemory($"spaces_{databaseName}"));
        services.AddStorageInternal(ProductInstance, builder => builder.AddSqliteInMemory($"product_{databaseName}"));
        services.AddIdentityServerBuilder().AddSpaces(SpacesInstance, _ => { });
        services.GetOrAddStorageInstanceRouter().AddMapping(ProductCategory, ProductInstance);
        TestSpacesLicense.RegisterEntitled(services);

        var provider = services.BuildServiceProvider();
        foreach (var instance in new[] { SpacesInstance, ProductInstance })
        {
            await provider.GetRequiredKeyedService<IStorageInstanceSchema>(instance).MigrateAsync(_ct);
        }

        return provider;
    }

    private async Task SeedLegacySpaceAsync(IServiceProvider services, string name, int poolId, bool isDeleted)
    {
        var partitionedStorage = services.GetRequiredService<DefaultPartitionedStorageFactory>()
            .GetPartitionedStorage(DataCategoryName.Spaces, (PoolId)(-1));

        var spaceGuid = Guid.CreateVersion7();
        var space = new SpaceDso.V1(spaceGuid, name, Enabled: true, poolId, [], IsDeleted: isDeleted);
        var searchFields = new SearchFieldsBuilder()
            .Add("name", name)
            .Add("enabled", true)
            .Add("poolId", poolId)
            .Add("isDeleted", isDeleted)
            .Build();

        (await partitionedStorage.CreateAsync(
            UuidV7.From(spaceGuid),
            space,
            [DataStorageKey.Create(SpacePoolDskV1.Create(poolId))],
            searchFields,
            Expiration.NoExpiration,
            [],
            _ct)).ShouldBe(CreateResult.Success);
    }

    private async Task<SpaceId> CreateSpaceAsync(ISpaceAdmin admin, string name, PoolId? poolId = null)
    {
        var result = await admin.CreateAsync(CreateConfiguration(name, poolId), _ct);
        result.IsSuccess.ShouldBeTrue(
            $"failed to create space '{name}': {string.Join("; ", (result.Errors ?? []).Select(error => error.Message))}");

        return result.Id!;
    }

    private static CreateSpaceConfiguration CreateConfiguration(string name, PoolId? poolId) => new()
    {
        Name = name,
        MatchPatterns = [new SpaceMatchPattern { Origin = $"https://{name}.example.com" }],
        PoolId = poolId
    };

    private async Task<int> GetPoolIdAsync(ISpaceAdmin admin, SpaceId id)
    {
        var result = await admin.GetAsync(id, _ct);
        result.Found.ShouldBeTrue();
        result.Item!.PoolId.Value.ShouldBeGreaterThan(0);
        return result.Item.PoolId.Value;
    }

    private async Task DeleteAndPurgeAsync(ISpaceAdmin admin, SpaceId id)
    {
        (await admin.DeleteAsync(id, _ct)).IsSuccess.ShouldBeTrue();
        (await admin.PurgeAsync(id, _ct)).IsSuccess.ShouldBeTrue();
    }

    private async Task<(SpaceId First, SpaceId Second)> CreateWithSameHighWaterMarkAsync(
        Func<Task<SpaceId>> firstCreate,
        Func<Task<SpaceId>> secondCreate)
    {
        using var parent = new Activity("pool-id-allocation-race").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var resumeFirstRead = new ManualResetEventSlim();
        var firstReadReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == StorageTelemetryConstants.MeterName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId != parent.TraceId ||
                    activity.OperationName != "Store.Read" ||
                    !Equals(activity.GetTagItem(StorageTelemetryConstants.Tags.EntityType), SpacePoolIdHighWaterMarkDso.EntityType.Name))
                {
                    return;
                }

                if (Interlocked.Increment(ref readCount) == 1)
                {
                    // Let the other create commit after this caller has read the same mark.
                    firstReadReached.TrySetResult(true);
                    if (!resumeFirstRead.Wait(TimeSpan.FromSeconds(15), _ct))
                    {
                        throw new TimeoutException("Timed out waiting to resume the first high-water-mark read.");
                    }
                }
            }
        };

        ActivitySource.AddActivityListener(listener);
        var first = Task.Run(firstCreate);

        try
        {
            await firstReadReached.Task.WaitAsync(TimeSpan.FromSeconds(15), _ct);
            var second = await secondCreate();
            resumeFirstRead.Set();
            var firstResult = await first;

            Volatile.Read(ref readCount).ShouldBeGreaterThanOrEqualTo(3);
            return (firstResult, second);
        }
        finally
        {
            resumeFirstRead.Set();
            await first;
        }
    }
}
