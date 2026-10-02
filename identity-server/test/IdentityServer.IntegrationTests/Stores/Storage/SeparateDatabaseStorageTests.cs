// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.PersistedGrants;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

/// <summary>
/// Verifies that configuration and operational storage can target separate databases
/// using the public per-instance storage provider registration API
/// (<see cref="IdentityServerStorageBuilderExtensions"/>).
/// </summary>
/// <remarks>
/// Routing is driven by each repository passing its <c>DataCategoryName</c> to the shared
/// key-aware <see cref="IPartitionedStorageFactory"/>, which resolves the <see cref="DataCategoryName"/> to a
/// <see cref="StorageInstanceId"/> via <see cref="IStorageInstanceRouter"/>, then resolves the keyed
/// <see cref="IPartitionedStorage"/> registered for that instance.
/// </remarks>
public class SeparateDatabaseStorageTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;

    // Controllable clock shared by both keyed stores so the purge test can advance time
    // past a grant's expiration deterministically.
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    // Two distinct storage instances, each backed by its own in-memory database.
    private readonly StorageInstanceId _configStorageInstanceId = StorageInstanceId.Create("config");
    private readonly StorageInstanceId _opsStorageInstanceId = StorageInstanceId.Create("ops");

    private readonly string _configDb = $"Config_{Guid.NewGuid():N}";
    private readonly string _opsDb = $"Ops_{Guid.NewGuid():N}";

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddIdentityServer()
            .AddStorage(_configStorageInstanceId, s => s.AddSqlite(opt =>
                opt.ConnectionString = $"Data Source={_configDb};Mode=Memory;Cache=Shared"))
            .AddStorage(_opsStorageInstanceId, s => s.AddSqlite(opt =>
                opt.ConnectionString = $"Data Source={_opsDb};Mode=Memory;Cache=Shared"))
            .AddConfigurationStorage(_configStorageInstanceId)
            .AddOperationalStorage(_opsStorageInstanceId);

        // The Sqlite stores resolve TimeProvider from the container; override it so the
        // purge test controls expiration timing.
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(_clock));

        _provider = services.BuildServiceProvider();

        // Migrate both databases through the per-instance schema factory.
        var schemaFactory = _provider.GetRequiredService<IStorageInstanceSchemaFactory>();

        var configSchema = await schemaFactory.GetStorageInstanceSchema(_configStorageInstanceId, TestContext.Current.CancellationToken);
        await configSchema.MigrateAsync(TestContext.Current.CancellationToken);

        var opsSchema = await schemaFactory.GetStorageInstanceSchema(_opsStorageInstanceId, TestContext.Current.CancellationToken);
        await opsSchema.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task Operational_store_resolves_and_works()
    {
        await using var scope = _provider.CreateAsyncScope();

        var grantStore = scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
        var ct = TestContext.Current.CancellationToken;

        await grantStore.StoreAsync(new PersistedGrant
        {
            Key = "test-grant",
            Type = "test",
            ClientId = "client",
            SubjectId = "subject",
            Data = "data"
        }, ct);

        var found = await grantStore.GetAsync("test-grant", ct);
        found.ShouldNotBeNull();
        found.Key.ShouldBe("test-grant");
    }

    [Fact]
    public async Task Configuration_store_resolves_and_works()
    {
        await using var scope = _provider.CreateAsyncScope();

        var clientStore = scope.ServiceProvider.GetRequiredService<IClientStore>();
        var ct = TestContext.Current.CancellationToken;

        // The client store resolves against the configuration database, not the operational one.
        var result = await clientStore.FindClientByIdAsync("nonexistent", ct);
        result.ShouldBeNull();
    }


    [Fact]
    public void Mapping_configuration_category_to_a_second_instance_throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var otherStorageInstanceId = StorageInstanceId.Create("config-other");

        Should.Throw<InvalidOperationException>(() =>
            services.AddIdentityServer()
                .AddStorage(_configStorageInstanceId, s => s.AddSqliteInMemory())
                .AddStorage(otherStorageInstanceId, s => s.AddSqliteInMemory())
                .AddConfigurationStorage(_configStorageInstanceId)
                .AddConfigurationStorage(otherStorageInstanceId));
    }

    [Fact]
    public void Mapping_operational_category_to_a_second_instance_throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var otherStorageInstanceId = StorageInstanceId.Create("ops-other");

        Should.Throw<InvalidOperationException>(() =>
            services.AddIdentityServer()
                .AddStorage(_opsStorageInstanceId, s => s.AddSqliteInMemory())
                .AddStorage(otherStorageInstanceId, s => s.AddSqliteInMemory())
                .AddOperationalStorage(_opsStorageInstanceId)
                .AddOperationalStorage(otherStorageInstanceId));
    }


    [Fact]
    public async Task Data_written_to_operational_store_is_not_visible_in_configuration_store()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var scope = _provider.CreateAsyncScope();

        // Write a grant to operational storage.
        var grantStore = scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
        await grantStore.StoreAsync(new PersistedGrant
        {
            Key = "isolation-key",
            Type = "test",
            ClientId = "client",
            SubjectId = "subject",
            Data = "data"
        }, ct);

        // The grant is retrievable from the operational store.
        var found = await grantStore.GetAsync("isolation-key", ct);
        found.ShouldNotBeNull();

        // The configuration database must not contain the grant: querying the operational
        // entity type through the configuration category (mapped to the configuration instance)
        // returns nothing, proving the two instances are backed by physically separate databases.
        var partitionedStorageFactory = scope.ServiceProvider.GetRequiredService<IPartitionedStorageFactory>();
        var configStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Configuration, ct);

        var configRead = await configStorage.TryReadAsync(
            PersistedGrantDso.EntityType,
            DataStorageKey.Create(PersistedGrantKeyDskV1.Create("isolation-key")),
            ct);

        configRead.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Data_written_to_configuration_store_is_not_visible_in_operational_store()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var scope = _provider.CreateAsyncScope();

        var clientAdmin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
        var createResult = await clientAdmin.CreateAsync(
            new CreateClient
            {
                ClientId = $"client_{Guid.NewGuid():N}",
                AllowedGrantTypes = [GrantType.ClientCredentials],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
            }, ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        // The client is retrievable from the configuration store.
        var getResult = await clientAdmin.GetAsync(createResult.Id, ct);
        getResult.Found.ShouldBeTrue();

        // The operational database must not contain the client: querying the configuration
        // entity type through the operational category (mapped to the operational instance)
        // returns nothing, proving the two instances are backed by physically separate databases.
        var partitionedStorageFactory = scope.ServiceProvider.GetRequiredService<IPartitionedStorageFactory>();
        var opsStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Operational, ct);

        var opsRead = await opsStorage.TryReadAsync(ClientDso.EntityType, UuidV7.From(createResult.Id.Value), ct);

        opsRead.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Storage_purge_host_purges_expired_grants_from_the_operational_store()
    {
        var ct = TestContext.Current.CancellationToken;

        // Store a grant that expires one hour from now. Grants are operational data, so
        // this lands in the operational database via DataCategoryName.Operational. The store
        // treats an already-expired create as a no-op, so we use a future expiration and
        // advance the clock below.
        await using (var scope = _provider.CreateAsyncScope())
        {
            var grantStore = scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
            await grantStore.StoreAsync(new PersistedGrant
            {
                Key = "expiring-grant",
                Type = "test",
                ClientId = "client",
                SubjectId = "subject",
                Data = "data",
                Expiration = _clock.GetUtcNow().UtcDateTime.AddHours(1)
            }, ct);

            var before = await grantStore.GetAsync("expiring-grant", ct);
            before.ShouldNotBeNull("grant must exist in the operational store before purge");
        }

        // Advance past the grant's expiration so PurgeExpiredAsync considers it expired.
        _clock.Advance(TimeSpan.FromHours(2));

        // Run the purge host against the real resolver/factory. StoragePurgeHost iterates every
        // instance returned by IStorageInstanceRouter.GetAll() and purges each one, catching
        // any errors per run. If purging skipped the operational instance in this
        // separate-database setup, the expired grant below would survive.
        var options = new IdentityServerOptions();
        options.StoragePurge.EnablePurge = true;
        options.StoragePurge.BatchSize = 100;

        var storageInstanceRouter = _provider.GetRequiredService<IStorageInstanceRouter>();
        var crossPartitionStorageFactory = _provider.GetRequiredService<ICrossPartitionStorageFactory>();
        var host = new StoragePurgeHost(
            storageInstanceRouter,
            crossPartitionStorageFactory,
            options,
            _provider.GetRequiredService<ILogger<StoragePurgeHost>>());

        await host.RunPurgeAsync(ct);

        // The expired grant must be gone, proving the purge targeted the operational
        // database via the operational storage instance.
        await using (var scope = _provider.CreateAsyncScope())
        {
            var grantStore = scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
            var after = await grantStore.GetAsync("expiring-grant", ct);
            after.ShouldBeNull("expired grant should have been purged from the operational store");
        }
    }
}

