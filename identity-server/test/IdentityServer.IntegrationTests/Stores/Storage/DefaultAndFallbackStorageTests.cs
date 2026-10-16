// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public sealed class DefaultAndFallbackStorageTests
{
    [Fact]
    public async Task Default_provider_serves_both_categories_when_no_category_overrides_are_configured()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbName = $"Default_{Guid.NewGuid():N}";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory(dbName))
            .AddConfigurationStorage()
            .AddOperationalStorage();

        await using var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);

        await using var scope = provider.CreateAsyncScope();

        // Configuration data resolves against the single default store.
        var clientStore = scope.ServiceProvider.GetRequiredService<IClientStore>();
        var client = await clientStore.FindClientByIdAsync("nonexistent", ct);
        client.ShouldBeNull();

        // Operational data resolves against the same default store.
        var grantStore = scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
        await grantStore.StoreAsync(new PersistedGrant
        {
            Key = "default-store-key",
            Type = "test",
            ClientId = "client",
            SubjectId = "subject",
            Data = "data"
        }, ct);

        var found = await grantStore.GetAsync("default-store-key", ct);
        found.ShouldNotBeNull();
    }

    [Fact]
    public async Task Registering_a_storage_database_is_enough_to_purge_its_expired_data()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var purgeInterval = TimeSpan.FromMinutes(1);
        var lifetime = TimeSpan.FromHours(1);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddIdentityServer(options =>
            {
                options.StoragePurge.FuzzStartup = false;
                options.StoragePurge.PurgeInterval = purgeInterval;
            })
            .AddStorage(s => s.AddSqliteInMemory($"Default_{Guid.NewGuid():N}"));
        services.AddDsoRegistration<IdentityProviderDso.V1>();
        services.AddSingleton<TimeProvider>(timeProvider);

        await using var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(cancellationToken);

        var storageFactory = provider.GetRequiredService<IPartitionedStorageFactory>();
        var categoryName = DataCategoryName.Create("some-other-category");
        var otherCategoryStorage = await storageFactory.GetPartitionedStorageAsync(categoryName, cancellationToken);

        var entityId = UuidV7.New();
        var identityProvider = new IdentityProviderDso.V1
            { Id = Guid.NewGuid(), Scheme = $"idp_{Guid.NewGuid():N}", Enabled = true, Type = "oidc" };
        var expiration = Expiration.InRelative(lifetime);

        var createResult = await otherCategoryStorage.CreateAsync(
            entityId,
            identityProvider,
            [],
            [],
            expiration,
            [],
            cancellationToken);
        createResult.ShouldBe(CreateResult.Success);

        timeProvider.Advance(lifetime);

        var purgeHost = provider.GetServices<IHostedService>().OfType<StoragePurgeHost>().ShouldHaveSingleItem();
        await purgeHost.StartAsync(cancellationToken);
        await Task.Delay(100, cancellationToken);
        timeProvider.Advance(purgeInterval);
        var stillFound = await WaitForPurgeAsync(otherCategoryStorage, entityId, cancellationToken);
        await purgeHost.StopAsync(cancellationToken);

        stillFound.ShouldBeFalse("expired data in a database registered with AddStorage must be purged");
    }

    [Fact]
    public async Task Unconfigured_configuration_category_falls_back_silently_to_the_default_store()
    {
        var ct = TestContext.Current.CancellationToken;
        var defaultDb = $"Default_{Guid.NewGuid():N}";
        var opsDb = $"Ops_{Guid.NewGuid():N}";
        var opsStorageInstanceId = StorageInstanceId.Create("ops");

        var services = new ServiceCollection();
        services.AddLogging();

        // Deliberately do NOT call AddConfigurationStorage(): that method both registers the
        // configuration admin/store services AND maps DataCategoryName.Configuration to an instance,
        // so calling it would defeat the purpose of this test. Instead, register only the DSO type
        // needed to exercise the storage layer directly (mirroring what AddConfigurationStorage
        // would register for schema/migration purposes), leaving DataCategoryName.Configuration
        // genuinely unmapped.
        services.AddIdentityServer()
            .AddStorage(StorageInstanceId.Default, s => s.AddSqliteInMemory(defaultDb))
            .AddStorage(opsStorageInstanceId, s => s.AddSqliteInMemory(opsDb))
            .AddOperationalStorage(opsStorageInstanceId);
        services.AddDsoRegistration<IdentityProviderDso.V1>();

        await using var provider = services.BuildServiceProvider();

        // Only DataCategoryName.Operational is mapped to opsStorageInstanceId; DataCategoryName.Configuration
        // has no mapping and must resolve against the default instance's schema instead.
        var schemaFactory = provider.GetRequiredService<IStorageInstanceSchemaFactory>();

        var opsSchema = await schemaFactory.GetStorageInstanceSchema(opsStorageInstanceId, ct);
        await opsSchema.MigrateAsync(ct);

        var defaultSchema = await schemaFactory.GetStorageInstanceSchema(StorageInstanceId.Default, ct);
        await defaultSchema.MigrateAsync(ct);

        await using var scope = provider.CreateAsyncScope();

        // Assert the fallback resolution directly: no mapping is registered for
        // DataCategoryName.Configuration, so the resolver falls back to the default instance.
        var storageInstanceRouter = scope.ServiceProvider.GetRequiredService<IStorageInstanceRouter>();
        storageInstanceRouter.Resolve(DataCategoryName.Configuration).ShouldBe(StorageInstanceId.Default);

        // Prove the fallback is honored by the storage factory itself (not just the resolver) by
        // writing data through the Configuration category and reading it back from the default
        // database, independent of any admin/repository registration.
        var partitionedStorageFactory = scope.ServiceProvider.GetRequiredService<IPartitionedStorageFactory>();
        var configurationStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Configuration, ct);

        var idpId = UuidV7.New();
        var createResult = await configurationStorage.CreateAsync(
            idpId,
            new IdentityProviderDso.V1
            {
                Id = Guid.NewGuid(),
                Scheme = $"idp_{Guid.NewGuid():N}",
                Enabled = true,
                Type = "oidc"
            },
            [],
            [],
            Expiration.NoExpiration,
            [],
            ct);
        createResult.ShouldBe(CreateResult.Success);

        var defaultRead = await configurationStorage
            .TryReadAsync(IdentityProviderDso.EntityType, idpId, ct);
        defaultRead.Found.ShouldBeTrue();

        // The ops database is read through the Operational category, which AddOperationalStorage maps
        // explicitly, so this check does not depend on the fallback under test: had the Configuration
        // write been routed to the ops database, the row would be found here.
        var opsStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Operational, ct);
        var opsRead = await opsStorage
            .TryReadAsync(IdentityProviderDso.EntityType, idpId, ct);
        opsRead.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Configured_category_routes_dynamic_schema_data_to_its_own_store_and_isolates_it_from_operational()
    {
        var ct = TestContext.Current.CancellationToken;
        var configDb = $"Config_{Guid.NewGuid():N}";
        var opsDb = $"Ops_{Guid.NewGuid():N}";
        var configStorageInstanceId = StorageInstanceId.Create("config");
        var opsStorageInstanceId = StorageInstanceId.Create("ops");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityServer()
            .AddStorage(StorageInstanceId.Default, s => s.AddSqliteInMemory())
            .AddStorage(configStorageInstanceId, s => s.AddSqliteInMemory(configDb))
            .AddStorage(opsStorageInstanceId, s => s.AddSqliteInMemory(opsDb))
            .AddOperationalStorage(opsStorageInstanceId)
            .AddDynamicSchemas(configStorageInstanceId);

        await using var provider = services.BuildServiceProvider();

        var schemaFactory = provider.GetRequiredService<IStorageInstanceSchemaFactory>();
        var configSchema = await schemaFactory.GetStorageInstanceSchema(configStorageInstanceId, ct);
        await configSchema.MigrateAsync(ct);
        var opsSchema = await schemaFactory.GetStorageInstanceSchema(opsStorageInstanceId, ct);
        await opsSchema.MigrateAsync(ct);

        await using var scope = provider.CreateAsyncScope();

        // AddDynamicSchemas(configStorageInstanceId) wires ISchemaAdmin to DataCategoryName.DynamicSchemas,
        // mapped to configStorageInstanceId (see SchemaServiceCollectionExtensions.RegisterDynamicSchemaStorage).
        var schemaAdmin = scope.ServiceProvider.GetRequiredService<ISchemaAdmin>();
        var schemaId = SchemaId.Create($"custom:{Guid.NewGuid():N}");
        var createResult = await schemaAdmin.CreateAsync(
            new SchemaConfiguration { SchemaId = schemaId, DisplayName = "Widget" }, ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        // The schema is retrievable through the admin API (routed via the dynamic-schemas category).
        var fetched = await schemaAdmin.GetAsync(schemaId, ct);
        fetched.Found.ShouldBeTrue();

        // Prove it physically landed in the configuration database, not the operational one, by
        // standing up independent providers pointed at each shared in-memory database (identified
        // by data source name) and reading the schema back purely through the public ISchemaAdmin
        // API. The original provider is still alive, so the shared-cache in-memory SQLite databases
        // it opened are still backed by a live connection and remain readable from another provider
        // that connects to the same data source name.

        // Positive control: a provider pointed at the configuration database must find the schema,
        // confirming this cross-provider read technique actually observes the shared database.
        (await GetSchemaViaFreshProviderAsync(configDb, schemaId, ct)).Found.ShouldBeTrue();

        // The actual assertion: a provider pointed at the operational database must NOT find the
        // schema, proving it was not routed there.
        (await GetSchemaViaFreshProviderAsync(opsDb, schemaId, ct)).Found.ShouldBeFalse();
    }

    private static async Task<bool> WaitForPurgeAsync(IPartitionedStorage storage, UuidV7 entityId, Ct cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        var readResult = await storage.TryReadAsync(IdentityProviderDso.EntityType, entityId, cancellationToken);
        while (readResult.Found && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, cancellationToken);
            readResult = await storage.TryReadAsync(IdentityProviderDso.EntityType, entityId, cancellationToken);
        }

        return readResult.Found;
    }

    private static async Task<GetResult<SchemaConfiguration>> GetSchemaViaFreshProviderAsync(
        string dataSourceName, SchemaId schemaId, Ct ct)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory(dataSourceName))
            .AddDynamicSchemas();

        await using var provider = services.BuildServiceProvider();

        var schemaFactory = provider.GetRequiredService<IStorageInstanceSchemaFactory>();
        var schema = await schemaFactory.GetStorageInstanceSchema(StorageInstanceId.Default, ct);
        await schema.MigrateAsync(ct);

        var schemaAdmin = provider.GetRequiredService<ISchemaAdmin>();
        return await schemaAdmin.GetAsync(schemaId, ct);
    }
}

