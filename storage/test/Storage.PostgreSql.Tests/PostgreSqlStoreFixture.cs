// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.PostgreSql;

internal sealed class PostgreSqlStoreFixture(
    ServiceProvider provider,
    IStorage storage,
    PostgreSqlDatabasePool pool,
    string connectionString) : IStorageFixture
{
    public IStorage Storage { get; } = storage;

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();
        await pool.ReturnAsync(connectionString);
    }
}
