// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.PostgreSql;

public class PostgreSqlStoreTests(AspireFixture fixture) : IClassFixture<AspireFixture>
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private const string ServiceKey = "my-postgresql-storage";

    private ServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        _ = serviceCollection.AddLogging();
        _ = serviceCollection.AddNpgsqlDataSource(fixture.ConnectionString, serviceKey: ServiceKey);
        _ = serviceCollection.AddStorageInternal(storage => storage.AddPostgreSqlStore(ServiceKey, opt => { }));
        return serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void Can_resolve_storage()
    {
        var serviceProvider = CreateServiceProvider();

        var pooledStore = serviceProvider.GetRequiredKeyedService<IPooledStore>(ServiceKey);

        var storage = pooledStore.OpenPool(1);

        _ = storage.ShouldNotBeNull();
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
