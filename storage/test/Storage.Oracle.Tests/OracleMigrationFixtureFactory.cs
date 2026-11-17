// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Oracle.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace Duende.Storage.Oracle;

internal sealed class OracleMigrationFixtureFactory(AspireFixture aspire) : IMigrationFixtureFactory
{
    public async Task<IMigrationFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configureServices = null)
    {
        // Each migration test gets its own fresh Oracle user (schema), independent of
        // the shared provisioner's pool, so the storage writes into that user's own
        // schema (no SchemaName option required) and the schema is guaranteed unmigrated.
        var (connectionString, user) = await OracleTestDatabaseProvisioner.CreateFreshUserAsync(aspire.ServerConnectionString, ct);

        var services = new ServiceCollection();
        _ = services.AddLogging();
        configureServices?.Invoke(services);
        _ = services.AddKeyedSingleton<CreateOracleConnection>("migration-test", () => new OracleConnection(connectionString));
        _ = services.AddStorageInternal(storage => storage.AddOracle(sp => sp.GetRequiredKeyedService<CreateOracleConnection>("migration-test"), _ => { }));
        var provider = services.BuildServiceProvider();

        var storageInstanceSchema = await provider.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(ct);
        return new OracleMigrationFixture(provider, aspire.ServerConnectionString, user, storageInstanceSchema, connectionString);
    }
}

internal sealed class OracleMigrationFixture(
    ServiceProvider provider,
    string serverConnectionString,
    string user,
    IStorageInstanceSchema storageInstanceSchema,
    string connectionString) : IMigrationFixture
{
    public uint RequiredVersion => 3u;
    public string SchemaName => user;
    public IStorageInstanceSchema StorageInstanceSchema => storageInstanceSchema;
    public IPartitionedStorageFactory PartitionedStorageFactory => provider.GetRequiredService<IPartitionedStorageFactory>();
    public ICrossPartitionStorageFactory CrossPartitionStorageFactory => provider.GetRequiredService<ICrossPartitionStorageFactory>();

    /// <summary>
    /// The connection string backing this fixture's provisioned user/schema, for tests that need a
    /// second, independently controlled connection/transaction alongside the engine (for example to
    /// hold a row lock open while the engine runs a delete or a batch update).
    /// </summary>
    public string ConnectionString => connectionString;

    /// <summary>
    /// A privileged connection string (not scoped to this fixture's own user), for tests that need
    /// to query cross-session catalog views (e.g. <c>v$session</c>) that this fixture's own user
    /// isn't granted access to.
    /// </summary>
    public string ServerConnectionString => serverConnectionString;

    /// <summary>
    /// Executes raw SQL against the schema. Oracle runs a single statement per command
    /// and cannot batch DDL, so the script is split on lines containing only a slash
    /// (the SQL*Plus statement terminator) and each statement is executed individually.
    /// </summary>
    public async Task ExecuteSqlAsync(string sql, Ct ct)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);

        foreach (var statement in SplitStatements(sql))
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = statement;
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }

        await using var commitCmd = connection.CreateCommand();
        commitCmd.CommandText = "COMMIT";
        _ = await commitCmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<object?> ExecuteScalarAsync(string sql, Ct ct)
    {
        await using var connection = new OracleConnection(connectionString);
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
        // Oracle's GetScripts returns schema-qualified names via "[[schema]]" replaced with "" (the
        // connection's own user/schema), matching how the engine's own BuildMigrationScript is applied.
        var scripts = MigrationScriptLoader.GetScripts(typeof(OracleStorageEngineOptions).Assembly, DatabaseSchemaVersion.Zero, string.Empty)
            .Where(s => s.TargetVersion <= version)
            .OrderBy(s => s.TargetVersion);

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);

        foreach (var (_, statements) in scripts)
        {
            foreach (var statement in statements)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = statement;
                _ = await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        await using var commitCmd = connection.CreateCommand();
        commitCmd.CommandText = "COMMIT";
        _ = await commitCmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> ExecuteScalarCountAsync(string sql, Ct ct)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<EntityLinkRow>> DumpEntityLinksAsync(Ct ct)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id
            FROM ENTITY_LINKS
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<EntityLinkRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new EntityLinkRow(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                OracleGuidConverter.FromRaw((byte[])reader.GetValue(3)),
                reader.GetInt32(4),
                OracleGuidConverter.FromRaw((byte[])reader.GetValue(5))));
        }

        return rows;
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();

        try
        {
            OracleConnection.ClearAllPools();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await OracleTestDatabaseProvisioner.DropUserAsync(serverConnectionString, user, cts.Token);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.WriteLine($"Failed to drop migration schema {user}: {ex.Message}");
        }
    }

    private static IEnumerable<string> SplitStatements(string sql)
    {
        var current = new System.Text.StringBuilder();
        foreach (var rawLine in sql.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Trim() == "/")
            {
                var statement = current.ToString().Trim();
                if (statement.Length > 0)
                {
                    yield return statement;
                }
                _ = current.Clear();
                continue;
            }

            _ = current.Append(line).Append('\n');
        }

        var last = current.ToString().Trim();
        if (last.Length > 0)
        {
            yield return last;
        }
    }

    public async Task InsertLegacyLinkAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ENTITY_LINKS (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id)
            VALUES (:poolId, :linkTypeId, :leftEntityTypeId, :leftEntityId, :rightEntityTypeId, :rightEntityId)
            """;
        _ = cmd.Parameters.Add(new OracleParameter(":poolId", poolId));
        _ = cmd.Parameters.Add(new OracleParameter(":linkTypeId", linkTypeId));
        _ = cmd.Parameters.Add(new OracleParameter(":leftEntityTypeId", leftEntityTypeId));
        _ = cmd.Parameters.Add(new OracleParameter(":leftEntityId", OracleDbType.Raw) { Value = OracleGuidConverter.ToRaw(leftEntityId) });
        _ = cmd.Parameters.Add(new OracleParameter(":rightEntityTypeId", rightEntityTypeId));
        _ = cmd.Parameters.Add(new OracleParameter(":rightEntityId", OracleDbType.Raw) { Value = OracleGuidConverter.ToRaw(rightEntityId) });
        _ = await cmd.ExecuteNonQueryAsync(ct);

        await using var commitCmd = connection.CreateCommand();
        commitCmd.CommandText = "COMMIT";
        _ = await commitCmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> CountEntityLinksAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM ENTITY_LINKS
            WHERE pool_id = :poolId AND link_type_id = :linkTypeId
                AND left_entity_type_id = :leftEntityTypeId AND left_entity_id = :leftEntityId
                AND right_entity_type_id = :rightEntityTypeId AND right_entity_id = :rightEntityId
            """;
        _ = cmd.Parameters.Add(new OracleParameter(":poolId", poolId));
        _ = cmd.Parameters.Add(new OracleParameter(":linkTypeId", linkTypeId));
        _ = cmd.Parameters.Add(new OracleParameter(":leftEntityTypeId", leftEntityTypeId));
        _ = cmd.Parameters.Add(new OracleParameter(":leftEntityId", OracleDbType.Raw) { Value = OracleGuidConverter.ToRaw(leftEntityId) });
        _ = cmd.Parameters.Add(new OracleParameter(":rightEntityTypeId", rightEntityTypeId));
        _ = cmd.Parameters.Add(new OracleParameter(":rightEntityId", OracleDbType.Raw) { Value = OracleGuidConverter.ToRaw(rightEntityId) });
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    // Oracle gates each migration step on the stored version, so a rerun always completes as a
    // no-op: no exception is ever the expected outcome here.
    public bool IsExpectedRerunFailure(Exception exception) => false;

    public bool MigrationScriptsAreRerunnable => true;
}
