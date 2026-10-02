// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

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

        // Prove it physically landed in the configuration database, not the operational one.
        var partitionedStorageFactory = provider.GetRequiredService<IPartitionedStorageFactory>();

        var configStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.DynamicSchemas, ct);
        var configRead = await configStorage.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);
        configRead.Found.ShouldBeTrue();

        var opsStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Operational, ct);
        var opsRead = await opsStorage.TryReadAsync(
            AttributeSchemaDso.EntityType,
            DataStorageKey.Create(SchemaIdDskV1.Create(schemaId)),
            ct);
        opsRead.Found.ShouldBeFalse();
    }
}

