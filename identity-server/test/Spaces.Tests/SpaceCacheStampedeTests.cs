// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class SpaceCacheStampedeTests : IAsyncLifetime
{
    private const string Origin = "https://stampede.example.com";

    private readonly GatedHybridCache _gated = new();
    private readonly string _dataSourceName = DateTime.UtcNow.ToString("s") + ":" + DateTime.UtcNow.Ticks;
    private ServiceProvider _services = null!;
    private static Ct _ct => TestContext.Current.CancellationToken;
    private static TimeSpan Wait => TimeSpan.FromSeconds(10);

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDsoRegistration<TestDso>();
        sc.AddStorageInternal(b => b.AddSqliteInMemory(dataSourceName: _dataSourceName));
        _ = sc.AddKeyedSingleton<HybridCache>(SpacesServiceProviderKeys.SpacesCache, _gated);
        sc.AddIdentityServerBuilder().AddSpaces(_ => { });
        TestSpacesLicense.RegisterEntitled(sc);
        _services = sc.BuildServiceProvider();

        await _services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(_ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _gated.Dispose();
    }

    private async Task<SpaceId> CreateSpace()
    {
        var admin = _services.GetRequiredService<ISpaceAdmin>();
        var space = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Stampede Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = Origin }]
            },
            _ct);
        return space.Id!;
    }

    private async Task<GatedHybridCache.KeyGate> ArmGate(string key)
    {
        // Create may already have filled the key; clear it so both callers miss L1.
        await _gated.RemoveAsync(key, _ct);
        return _gated.Gate(key);
    }

    private async Task<IPartitionedStorage> OpenStorage(SpaceId spaceId)
    {
        using var scope = _services.GetRequiredService<ISpaceContextAccessor>().SetSpace(spaceId);
        return await _services.GetRequiredService<IPartitionedStorageFactory>()
            .GetPartitionedStorageAsync(DataCategoryName.Spaces, _ct);
    }

    private Task<SpaceResolutionResult?> Resolve() =>
        _services.GetRequiredService<ISpaceStore>()
            .TryResolveSpace(new SpaceMatchPattern { Origin = Origin }, _ct);

    private Task<Space?> Lookup(SpaceId spaceId) =>
        _services.GetRequiredService<ISpaceStore>().TryGetSpace(spaceId, _ct);

    private static async Task ResolveWhileHeld(
        GatedHybridCache.KeyGate gate, Func<Task> held, Func<Task<SpaceResolutionResult?>> resolve, SpaceId spaceId)
    {
        Task? a = null;
        try
        {
            a = held();
            await gate.FactoryStarted.WaitAsync(Wait, _ct);
            var resolved = await resolve().WaitAsync(Wait, _ct);
            resolved.ShouldNotBeNull().SpaceId.ShouldBe(spaceId);
        }
        finally
        {
            gate.Release();
        }

        await a.WaitAsync(Wait, _ct);
    }

    [Fact]
    public async Task resolving_a_space_while_its_routing_lookup_is_in_flight_succeeds()
    {
        var spaceId = await CreateSpace();
        var gate = await ArmGate(SpaceCacheKeys.ForSpaceIdRouting(spaceId));

        IPartitionedStorage? storage = null;
        await ResolveWhileHeld(gate, async () => storage = await OpenStorage(spaceId), Resolve, spaceId);

        storage.ShouldNotBeNull();
    }

    [Fact]
    public async Task resolving_a_space_while_its_by_id_lookup_is_in_flight_succeeds()
    {
        var spaceId = await CreateSpace();
        var gate = await ArmGate(SpaceCacheKeys.ForSpaceId(spaceId));

        Space? space = null;
        await ResolveWhileHeld(gate, async () => space = await Lookup(spaceId), Resolve, spaceId);

        space.ShouldNotBeNull().Id.ShouldBe(spaceId);
    }
}
