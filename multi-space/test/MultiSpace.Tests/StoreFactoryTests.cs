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

namespace Duende.MultiSpace;

public sealed class StoreFactoryTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;
    private readonly string _dataSourceName = DateTime.UtcNow.ToString("s") + ":" + DateTime.UtcNow.Ticks;
    private static CancellationToken _ct => TestContext.Current.CancellationToken;
    public async ValueTask InitializeAsync()
    {
        _services = BuildServiceProvider(s => s.AddMultiSpace());

        var schema = _services.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
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

        // Resolve a scoped IStoreFactory with the space context set
        await using var scope = _services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        accessor.SetSpace(space.Id!);

        var factory = scope.ServiceProvider.GetRequiredService<IStoreFactory>();
        var store = await factory.GetStore(_ct);

        // Store was resolved without throwing — the factory routed to the correct pool
        store.ShouldNotBeNull();
    }

    [Fact]
    public async Task factory_returns_default_pool_when_no_space_set()
    {
        // SpaceId.Default has no registered configuration -> pool 0 is used
        await using var scope = _services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        accessor.SetSpace(SpaceId.Default);

        var factory = scope.ServiceProvider.GetRequiredService<IStoreFactory>();
        var store = await factory.GetStore(_ct);

        // Pool 0 is always accessible
        store.ShouldNotBeNull();
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

        // Write data via Space A's store
        await using var scopeA = _services.CreateAsyncScope();
        scopeA.ServiceProvider.GetRequiredService<ISpaceContextAccessor>().SetSpace(spaceA.Id!);
        var storeA = await scopeA.ServiceProvider.GetRequiredService<IStoreFactory>().GetStore(_ct);

        var id = UuidV7.New();
        var dso = new TestDso("space-a-data");
        var result = await storeA.CreateAsync(id, dso, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [],
            _ct);
        result.ShouldBe(CreateResult.Success);

        // Read from Space A — should find it
        var readA = await storeA.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
        readA.Found.ShouldBeTrue();

        // Read same ID from Space B — should not find it
        await using var scopeB = _services.CreateAsyncScope();
        scopeB.ServiceProvider.GetRequiredService<ISpaceContextAccessor>().SetSpace(spaceB.Id!);
        var storeB = await scopeB.ServiceProvider.GetRequiredService<IStoreFactory>().GetStore(_ct);

        var readB = await storeB.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
        readB.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task existing_data_in_default_pool_remains_accessible_after_enabling_multi_space()
    {
        var id = UuidV7.New();
        var dso = new TestDso("pre-existing-data");

        // simulate upgrade path. Write a piece of data with multi-space disabled.
        var singleTenantStore = await BuildServiceProvider(_ => { }).GetRequiredService<IStoreFactory>().GetStore(_ct);
        var createResult = await singleTenantStore.CreateAsync(id, dso, [], [], Expiration.NoExpiration, [], _ct);
        createResult.ShouldBe(CreateResult.Success);

        // Now access the same data through MultiSpaceStoreFactory with SpaceId.Default.
        // This proves that enabling multi-space does not move, hide, or break existing data.
        var multiSpaceServiceProvider = BuildServiceProvider(s => s.AddMultiSpace());
        await using var scope = multiSpaceServiceProvider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>().SetSpace(SpaceId.Default);
        var multiTenantSpace = await scope.ServiceProvider.GetRequiredService<IStoreFactory>().GetStore(_ct);

        var readResult = await multiTenantSpace.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
        readResult.Found.ShouldBeTrue("data should be present in default space");
        readResult.Dso.ShouldBeOfType<TestDso>().Value.ShouldBe("pre-existing-data");

        // Sanity check. Try accessing it via a different space
        // This proves that enabling multi-space does not move, hide, or break existing data.
        var space = await multiSpaceServiceProvider.GetRequiredService<ISpaceAdmin>().CreateAsync(
            new CreateSpaceConfiguration()
            {
                Name = "bob",
                MatchPatterns = [new SpaceMatchPattern()
                {
                    Origin = "https://bob.example.com"
                }]
            }, _ct);

        await using var otherScope = multiSpaceServiceProvider.CreateAsyncScope();
        otherScope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>().SetSpace(space.Id!);
        var otherSpaceStore = await otherScope.ServiceProvider.GetRequiredService<IStoreFactory>().GetStore(_ct);

        readResult = await otherSpaceStore.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct);
        readResult.Found.ShouldBeFalse("data should not be present in a different space");
    }
}
