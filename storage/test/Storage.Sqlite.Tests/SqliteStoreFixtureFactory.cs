// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

internal sealed class SqliteStoreFixtureFactory : IStorageFixtureFactory
{
    public async Task<IStorageFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configure = null) =>
        await StoreFixture.CreateAsync(ct, configure);
}
