// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.PostgreSql;

internal sealed class PostgreSqlStoreFixture(
    ServiceProvider provider,
    IStore store,
    PostgreSqlDatabasePool pool,
    string connectionString) : IStoreFixture
{
    public IStore Store { get; } = store;

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();
        await pool.ReturnAsync(connectionString);
    }
}
