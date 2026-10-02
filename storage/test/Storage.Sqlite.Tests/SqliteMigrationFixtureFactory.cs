// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

internal sealed class SqliteMigrationFixtureFactory : IMigrationFixtureFactory
{
    public async Task<IMigrationFixture> CreateAsync(CancellationToken ct)
    {
        var dbName = $"migration_test_{Guid.NewGuid():N}";
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddStorageInternal(storage => storage.AddSqlite(opt => opt.ConnectionString = connectionString));
        var provider = services.BuildServiceProvider();

        var storageInstanceSchema = await provider.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(ct);
        IMigrationFixture fixture = new SqliteMigrationFixture(provider, storageInstanceSchema, connectionString);
        return fixture;
    }
}

internal sealed class SqliteMigrationFixture(
    ServiceProvider provider,
    IStorageInstanceSchema storageInstanceSchema,
    string connectionString) : IMigrationFixture
{
    public uint RequiredVersion => 2u;
    public IStorageInstanceSchema StorageInstanceSchema => storageInstanceSchema;

    public async Task ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        // Split into individual statements so that ALTER TABLE ADD COLUMN
        // errors (duplicate column) can be swallowed when running scripts
        // idempotently, matching the same behaviour as IF NOT EXISTS guards
        // in PostgreSQL/MsSql migrations.
        var statements = sql
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s));

        foreach (var statement in statements)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = statement;
            try
            {
                _ = await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
            {
                // Column already exists — idempotent ADD COLUMN is not natively
                // supported in SQLite, so we swallow this specific error.
            }
        }
    }

    public async ValueTask DisposeAsync() => await provider.DisposeAsync();
}
