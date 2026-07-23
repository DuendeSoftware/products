// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace Duende.Storage.Oracle;

public class OracleStoreTests(AspireFixture fixture) : IClassFixture<AspireFixture>
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private const string ServiceKey = "my-oracle-store";

    private ServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        _ = serviceCollection.AddLogging();
        var connectionString = fixture.ConnectionString;
        _ = serviceCollection.AddKeyedSingleton<CreateOracleConnection>(ServiceKey, () => new OracleConnection(connectionString));
        _ = serviceCollection.AddStorageInternal(storage => storage.AddOracleStore(ServiceKey, _ => { }));
        return serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void Can_resolve_store()
    {
        var serviceProvider = CreateServiceProvider();

        var pooledStore = serviceProvider.GetRequiredKeyedService<IPooledStore>(ServiceKey);

        var store = pooledStore.OpenPool(1);

        _ = store.ShouldNotBeNull();
    }

    [Fact]
    public async Task Can_create_schema()
    {
        var serviceProvider = CreateServiceProvider();

        var pooledStore = serviceProvider.GetRequiredKeyedService<IPooledStore>(ServiceKey);

        var schemaVersionResult = await pooledStore.CheckVersionAsync(_ct);
        schemaVersionResult.CurrentVersion.ShouldBe(0u);
        schemaVersionResult.IsCompatible.ShouldBeFalse();
        schemaVersionResult.RequiredVersion.ShouldBe(2u);

        await pooledStore.MigrateAsync(_ct);
        schemaVersionResult = await pooledStore.CheckVersionAsync(_ct);
        schemaVersionResult.CurrentVersion.ShouldBe(2u);
    }
}
