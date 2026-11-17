// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

internal sealed class SqliteMigrationFixtureFactory : IMigrationFixtureFactory
{
    public async Task<IMigrationFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configureServices = null)
    {
        var dbName = $"migration_test_{Guid.NewGuid():N}";
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        var services = new ServiceCollection();
        _ = services.AddLogging();
        configureServices?.Invoke(services);
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
    public uint RequiredVersion => 3u;
    public string SchemaName => "main";
    public IStorageInstanceSchema StorageInstanceSchema => storageInstanceSchema;
    public IPartitionedStorageFactory PartitionedStorageFactory => provider.GetRequiredService<IPartitionedStorageFactory>();
    public ICrossPartitionStorageFactory CrossPartitionStorageFactory => provider.GetRequiredService<ICrossPartitionStorageFactory>();

    public async Task ExecuteSqlAsync(string sql, Ct ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        // Split into individual statements and run each as-is: an unguarded ALTER TABLE ADD
        // COLUMN (V002, V003) fails with "duplicate column name" if the script is re-run against
        // a database that already has the column, the same way a real caller running this script
        // by hand would see it fail. This fixture does not swallow that error, so tests exercising
        // a rerun see the real, honest outcome instead of a false "it completed" result.
        var statements = sql
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s));

        foreach (var statement in statements)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = statement;
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<object?> ExecuteScalarAsync(string sql, Ct ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync(ct);
    }

    public Task ExecuteMigrationScriptAsync(string sql, Ct ct) => ExecuteSqlAsync(sql, ct);

    public Task DropColumnAsync(string table, string column, Ct ct) =>
        ExecuteSqlAsync($"ALTER TABLE {table} DROP COLUMN {column}", ct);

    public async Task MigrateToAsync(uint version, Ct ct)
    {
        var scripts = MigrationScriptLoader.GetScripts(typeof(SqliteStorageEngineOptions).Assembly, DatabaseSchemaVersion.Zero)
            .Where(s => s.TargetVersion <= version)
            .OrderBy(s => s.TargetVersion);

        foreach (var (_, sql) in scripts)
        {
            await ExecuteSqlAsync(sql, ct);
        }
    }

    public async Task<long> ExecuteScalarCountAsync(string sql, Ct ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<EntityLinkRow>> DumpEntityLinksAsync(Ct ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id
            FROM entity_links
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<EntityLinkRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new EntityLinkRow(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                Guid.Parse(reader.GetString(3)),
                reader.GetInt32(4),
                Guid.Parse(reader.GetString(5))));
        }

        return rows;
    }

    public async ValueTask DisposeAsync() => await provider.DisposeAsync();

    public async Task InsertLegacyLinkAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO entity_links (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id)
            VALUES (@pool_id, @link_type_id, @left_entity_type_id, @left_entity_id, @right_entity_type_id, @right_entity_id)
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", poolId);
        _ = cmd.Parameters.AddWithValue("@link_type_id", linkTypeId);
        _ = cmd.Parameters.AddWithValue("@left_entity_type_id", leftEntityTypeId);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", leftEntityId.ToString());
        _ = cmd.Parameters.AddWithValue("@right_entity_type_id", rightEntityTypeId);
        _ = cmd.Parameters.AddWithValue("@right_entity_id", rightEntityId.ToString());
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> CountEntityLinksAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM entity_links
            WHERE pool_id = @pool_id AND link_type_id = @link_type_id
                AND left_entity_type_id = @left_entity_type_id AND left_entity_id = @left_entity_id
                AND right_entity_type_id = @right_entity_type_id AND right_entity_id = @right_entity_id
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", poolId);
        _ = cmd.Parameters.AddWithValue("@link_type_id", linkTypeId);
        _ = cmd.Parameters.AddWithValue("@left_entity_type_id", leftEntityTypeId);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", leftEntityId.ToString());
        _ = cmd.Parameters.AddWithValue("@right_entity_type_id", rightEntityTypeId);
        _ = cmd.Parameters.AddWithValue("@right_entity_id", rightEntityId.ToString());
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public bool IsExpectedRerunFailure(Exception exception) =>
        exception is SqliteException && exception.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase);

    // SQLite's V002/V003 steps have an unguarded ALTER TABLE ADD COLUMN, so a rerun fails partway
    // through instead of completing as a no-op (see IsExpectedRerunFailure).
    public bool MigrationScriptsAreRerunnable => false;
}
