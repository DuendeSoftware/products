// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

internal sealed class SqliteMigrationFixtureFactory : IMigrationFixtureFactory
{
    public Task<IMigrationFixture> CreateAsync(CancellationToken ct)
    {
        var dbName = $"migration_test_{Guid.NewGuid():N}";
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddStorageInternal(storage => storage.AddSqliteStore("migration-test", opt => opt.ConnectionString = connectionString));
        var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredKeyedService<IDatabaseSchema>("migration-test");
        IMigrationFixture fixture = new SqliteMigrationFixture(provider, schema, connectionString);
        return Task.FromResult(fixture);
    }
}

internal sealed class SqliteMigrationFixture(
    ServiceProvider provider,
    IDatabaseSchema schema,
    string connectionString) : IMigrationFixture
{
    public IDatabaseSchema Schema => schema;

    public async Task ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync() => await provider.DisposeAsync();
}
