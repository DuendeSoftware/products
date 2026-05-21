// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

internal sealed class SqliteStoreFixtureFactory : IStoreFixtureFactory
{
    public async Task<IStoreFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configure = null) =>
        await StoreFixture.CreateAsync(ct, configure);
}
