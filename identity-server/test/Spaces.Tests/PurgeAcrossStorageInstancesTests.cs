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
using Microsoft.Extensions.DependencyInjection.Extensions;
using PoolId = Duende.Storage.PoolId;

namespace Duende.Spaces;

public sealed class PurgeAcrossStorageInstancesTests : IAsyncLifetime
{
    private static readonly StorageInstanceId SpacesInstance = StorageInstanceId.Create("spaces_instance");
    private static readonly StorageInstanceId ProductInstance = StorageInstanceId.Create("product_instance");
    private static readonly DataCategoryName ProductCategory = DataCategoryName.Create("product_data");

    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;
    private ISpaceContextAccessor _spaceContext = null!;
    private static Ct _ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _services = await BuildServiceProviderAsync(_ => { });
        _admin = _services.GetRequiredService<ISpaceAdmin>();
        _spaceContext = _services.GetRequiredService<ISpaceContextAccessor>();
    }

    private async Task<ServiceProvider> BuildServiceProviderAsync(Action<IServiceCollection> configure)
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDsoRegistration<TestDso>();
        sc.AddStorageInternal(SpacesInstance, b => b.AddSqliteInMemory($"spaces_{_suffix}"));
        sc.AddStorageInternal(ProductInstance, b => b.AddSqliteInMemory($"product_{_suffix}"));
        sc.AddIdentityServerBuilder().AddSpaces(SpacesInstance, _ => { });
        sc.GetOrAddStorageInstanceRouter().AddMapping(ProductCategory, ProductInstance);
        TestSpacesLicense.RegisterEntitled(sc);
        configure(sc);

        var services = sc.BuildServiceProvider();
        foreach (var instance in new[] { SpacesInstance, ProductInstance })
        {
            await services.GetRequiredKeyedService<IStorageInstanceSchema>(instance).MigrateAsync(_ct);
        }

        return services;
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public async Task purge_removes_product_data_held_in_another_storage_instance()
    {
        var first = await CreateSpaceAsync(_admin, "first", (PoolId)70);
        var id = UuidV7.New();

        using (_spaceContext.SetSpace(first))
        {
            var store = await _services.GetRequiredService<IPartitionedStorageFactory>()
                .GetPartitionedStorageAsync(ProductCategory, _ct);
            (await store.CreateAsync(id, new TestDso("secret"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);
        }

        (await _admin.DeleteAsync(first, _ct)).IsSuccess.ShouldBeTrue();
        (await _admin.PurgeAsync(first, _ct)).IsSuccess.ShouldBeTrue();

        var second = await CreateSpaceAsync(_admin, "second", (PoolId)70);

        using (_spaceContext.SetSpace(second))
        {
            var store = await _services.GetRequiredService<IPartitionedStorageFactory>()
                .GetPartitionedStorageAsync(ProductCategory, _ct);
            (await store.TryReadAsync(TestDso.DsoVersion.EntityType, id, _ct)).Found.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task failed_purge_in_one_instance_keeps_the_space_and_its_pool_id_reserved()
    {
        await using var services = await BuildServiceProviderAsync(sc =>
            sc.Replace(ServiceDescriptor.Transient<ICrossPartitionStorageFactory>(sp =>
                new FailingCrossPartitionStorageFactory(sp, SpacesInstance))));

        var admin = services.GetRequiredService<ISpaceAdmin>();
        var space = await CreateSpaceAsync(admin, "failing", (PoolId)71);

        var productRowId = UuidV7.New();
        using (services.GetRequiredService<ISpaceContextAccessor>().SetSpace(space))
        {
            var store = await services.GetRequiredService<IPartitionedStorageFactory>()
                .GetPartitionedStorageAsync(ProductCategory, _ct);
            (await store.CreateAsync(productRowId, new TestDso("healthy"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], _ct))
                .ShouldBe(CreateResult.Success);
        }

        (await admin.DeleteAsync(space, _ct)).IsSuccess.ShouldBeTrue();

        var purge = await admin.PurgeAsync(space, _ct);

        purge.IsSuccess.ShouldBeFalse();
        purge.Errors!.ShouldContain(e =>
            e.Code == "purge_incomplete" &&
            e.Message.Contains("spaces_instance", StringComparison.Ordinal) &&
            !e.Message.Contains("product_instance", StringComparison.Ordinal));

        var kept = await admin.GetAsync(space, _ct);
        kept.Found.ShouldBeTrue();
        kept.Item!.IsDeleted.ShouldBeTrue();

        // The deleted space can't be entered, so read the pool directly. The failing instance is
        // registered first, so this also shows the purge kept going after the failure.
        var productStore = services.GetRequiredService<DefaultPartitionedStorageFactory>()
            .GetPartitionedStorage(ProductCategory, (PoolId)71);
        (await productStore.TryReadAsync(TestDso.DsoVersion.EntityType, productRowId, _ct)).Found.ShouldBeFalse();

        var reuse = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "reuse",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://reuse.example.com" }],
                PoolId = (PoolId)71
            },
            _ct);
        reuse.IsSuccess.ShouldBeFalse();
        reuse.Errors!.ShouldContain(e => e.Message.Contains("already in use", StringComparison.Ordinal));
    }

    private sealed class FailingCrossPartitionStorageFactory(IServiceProvider services, StorageInstanceId failingInstance)
        : ICrossPartitionStorageFactory
    {
        public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, Ct ct) =>
            storageInstanceId == failingInstance
                ? throw new InvalidOperationException("Simulated purge failure.")
                : Task.FromResult(services.GetRequiredKeyedService<ICrossPartitionStorage>(storageInstanceId));
    }

    private static async Task<SpaceId> CreateSpaceAsync(ISpaceAdmin admin, string name, PoolId poolId)
    {
        var result = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = name,
                MatchPatterns = [new SpaceMatchPattern { Origin = $"https://{name}.example.com" }],
                PoolId = poolId
            },
            _ct);
        result.IsSuccess.ShouldBeTrue();
        return result.Id!;
    }
}
