// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

internal sealed class SqliteStorageEngineFixtureFactory : IStorageFixtureFactory
{
    public async Task<IStorageFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configureServices = null) =>
        await StorageFixture.Create(
            configureStorage: AddStorageInstance,
            configureServices: configureServices,
            ct: ct
        );

    public void AddStorageInstance(IStorageBuilder storage) => storage.AddSqliteInMemory();
}
