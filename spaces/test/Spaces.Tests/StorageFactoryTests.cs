// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class StorageFactoryTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceContextAccessor _spaceContext = null!;
    private ISpaceAdmin _admin = null!;
    private readonly string _dataSourceName = DateTime.UtcNow.ToString("s") + ":" + DateTime.UtcNow.Ticks;
    private static CancellationToken _ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _services = BuildServiceProvider(s => s.AddSpaces());

        var storageInstanceSchema = _services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(_ct);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
        _spaceContext = _services.GetRequiredService<ISpaceContextAccessor>();
    }

    private ServiceProvider BuildServiceProvider(Action<IServiceCollection> configure)
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDsoRegistration<TestDso>();
        sc.AddStorageInternal(b => b.AddSqliteInMemory(dataSourceName: _dataSourceName));
        configure(sc);
        TestSpacesLicense.RegisterEntitled(sc);
        return sc.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public async Task factory_returns_store_for_resolved_pool()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Factory Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://factory.example.com" }]
            },
            _ct);

        var getResult = await _admin.GetAsync(space.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.PoolId.Value.ShouldBeGreaterThan(0);

        using (_spaceContext.SetSpace(space.Id!))
        {
            var partitionedStorageFactory = _services.GetRequiredService<IPartitionedStorageFactory>();
            var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);

            // Store was resolved without throwing — the factory routed to the correct pool
            partitionedStorage.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task factory_returns_default_pool_when_no_space_set()
    {
        using (_spaceContext.SetSpace(SpaceId.Default))
        {
            var partitionedStorageFactory = _services.GetRequiredService<IPartitionedStorageFactory>();
            var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);

            // Pool 0 is always accessible
            partitionedStorage.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task data_written_in_one_space_is_invisible_from_another()
    {
        var spaceA = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space A",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://a.example.com" }]
            },
            _ct);

        var spaceB = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space B",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://b.example.com" }]
            },
            _ct);

        // Write data via Space A's store and verify it's readable
        var id = UuidV7.New();

        using (_spaceContext.SetSpace(spaceA.Id!))
        {
            var storeA = await _services.GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
            var dso = new TestDso("space-a-data");
            var result = await storeA.CreateAsync(id, dso, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [],
                _ct);
            result.ShouldBe(CreateResult.Success);

            // Read from Space A — should find it
            var readA = await storeA.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
            readA.Found.ShouldBeTrue();
        }

        // Read same ID from Space B — should not find it
        using (_spaceContext.SetSpace(spaceB.Id!))
        {
            var storeB = await _services.GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
            var readB = await storeB.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
            readB.Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task storage_obtained_for_one_space_keeps_reading_that_space_after_another_space_is_opened()
    {
        var spaceA = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space A",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://a.example.com" }]
            },
            _ct);

        var spaceB = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space B",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://b.example.com" }]
            },
            _ct);

        var id = UuidV7.New();
        var factory = _services.GetRequiredService<IPartitionedStorageFactory>();

        IPartitionedStorage storeA;
        using (_spaceContext.SetSpace(spaceA.Id!))
        {
            storeA = await factory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
            var result = await storeA.CreateAsync(id, new TestDso("space-a-data"), [], SearchFieldCollection.Empty,
                Expiration.NoExpiration, [], _ct);
            result.ShouldBe(CreateResult.Success);
        }

        using (_spaceContext.SetSpace(spaceB.Id!))
        {
            _ = await factory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
        }

        var readA = await storeA.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
        readA.Found.ShouldBeTrue("storage obtained for space A must not be retargeted when space B's storage is opened");
    }

    [Fact]
    public async Task existing_data_in_default_pool_remains_accessible_after_enabling_multi_space()
    {
        var id = UuidV7.New();
        var dso = new TestDso("pre-existing-data");

        // Simulate upgrade path. Write a piece of data with Spaces disabled.
        var singleTenantStore = await BuildServiceProvider(_ => { }).GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
        var createResult = await singleTenantStore.CreateAsync(id, dso, [], [], Expiration.NoExpiration, [], _ct);
        createResult.ShouldBe(CreateResult.Success);

        // Now access the same data through SpacesPartitionedStorageFactory with SpaceId.Default.
        // This proves that enabling Spaces does not move, hide, or break existing data.
        var spacesServiceProvider = BuildServiceProvider(s => s.AddSpaces());
        var spacesContext = spacesServiceProvider.GetRequiredService<ISpaceContextAccessor>();

        using (spacesContext.SetSpace(SpaceId.Default))
        {
            var multiTenantSpace = await spacesServiceProvider.GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);

            var readResult = await multiTenantSpace.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
            readResult.Found.ShouldBeTrue("data should be present in default space");
            readResult.Dso.ShouldBeOfType<TestDso>().Value.ShouldBe("pre-existing-data");
        }

        // Sanity check. Try accessing it via a different space.
        // This proves that enabling Spaces does not move, hide, or break existing data.
        var space = await spacesServiceProvider.GetRequiredService<ISpaceAdmin>().CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "bob",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://bob.example.com" }]
            }, _ct);

        using (spacesContext.SetSpace(space.Id!))
        {
            var otherSpaceStore = await spacesServiceProvider.GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);

            var readResult = await otherSpaceStore.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
            readResult.Found.ShouldBeFalse("data should not be present in a different space");
        }
    }

    [Fact]
    public async Task factory_throws_when_current_space_has_been_deleted()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Doomed Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://doomed.example.com" }]
            },
            _ct);

        await _admin.DeleteAsync(space.Id!, _ct);

        using (_spaceContext.SetSpace(space.Id!))
        {
            var partitionedStorageFactory = _services.GetRequiredService<IPartitionedStorageFactory>();

            var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct));
            exception.Message.ShouldBe(
                $"Space '{space.Id!.Value}' is not available for data access.");
        }
    }

    [Fact]
    public async Task factory_returns_store_for_live_space()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Live Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://live.example.com" }]
            },
            _ct);

        using (_spaceContext.SetSpace(space.Id!))
        {
            var partitionedStorageFactory = _services.GetRequiredService<IPartitionedStorageFactory>();
            var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);

            partitionedStorage.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task factory_throws_when_current_space_has_been_purged()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Purged Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://purged.example.com" }]
            },
            _ct);

        await _admin.DeleteAsync(space.Id!, _ct);
        await _admin.PurgeAsync(space.Id!, _ct);

        using (_spaceContext.SetSpace(space.Id!))
        {
            var partitionedStorageFactory = _services.GetRequiredService<IPartitionedStorageFactory>();

            var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct));
            exception.Message.ShouldBe(
                $"Space '{space.Id!.Value}' is not available for data access.");
        }
    }

    [Fact]
    public async Task Storage_for_a_space_is_unavailable_while_the_space_is_disabled_and_available_again_once_it_is_re_enabled()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Disabled Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://disabled.example.com" }]
            },
            _ct);

        using (_spaceContext.SetSpace(space.Id!))
        {
            var partitionedStorageFactory = _services.GetRequiredService<IPartitionedStorageFactory>();
            await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);

            await SetEnabledAsync(space.Id!, false);

            var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct));
            exception.Message.ShouldBe(
                $"Space '{space.Id!.Value}' is not available for data access.");

            await SetEnabledAsync(space.Id!, true);

            var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
            storage.ShouldNotBeNull();
        }
    }
    [Fact]
    public async Task Storage_for_a_space_is_unavailable_once_the_space_is_deleted_and_available_again_once_it_is_restored()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Deleted Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://deleted.example.com" }]
            },
            _ct);

        using (_spaceContext.SetSpace(space.Id!))
        {
            var partitionedStorageFactory = _services.GetRequiredService<IPartitionedStorageFactory>();
            await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);

            (await _admin.DeleteAsync(space.Id!, _ct)).IsSuccess.ShouldBeTrue("the space should be deleted");

            var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct));
            exception.Message.ShouldBe(
                $"Space '{space.Id!.Value}' is not available for data access.");

            (await _admin.UndeleteAsync(space.Id!, _ct)).IsSuccess.ShouldBeTrue("the space should be restored");

            var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
            storage.ShouldNotBeNull();
        }
    }
    [Fact]
    public async Task data_written_under_the_management_space_is_isolated_from_the_default_pool()
    {
        var id = UuidV7.New();
        var dso = new TestDso("management-pool-data");

        using (_spaceContext.SetSpace(SpaceId.Management))
        {
            var managementStore = await _services.GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
            var createResult = await managementStore.CreateAsync(id, dso, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], _ct);
            createResult.ShouldBe(CreateResult.Success);

            var readBack = await managementStore.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
            readBack.Found.ShouldBeTrue("data written to the management pool must be readable back from the management pool");
        }

        using (_spaceContext.SetSpace(SpaceId.Default))
        {
            var defaultStore = await _services.GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
            var readFromDefault = await defaultStore.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
            readFromDefault.Found.ShouldBeFalse("the management pool must be a distinct pool from the default pool");
        }
    }

    [Fact]
    public async Task resolving_a_space_means_opening_its_storage_does_not_read_the_space_again()
    {
        const string origin = "https://primed.example.com";
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Primed Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }]
            },
            _ct);
        var poolId = (await _admin.GetAsync(space.Id!, _ct)).Item!.PoolId;

        var resolved = await _services.GetRequiredService<ISpaceStore>()
            .TryResolveSpace(new SpaceMatchPattern { Origin = origin }, _ct);
        resolved.ShouldNotBeNull();

        var readFromStorage = false;
        var cachedPoolId = await _services.GetRequiredService<HybridCache>().GetOrCreateAsync<int?>(
            SpaceCacheKeys.ForSpaceIdRouting(space.Id!),
            _ =>
            {
                readFromStorage = true;
                return ValueTask.FromResult<int?>(null);
            },
            cancellationToken: _ct);

        readFromStorage.ShouldBeFalse("resolving the space should already have cached its pool for routing");
        cachedPoolId.ShouldBe(poolId.Value);

        using (_spaceContext.SetSpace(space.Id!))
        {
            var storage = await _services.GetRequiredService<IPartitionedStorageFactory>()
                .GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
            storage.ShouldNotBeNull();
        }
    }

    private async Task SetEnabledAsync(SpaceId id, bool enabled)
    {
        var current = await _admin.GetAsync(id, _ct);
        var item = current.Item!;
        item.Enabled = enabled;
        var result = await _admin.UpdateAsync(id, item, current.Version!, _ct);
        result.IsSuccess.ShouldBeTrue($"the space should be updated to Enabled = {enabled}");
    }
}
