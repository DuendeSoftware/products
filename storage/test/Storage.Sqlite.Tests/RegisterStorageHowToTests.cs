// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

// begin-snippet: register-storage-catalog-category
public static class CatalogDataCategories
{
    extension(DataCategoryName)
    {
        /// <summary>
        /// The storage category for this product's catalog data. Modeled on IdentityServer's
        /// and User Management's own <c>DataCategoryName</c> extension properties.
        /// </summary>
        public static DataCategoryName Catalog => DataCategoryName.Create("catalog");
    }
}
// end-snippet

public sealed class RegisterStorageHowToTests
{
    // begin-snippet: register-storage-catalog-dso
    public static class CatalogItemDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(8001, nameof(CatalogItemDso));

        public sealed record V1(string Sku, string Title) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    [Fact]
    public async Task Default_instance_is_usable_once_a_single_provider_is_registered()
    {
        var ct = TestContext.Current.CancellationToken;

        // begin-snippet: register-storage-default-instance
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddStorageInternal(storage => storage.AddSqliteInMemory());
        // end-snippet

        await using var provider = services.BuildServiceProvider();

        await MigrateAsync(provider, StorageInstanceId.Default, ct);

        var storage = provider.GetRequiredKeyedService<IPartitionedStorage>(StorageInstanceId.Default);
        _ = storage.ShouldNotBeNull();
    }

    [Fact]
    public void Registering_a_second_provider_for_the_same_instance_throws()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = Should.Throw<InvalidOperationException>(() =>
        {
            // begin-snippet: register-storage-two-providers-throws
            _ = services.AddStorageInternal(storage =>
            {
                _ = storage.AddSqliteInMemory();
                // A second provider call for the same instance throws immediately: a storage
                // instance can only ever be backed by one database provider.
                _ = storage.AddSqliteInMemory();
            });
            // end-snippet
        });
    }

    private static IServiceCollection BuildServicesWithNamedCatalogInstance(StorageInstanceId catalogInstanceId)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddStorageInternal(storage => storage.AddSqliteInMemory());
        _ = services.AddStorageInternal(catalogInstanceId, storage => storage.AddSqliteInMemory());
        services.GetOrAddStorageInstanceRouter().AddMapping(DataCategoryName.Catalog, catalogInstanceId);
        services.AddDsoRegistration<CatalogItemDso.V1>();
        return services;
    }

    [Fact]
    public async Task Mapped_category_is_stored_in_its_own_instance_not_the_default_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var catalogInstanceId = StorageInstanceId.Create("catalog");

        // begin-snippet: register-storage-named-instance-and-mapping
        var services = new ServiceCollection();
        _ = services.AddLogging();

        // The default instance stores everything that isn't mapped elsewhere.
        _ = services.AddStorageInternal(storage => storage.AddSqliteInMemory());

        // A separate database for catalog data, registered under its own StorageInstanceId.
        _ = services.AddStorageInternal(catalogInstanceId, storage => storage.AddSqliteInMemory());

        // Route the Catalog category to that instance. Without this mapping, DataCategoryName.Catalog
        // would resolve to StorageInstanceId.Default instead.
        services.GetOrAddStorageInstanceRouter().AddMapping(DataCategoryName.Catalog, catalogInstanceId);

        services.AddDsoRegistration<CatalogItemDso.V1>();
        // end-snippet

        await using var provider = services.BuildServiceProvider();

        await MigrateAsync(provider, StorageInstanceId.Default, ct);
        await MigrateAsync(provider, catalogInstanceId, ct);

        var factory = provider.GetRequiredService<IPartitionedStorageFactory>();
        var catalogStorage = await factory.GetPartitionedStorageAsync(DataCategoryName.Catalog, ct);

        var id = UuidV7.New();
        var item = new CatalogItemDso.V1("SKU-1", "Widget");
        var createResult = await catalogStorage.CreateAsync(id, item, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        createResult.ShouldBe(CreateResult.Success);

        // The row lives in the catalog instance's own database...
        var fromCatalogInstance = await catalogStorage.TryReadAsync(CatalogItemDso.EntityType, id, ct);
        fromCatalogInstance.Found.ShouldBeTrue();

        // ...and is absent from the default instance's database, because the category mapping
        // routed the write to a different database entirely. Any unmapped category resolves to
        // Default, so this reuses that resolution instead of the raw keyed service (which starts
        // scoped to the management pool, not PoolId.Default).
        var defaultStorage = await factory.GetPartitionedStorageAsync(DataCategoryName.Create("misc"), ct);
        var fromDefaultInstance = await defaultStorage.TryReadAsync(CatalogItemDso.EntityType, id, ct);
        fromDefaultInstance.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Unmapped_category_falls_back_to_the_default_instance()
    {
        var ct = TestContext.Current.CancellationToken;
        var catalogInstanceId = StorageInstanceId.Create("catalog");
        var services = BuildServicesWithNamedCatalogInstance(catalogInstanceId);
        await using var provider = services.BuildServiceProvider();

        await MigrateAsync(provider, StorageInstanceId.Default, ct);
        await MigrateAsync(provider, catalogInstanceId, ct);

        // begin-snippet: register-storage-unmapped-category-falls-back-to-default
        var factory = provider.GetRequiredService<IPartitionedStorageFactory>();
        // "misc" has no explicit mapping, so it resolves to StorageInstanceId.Default.
        var miscStorage = await factory.GetPartitionedStorageAsync(DataCategoryName.Create("misc"), ct);
        // end-snippet

        var id = UuidV7.New();
        var createResult = await miscStorage.CreateAsync(
            id, new CatalogItemDso.V1("SKU-2", "Gadget"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        createResult.ShouldBe(CreateResult.Success);

        // The unmapped write landed in the default instance's database: resolving the pool-scoped
        // store through the factory again (any other unmapped category also resolves to Default)
        // finds it.
        var defaultStorage = await factory.GetPartitionedStorageAsync(DataCategoryName.Create("misc"), ct);
        var fromDefaultInstance = await defaultStorage.TryReadAsync(CatalogItemDso.EntityType, id, ct);
        fromDefaultInstance.Found.ShouldBeTrue();
    }

    [Fact]
    public async Task Resolving_a_category_mapped_to_an_unregistered_instance_throws()
    {
        var ct = TestContext.Current.CancellationToken;

        // begin-snippet: register-storage-mapping-to-unregistered-instance-throws
        var services = new ServiceCollection();
        _ = services.AddLogging();

        _ = services.AddStorageInternal(storage => storage.AddSqliteInMemory());

        // "archive" is mapped, but no provider was ever registered for it with AddStorageInternal.
        var archiveInstanceId = StorageInstanceId.Create("archive");
        services.GetOrAddStorageInstanceRouter().AddMapping(DataCategoryName.Create("archive"), archiveInstanceId);
        // end-snippet
        await using var provider = services.BuildServiceProvider();
        await MigrateAsync(provider, StorageInstanceId.Default, ct);

        var factory = provider.GetRequiredService<IPartitionedStorageFactory>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await factory.GetPartitionedStorageAsync(DataCategoryName.Create("archive"), ct));
        exception.Message.ShouldContain("archive");
    }

    // begin-snippet: register-storage-catalog-repository
    internal sealed class CatalogItemRepository(IPartitionedStorageFactory partitionedStorageFactory)
    {
        internal async Task<CreateResult> AddAsync(UuidV7 id, CatalogItemDso.V1 item, Ct ct)
        {
            var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Catalog, ct);
            return await storage.CreateAsync(id, item, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        }

        internal async Task<CatalogItemDso.V1?> TryGetAsync(UuidV7 id, Ct ct)
        {
            var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Catalog, ct);
            var result = await storage.TryReadAsync(CatalogItemDso.EntityType, id, ct);
            return result.Found ? (CatalogItemDso.V1)result.Dso : null;
        }
    }
    // end-snippet

    [Fact]
    public async Task Repository_resolves_its_store_through_the_factory()
    {
        var ct = TestContext.Current.CancellationToken;
        var catalogInstanceId = StorageInstanceId.Create("catalog");
        var services = BuildServicesWithNamedCatalogInstance(catalogInstanceId);
        _ = services.AddTransient<CatalogItemRepository>();
        await using var provider = services.BuildServiceProvider();

        await MigrateAsync(provider, StorageInstanceId.Default, ct);
        await MigrateAsync(provider, catalogInstanceId, ct);

        var repository = provider.GetRequiredService<CatalogItemRepository>();
        var id = UuidV7.New();
        var createResult = await repository.AddAsync(id, new CatalogItemDso.V1("SKU-3", "Thingamajig"), ct);
        createResult.ShouldBe(CreateResult.Success);

        var found = await repository.TryGetAsync(id, ct);
        _ = found.ShouldNotBeNull();
        found.Title.ShouldBe("Thingamajig");
    }

    [Fact]
    public async Task Sqlite_options_expose_the_connection_string()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddStorageInternal(storage =>
        {
            // begin-snippet: register-storage-sqlite-options
            _ = storage.AddSqlite(options => options.ConnectionString = $"Data Source=HowToRegisterStorage_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            // end-snippet
        });

        await using var provider = services.BuildServiceProvider();
        await MigrateAsync(provider, StorageInstanceId.Default, ct);

        var storage = provider.GetRequiredKeyedService<IPartitionedStorage>(StorageInstanceId.Default);
        _ = storage.ShouldNotBeNull();
    }

    private static async Task MigrateAsync(IServiceProvider provider, StorageInstanceId storageInstanceId, Ct ct)
    {
        var schemaFactory = provider.GetRequiredService<IStorageInstanceSchemaFactory>();
        var schema = await schemaFactory.GetStorageInstanceSchema(storageInstanceId, ct);
        await schema.MigrateAsync(ct);
    }
}
