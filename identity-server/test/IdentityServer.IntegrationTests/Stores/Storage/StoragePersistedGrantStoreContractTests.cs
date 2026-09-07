// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.PersistedGrants;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class StoragePersistedGrantStoreContractTests : PersistedGrantStoreContractTests
{
    private readonly string _dbName = $"StoragePersistedGrantContract_{Guid.NewGuid():N}";
    private ServiceProvider _provider = null!;
    private readonly List<ServiceProvider> _isolatedProviders = [];

    public override async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<PersistedGrantDso.V1>();
        services.AddScoped<PersistedGrantRepository>();
        services.AddScoped<IPersistedGrantStore, PersistedGrantStore>();

        _provider = services.BuildServiceProvider();

        var schema = _provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        foreach (var provider in _isolatedProviders)
        {
            await provider.DisposeAsync();
        }
        await _provider.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override StoreHandle<IPersistedGrantStore> CreateStore()
    {
        var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
        return StoreHandle.WithDisposable(store, scope);
    }

    protected override async Task<StoreHandle<IPersistedGrantStore>> CreateIsolatedStoreAsync()
    {
        var isolatedDbName = $"StoragePersistedGrantContractIsolated_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={isolatedDbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<PersistedGrantDso.V1>();
        services.AddScoped<PersistedGrantRepository>();
        services.AddScoped<IPersistedGrantStore, PersistedGrantStore>();

        var provider = services.BuildServiceProvider();
        _isolatedProviders.Add(provider);

        var schema = provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);

        var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
        return StoreHandle.WithDisposable(store, scope);
    }
}
