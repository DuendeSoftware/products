// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.PostgreSql.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Duende.Storage.PostgreSql;

internal sealed class PostgreSqlMigrationFixtureFactory(AspireFixture aspire) : IMigrationFixtureFactory
{
    public async Task<IMigrationFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configureServices = null)
    {
        var db = aspire.DatabaseProvisioner.Provision();
        await db.CreateAsync(ct);
        var schemaName = "s_" + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);
        var services = new ServiceCollection();
        _ = services.AddLogging();
        configureServices?.Invoke(services);
        _ = services.AddStorageInternal(storage => storage.AddPostgreSql(
            _ => db.DataSource,
            o => o.SchemaName = schemaName));
        var provider = services.BuildServiceProvider();

        var storageInstanceSchema = await provider.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(ct);
        return new PostgreSqlMigrationFixture(provider, schemaName, storageInstanceSchema, db);
    }
}

internal sealed class PostgreSqlMigrationFixture(
    ServiceProvider provider,
    string schemaName,
    IStorageInstanceSchema storageInstanceSchema,
    ProvisionedTestDatabase db) : IMigrationFixture
{
    public uint RequiredVersion => 3u;
    public string SchemaName => schemaName;
    public IStorageInstanceSchema StorageInstanceSchema => storageInstanceSchema;
    public IPartitionedStorageFactory PartitionedStorageFactory => provider.GetRequiredService<IPartitionedStorageFactory>();
    public ICrossPartitionStorageFactory CrossPartitionStorageFactory => provider.GetRequiredService<ICrossPartitionStorageFactory>();

    /// <summary>
    /// The data source backing this fixture's provisioned database, for tests that need a second,
    /// independently controlled connection/transaction alongside the engine (for example to hold a
    /// row lock open while the engine runs a delete).
    /// </summary>
    public NpgsqlDataSource DataSource => db.DataSource;

    public async Task ExecuteSqlAsync(string sql, Ct ct)
    {
        await using var cnn = await db.DataSource.OpenConnectionAsync(ct);
        await using (var searchPathCmd = cnn.CreateCommand())
        {
            searchPathCmd.CommandText = $"SET search_path TO \"{schemaName}\"";
            _ = await searchPathCmd.ExecuteNonQueryAsync(ct);
        }

        await using var cmd = cnn.CreateCommand();
        cmd.CommandText = sql;
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<object?> ExecuteScalarAsync(string sql, Ct ct)
    {
        await using var cmd = db.DataSource.CreateCommand(sql);
        return await cmd.ExecuteScalarAsync(ct);
    }

    public Task ExecuteMigrationScriptAsync(string sql, Ct ct) => ExecuteSqlAsync(sql, ct);

    public Task DropColumnAsync(string table, string column, Ct ct) =>
        ExecuteSqlAsync($"ALTER TABLE \"{schemaName}\".{table} DROP COLUMN {column}", ct);

    public async Task MigrateToAsync(uint version, Ct ct)
    {
        var scripts = MigrationScriptLoader.GetScripts(typeof(PostgreSqlStorageEngineOptions).Assembly, DatabaseSchemaVersion.Zero, schemaName)
            .Where(s => s.TargetVersion <= version)
            .OrderBy(s => s.TargetVersion);

        foreach (var (_, sql) in scripts)
        {
            await ExecuteSqlAsync(sql, ct);
        }
    }

    public async Task<long> ExecuteScalarCountAsync(string sql, Ct ct)
    {
        await using var cnn = await db.DataSource.OpenConnectionAsync(ct);
        await using (var searchPathCmd = cnn.CreateCommand())
        {
            searchPathCmd.CommandText = $"SET search_path TO \"{schemaName}\"";
            _ = await searchPathCmd.ExecuteNonQueryAsync(ct);
        }

        await using var cmd = cnn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<EntityLinkRow>> DumpEntityLinksAsync(Ct ct)
    {
        await using var cmd = db.DataSource.CreateCommand(
            $"""
             SELECT pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id
             FROM "{schemaName}".entity_links
             """);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<EntityLinkRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new EntityLinkRow(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetGuid(3),
                reader.GetInt32(4),
                reader.GetGuid(5)));
        }

        return rows;
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();

        var dropCommand = db.DataSource.CreateCommand("DROP SCHEMA IF EXISTS \"" + schemaName + "\" CASCADE");
        _ = await dropCommand.ExecuteNonQueryAsync();

        // Intentionally not released back to the provisioner's pool: the database
        // stays checked out until DropAllAsync cleans it up at suite teardown,
        // matching the original migration fixture behaviour. Only the owned
        // NpgsqlDataSource is disposed here.
        await db.DataSource.DisposeAsync();
    }

    public async Task InsertLegacyLinkAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct)
    {
        await using var cnn = await db.DataSource.OpenConnectionAsync(ct);
        await using (var searchPathCmd = cnn.CreateCommand())
        {
            searchPathCmd.CommandText = $"SET search_path TO \"{schemaName}\"";
            _ = await searchPathCmd.ExecuteNonQueryAsync(ct);
        }

        await using var cmd = cnn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO entity_links (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id)
            VALUES ($1, $2, $3, $4, $5, $6)
            """;
        _ = cmd.Parameters.AddWithValue(poolId);
        _ = cmd.Parameters.AddWithValue(linkTypeId);
        _ = cmd.Parameters.AddWithValue(leftEntityTypeId);
        _ = cmd.Parameters.AddWithValue(leftEntityId);
        _ = cmd.Parameters.AddWithValue(rightEntityTypeId);
        _ = cmd.Parameters.AddWithValue(rightEntityId);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> CountEntityLinksAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct)
    {
        await using var cnn = await db.DataSource.OpenConnectionAsync(ct);
        await using (var searchPathCmd = cnn.CreateCommand())
        {
            searchPathCmd.CommandText = $"SET search_path TO \"{schemaName}\"";
            _ = await searchPathCmd.ExecuteNonQueryAsync(ct);
        }

        await using var cmd = cnn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM entity_links
            WHERE pool_id = $1 AND link_type_id = $2
                AND left_entity_type_id = $3 AND left_entity_id = $4
                AND right_entity_type_id = $5 AND right_entity_id = $6
            """;
        _ = cmd.Parameters.AddWithValue(poolId);
        _ = cmd.Parameters.AddWithValue(linkTypeId);
        _ = cmd.Parameters.AddWithValue(leftEntityTypeId);
        _ = cmd.Parameters.AddWithValue(leftEntityId);
        _ = cmd.Parameters.AddWithValue(rightEntityTypeId);
        _ = cmd.Parameters.AddWithValue(rightEntityId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    // PostgreSQL gates each migration step on the stored version, so a rerun always completes as a
    // no-op: no exception is ever the expected outcome here.
    public bool IsExpectedRerunFailure(Exception exception) => false;

    public bool MigrationScriptsAreRerunnable => true;
}
