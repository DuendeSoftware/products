// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.PostgreSql;

internal sealed class PostgreSqlStorageEngineFixtureFactory(AspireFixture aspire) : IStorageFixtureFactory
{
    public async Task<IStorageFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configureServices = null) =>
        await StorageFixture.Create(
            configureStorage: AddStorageInstance,
            configureServices: configureServices,
            beforeMigrate: CreateTestDatabasesAsync,
            ct: ct
        );

    public void AddStorageInstance(IStorageBuilder storage)
    {
        var db = aspire.DatabaseProvisioner.Provision();
        _ = storage.Services.AddSingleton(_ => db);
        _ = storage.AddPostgreSql(_ => db.DataSource);
    }

    private static async Task CreateTestDatabasesAsync(IServiceProvider serviceProvider, Ct ct)
    {
        foreach (var db in serviceProvider.GetServices<ProvisionedTestDatabase>())
        {
            await db.CreateAsync(ct);
        }
    }
}
