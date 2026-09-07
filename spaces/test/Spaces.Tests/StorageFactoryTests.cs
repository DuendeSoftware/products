// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
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

        var schema = _services.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
        _spaceContext = _services.GetRequiredService<ISpaceContextAccessor>();
    }

    private ServiceProvider BuildServiceProvider(Action<IServiceCollection> configure)
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDsoRegistration<TestDso>();
        sc.AddStorageInternal(b => b.AddSqliteInMemoryStore(dataSourceName: _dataSourceName));
        configure(sc);
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
            var factory = _services.GetRequiredService<IStorageFactory>();
            var storage = await factory.GetStorage(_ct);

            // Store was resolved without throwing — the factory routed to the correct pool
            storage.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task factory_returns_default_pool_when_no_space_set()
    {
        using (_spaceContext.SetSpace(SpaceId.Default))
        {
            var factory = _services.GetRequiredService<IStorageFactory>();
            var storage = await factory.GetStorage(_ct);

            // Pool 0 is always accessible
            storage.ShouldNotBeNull();
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
            var storeA = await _services.GetRequiredService<IStorageFactory>().GetStorage(_ct);
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
            var storeB = await _services.GetRequiredService<IStorageFactory>().GetStorage(_ct);
            var readB = await storeB.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
            readB.Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task existing_data_in_default_pool_remains_accessible_after_enabling_multi_space()
    {
        var id = UuidV7.New();
        var dso = new TestDso("pre-existing-data");

        // Simulate upgrade path. Write a piece of data with Spaces disabled.
        var singleTenantStore = await BuildServiceProvider(_ => { }).GetRequiredService<IStorageFactory>().GetStorage(_ct);
        var createResult = await singleTenantStore.CreateAsync(id, dso, [], [], Expiration.NoExpiration, [], _ct);
        createResult.ShouldBe(CreateResult.Success);

        // Now access the same data through SpacesStorageFactory with SpaceId.Default.
        // This proves that enabling Spaces does not move, hide, or break existing data.
        var spacesServiceProvider = BuildServiceProvider(s => s.AddSpaces());
        var spacesContext = spacesServiceProvider.GetRequiredService<ISpaceContextAccessor>();

        using (spacesContext.SetSpace(SpaceId.Default))
        {
            var multiTenantSpace = await spacesServiceProvider.GetRequiredService<IStorageFactory>().GetStorage(_ct);

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
            var otherSpaceStore = await spacesServiceProvider.GetRequiredService<IStorageFactory>().GetStorage(_ct);

            var readResult = await otherSpaceStore.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
            readResult.Found.ShouldBeFalse("data should not be present in a different space");
        }
    }
}
