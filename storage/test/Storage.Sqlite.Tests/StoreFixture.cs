// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

internal sealed class StoreFixture : IStorageFixture
{
    private const string ServiceKey = "test";
    private readonly ServiceProvider _provider;

    public IStorage Storage { get; }

    private StoreFixture(ServiceProvider provider, IStorage storage)
    {
        _provider = provider;
        Storage = storage;
    }

    public static async Task<StoreFixture> CreateAsync(
        Ct ct,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        configure?.Invoke(services);

        var dbName = $"test_{Guid.NewGuid():N}";
        _ = services.AddStorageInternal(storage => storage.AddSqliteStore(ServiceKey, opt =>
            opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);
        await schema.MigrateAsync(ct);

        var pooledStore = provider.GetRequiredKeyedService<IPooledStore>(ServiceKey);
        var storage = pooledStore.OpenPool(1);
        return new StoreFixture(provider, storage);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
}
