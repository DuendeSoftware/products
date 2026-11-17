// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.RegularExpressions;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.MsSql.Internal;
using Duende.Storage.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.MsSql;

internal sealed class MsSqlMigrationFixtureFactory(AspireFixture aspire) : IMigrationFixtureFactory
{
    public Task<IMigrationFixture> CreateAsync(Ct ct, Action<IServiceCollection>? configureServices = null) =>
        CreateAsync(caseSensitiveIdentifiers: null, ct, configureServices);

    internal async Task<IMigrationFixture> CreateAsync(bool? caseSensitiveIdentifiers, Ct ct, Action<IServiceCollection>? configureServices = null)
    {
        var db = aspire.DatabaseProvisioner.Provision(caseSensitiveIdentifiers);
        await db.CreateAsync(ct);
        var schemaName = "s_" + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);

        var services = new ServiceCollection();
        _ = services.AddLogging();
        configureServices?.Invoke(services);
        _ = services.AddStorageInternal(storage => storage.AddMsSql(_ => db.Connect, o => o.SchemaName = schemaName));
        var provider = services.BuildServiceProvider();

        var storageInstanceSchema = await provider.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(ct);
        return new MsSqlMigrationFixture(provider, schemaName, storageInstanceSchema, db);
    }
}

internal sealed class MsSqlMigrationFixture(
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
    /// The connection string backing this fixture's provisioned database, for tests that need a
    /// second, independently controlled connection/transaction alongside the engine (for example to
    /// hold a row lock open while the engine runs a delete or a batch update).
    /// </summary>
    public string ConnectionString => db.ConnectionString;

    public Task ExecuteSqlAsync(string sql, Ct ct) => ExecuteSqlAsync(sql, qualify: true, ct);

    public Task ExecuteMigrationScriptAsync(string sql, Ct ct) => ExecuteSqlAsync(sql, qualify: false, ct);

    /// <summary>
    /// Runs <paramref name="sql"/> against the database backing this fixture, splitting it into
    /// <c>GO</c> batches. When <paramref name="qualify"/> is <see langword="true"/>, unqualified
    /// storage table names are schema-qualified first (see <see cref="QualifyTables"/>) — but only
    /// for engine-independent SQL written by hand in tests, never for migration scripts: those also
    /// contain SQL string literals (e.g. V002's <c>TABLE_NAME = 'outbox_subscriber_queue'</c> guard
    /// against <c>INFORMATION_SCHEMA.COLUMNS</c>), and <see cref="QualifyTables"/> would rewrite a
    /// literal just as readily as a real table reference, silently breaking the script's own logic.
    /// Migration scripts already name their tables fully qualified by schema, so they never need it.
    /// </summary>
    private async Task ExecuteSqlAsync(string sql, bool qualify, Ct ct)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);

        // Split on GO batch separators so each migration script runs in its
        // own batch (required because scripts declare local variables that
        // would conflict if concatenated into a single batch).
        var batches = sql.Split(["\nGO\n", "\r\nGO\r\n"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = qualify ? QualifyTables(batch) : batch;
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<object?> ExecuteScalarAsync(string sql, Ct ct)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync(ct);
    }

    public Task DropColumnAsync(string table, string column, Ct ct) =>
        ExecuteSqlAsync($"ALTER TABLE [{schemaName}].[{table}] DROP COLUMN [{column}]", ct);

    public async Task MigrateToAsync(uint version, Ct ct)
    {
        var scripts = MigrationScriptLoader.GetScripts(typeof(MsSqlStorageEngineOptions).Assembly, DatabaseSchemaVersion.Zero, schemaName)
            .Where(s => s.TargetVersion <= version)
            .OrderBy(s => s.TargetVersion);

        foreach (var (_, sql) in scripts)
        {
            // Migration scripts are executed verbatim: they already qualify their own table
            // references by schema, and QualifyTables must not touch them — it would also rewrite
            // SQL string literals the scripts rely on (see ExecuteSqlAsync's qualify parameter).
            await ExecuteMigrationScriptAsync(sql, ct);
        }
    }

    public async Task<long> ExecuteScalarCountAsync(string sql, Ct ct)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = QualifyTables(sql);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The shared integration tests run engine-independent SQL that names storage tables
    /// unqualified (e.g. <c>SELECT COUNT(*) FROM entity_links</c>). SQL Server has no per-session
    /// default schema like PostgreSQL's <c>search_path</c>, so this fixture schema-qualifies the
    /// known storage table names itself before running the SQL. Already-qualified references
    /// (preceded by <c>.</c> or <c>[</c>) are left untouched.
    /// </summary>
    private string QualifyTables(string sql)
    {
        foreach (var table in KnownTables)
        {
            sql = Regex.Replace(sql, $@"(?<![.\[\w]){table}(?!\w)", $"[{schemaName}].[{table}]");
        }
        return sql;
    }

    private static readonly string[] KnownTables =
    [
        "entity_links",
        "entity_keys",
        "search_values",
        "outbox_subscriber_queue",
        "entities",
        "__schema_info",
    ];

    public async Task<IReadOnlyList<EntityLinkRow>> DumpEntityLinksAsync(Ct ct)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id
            FROM [{schemaName}].[entity_links]
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<EntityLinkRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new EntityLinkRow(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                SqlServerGuidConverter.ToUuidV7(reader.GetGuid(3)),
                reader.GetInt32(4),
                SqlServerGuidConverter.ToUuidV7(reader.GetGuid(5))));
        }

        return rows;
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();

        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            DECLARE @sql NVARCHAR(MAX) = N'';

            -- Drop all foreign keys
            SELECT @sql += 'ALTER TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';' + CHAR(10)
            FROM sys.foreign_keys fk
            INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = N'{schemaName}';
            EXEC sp_executesql @sql;

            -- Drop all types
            SET @sql = N'';
            SELECT @sql += 'DROP TYPE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(10)
            FROM sys.types t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = N'{schemaName}' AND t.is_user_defined = 1;
            EXEC sp_executesql @sql;

            -- Drop all tables
            SET @sql = N'';
            SELECT @sql += 'DROP TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(10)
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = N'{schemaName}';
            EXEC sp_executesql @sql;

            -- Drop extended properties on the schema
            IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 3 AND major_id = SCHEMA_ID(N'{schemaName}'))
                EXEC sys.sp_dropextendedproperty @name = N'SchemaVersion', @level0type = N'SCHEMA', @level0name = N'{schemaName}';

            -- Drop the schema
            DROP SCHEMA IF EXISTS [{schemaName}];
            """;
        _ = await cmd.ExecuteNonQueryAsync();
        // Intentionally not released back to the provisioner's pool: the database
        // stays checked out until DropAllAsync cleans it up at suite teardown,
        // matching the original migration fixture behaviour.
    }

    public async Task InsertLegacyLinkAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO [{schemaName}].[entity_links] (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id)
            VALUES (@poolId, @linkTypeId, @leftEntityTypeId, @leftEntityId, @rightEntityTypeId, @rightEntityId)
            """;
        _ = cmd.Parameters.AddWithValue("@poolId", poolId);
        _ = cmd.Parameters.AddWithValue("@linkTypeId", linkTypeId);
        _ = cmd.Parameters.AddWithValue("@leftEntityTypeId", leftEntityTypeId);
        _ = cmd.Parameters.AddWithValue("@leftEntityId", SqlServerGuidConverter.ToSqlServer(leftEntityId));
        _ = cmd.Parameters.AddWithValue("@rightEntityTypeId", rightEntityTypeId);
        _ = cmd.Parameters.AddWithValue("@rightEntityId", SqlServerGuidConverter.ToSqlServer(rightEntityId));
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> CountEntityLinksAsync(int poolId, int linkTypeId, int leftEntityTypeId, Guid leftEntityId, int rightEntityTypeId, Guid rightEntityId, Ct ct)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(*) FROM [{schemaName}].[entity_links]
            WHERE pool_id = @poolId AND link_type_id = @linkTypeId
                AND left_entity_type_id = @leftEntityTypeId AND left_entity_id = @leftEntityId
                AND right_entity_type_id = @rightEntityTypeId AND right_entity_id = @rightEntityId
            """;
        _ = cmd.Parameters.AddWithValue("@poolId", poolId);
        _ = cmd.Parameters.AddWithValue("@linkTypeId", linkTypeId);
        _ = cmd.Parameters.AddWithValue("@leftEntityTypeId", leftEntityTypeId);
        _ = cmd.Parameters.AddWithValue("@leftEntityId", SqlServerGuidConverter.ToSqlServer(leftEntityId));
        _ = cmd.Parameters.AddWithValue("@rightEntityTypeId", rightEntityTypeId);
        _ = cmd.Parameters.AddWithValue("@rightEntityId", SqlServerGuidConverter.ToSqlServer(rightEntityId));
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    // SQL Server gates each migration step on the stored version, so a rerun always completes as a
    // no-op: no exception is ever the expected outcome here.
    public bool IsExpectedRerunFailure(Exception exception) => false;

    public bool MigrationScriptsAreRerunnable => true;
}
