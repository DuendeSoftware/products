// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.PostgreSql;

internal sealed class PostgreSqlStoreFixtureFactory(AspireFixture aspire) : IStoreFixtureFactory
{
    private const string ServiceKey = "test";

    public async Task<IStoreFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configure = null)
    {
        var connectionString = await aspire.Pool.GetConnectionStringAsync(ct);

        var services = new ServiceCollection();
        _ = services.AddLogging();
        configure?.Invoke(services);
        _ = services.AddNpgsqlDataSource(connectionString, serviceKey: ServiceKey);
        _ = services.AddStorageInternal(storage => storage.AddPostgreSqlStore(ServiceKey, _ => { }));
        var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);
        await schema.MigrateAsync(ct);

        var pooledStore = provider.GetRequiredKeyedService<IPooledStore>(ServiceKey);
        var store = pooledStore.OpenPool(1);

        return new PostgreSqlStoreFixture(provider, store, aspire.Pool, connectionString);
    }
}
