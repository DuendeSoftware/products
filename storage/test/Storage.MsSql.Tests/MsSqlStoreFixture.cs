// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.MsSql;

internal sealed class MsSqlStoreFixture(
    ServiceProvider provider,
    IStore store,
    MsSqlDatabasePool pool,
    string connectionString) : IStoreFixture
{
    public IStore Store { get; } = store;

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();
        await pool.ReturnAsync(connectionString);
    }
}
