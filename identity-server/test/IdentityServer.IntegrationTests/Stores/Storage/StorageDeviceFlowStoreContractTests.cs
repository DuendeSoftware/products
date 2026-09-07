// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.DeviceFlow;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StorageDeviceFlowStoreContractTests : DeviceFlowStoreContractTests
{
    private readonly string _dbName = $"StorageDeviceFlowContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;

    public override async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<DeviceFlowDso.V1>();
        services.AddSingleton<IPoolContextAccessor, PoolContextAccessor>();
        services.AddSingleton<IStorageFactory, DefaultStorageFactory>();
        services.AddScoped<DeviceFlowRepository>();
        services.AddScoped<IPersistentGrantSerializer, PersistentGrantSerializer>();
        services.AddScoped<IDeviceFlowStore, Duende.IdentityServer.Stores.Storage.DeviceFlow.DeviceFlowStore>();

        _provider = services.BuildServiceProvider();

        var schema = _provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override StoreHandle<IDeviceFlowStore> CreateStore()
    {
        var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDeviceFlowStore>();
        return StoreHandle.WithDisposable(store, scope);
    }
}
