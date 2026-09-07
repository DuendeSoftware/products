// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace Duende.Storage.Oracle;

internal sealed class OracleStoreFixtureFactory(AspireFixture aspire) : IStorageFixtureFactory
{
    private const string ServiceKey = "test";

    public async Task<IStorageFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configure = null)
    {
        var connectionString = await aspire.Pool.GetConnectionStringAsync(ct);

        var services = new ServiceCollection();
        _ = services.AddLogging();
        configure?.Invoke(services);
        _ = services.AddKeyedSingleton<CreateOracleConnection>(ServiceKey, () => new OracleConnection(connectionString));
        _ = services.AddStorageInternal(storage => storage.AddOracleStore(ServiceKey, _ => { }));
        var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredKeyedService<IDatabaseSchema>(ServiceKey);
        await schema.MigrateAsync(ct);

        var pooledStore = provider.GetRequiredKeyedService<IPooledStore>(ServiceKey);
        var storage = pooledStore.OpenPool(1);

        return new OracleStoreFixture(provider, storage, aspire.Pool, connectionString);
    }
}
