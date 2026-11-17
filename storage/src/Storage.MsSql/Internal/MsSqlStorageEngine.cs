// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Pagination;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Cursors;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.MsSql.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
internal sealed class MsSqlStorageEngine(
    CreateSqlConnection createConnection,
    MsSqlStorageEngineOptions options,
    DataStorageTypeRegistry dataStorageTypeRegistry,
    TimeProvider timeProvider,
    OutboxSubscriptions outboxSubscriptions,
    ILogger<MsSqlStorageEngine> logger) : StoreBase, IPartitionedStorage, ICrossPartitionStorage, IStorageInstanceSchema
{
    private const int RequiredSchemaVersion = 3;
    private readonly string _schemaName = options.SchemaName;

    private SqlConnection OpenConnection() => createConnection();

    async Task<CheckSchemaVersionResult> IStorageInstanceSchema.CheckVersionAsync(Ct ct)
    {
        Log.CheckingSchemaVersion(logger);

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT CAST(JSON_VALUE(CAST(value AS NVARCHAR(MAX)), '$.Version') AS INT) as Version
            FROM sys.extended_properties ep
            WHERE ep.class = 3
              AND ep.name = N'SchemaVersion'
              AND ep.major_id = SCHEMA_ID('{_schemaName}')
            """;

        Log.ExecutingSql(logger, cmd.CommandText);
        var scalar = await cmd.ExecuteScalarAsync(ct);

        if (scalar is null or DBNull)
        {
            return new CheckSchemaVersionResult(0, RequiredSchemaVersion);
        }

        var version = (uint)Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        return new CheckSchemaVersionResult(version, RequiredSchemaVersion);
    }

    async Task IStorageInstanceSchema.MigrateAsync(Ct ct)
    {
        Log.MigratingSchema(logger, _schemaName);

        var versionResult = await ((IStorageInstanceSchema)this).CheckVersionAsync(ct);
        var currentVersion = new DatabaseSchemaVersion((int)versionResult.CurrentVersion);

        var scripts = MigrationScriptLoader.GetScripts(typeof(MsSqlStorageEngine).Assembly, currentVersion, _schemaName).ToList();

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        foreach (var (_, sql) in scripts)
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            // Acquire application lock for safe concurrent migration
            await using (var lockCmd = connection.CreateCommand())
            {
                lockCmd.Transaction = (SqlTransaction)transaction;
                lockCmd.CommandType = CommandType.Text;
                lockCmd.CommandText = """
                    DECLARE @result INT;
                    EXEC @result = sp_getapplock
                       @Resource = N'__schema_migration__',
                       @LockMode = N'Exclusive',
                       @LockOwner = N'Transaction',
                       @LockTimeout = 60000;

                    IF @result < 0
                    BEGIN
                      THROW 50000, 'Failed to acquire application lock for migration', 1;
                    END
                    """;
                Log.ExecutingSql(logger, lockCmd.CommandText);
                _ = await lockCmd.ExecuteNonQueryAsync(ct);
            }

            // Execute the migration script (version gate and version bump are inside the SQL)
            await using (var stepCmd = connection.CreateCommand())
            {
                stepCmd.Transaction = (SqlTransaction)transaction;
                stepCmd.CommandType = CommandType.Text;
                stepCmd.CommandText = sql;
                Log.ExecutingSql(logger, stepCmd.CommandText);
                _ = await stepCmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }

        var verifyResult = await ((IStorageInstanceSchema)this).VerifySchemaAsync(ct);
        if (!verifyResult.IsValid)
        {
            var errors = string.Join("; ", verifyResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Schema verification failed after migration: {errors}");
        }
    }

    async Task<SchemaVerificationResult> IStorageInstanceSchema.VerifySchemaAsync(Ct ct)
    {
        Log.VerifyingSchema(logger, _schemaName);

        var errors = new List<SchemaVerificationError>();

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        // Expected tables and their required columns (table -> column -> data_type)
        var expectedColumns = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["entities"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["pool_id"] = "int",
                ["entity_type_id"] = "int",
                ["entity_id"] = "uniqueidentifier",
                ["original_entity_id"] = "uniqueidentifier",
                ["entity_type_name"] = "nvarchar",
                ["value"] = "nvarchar",
                ["dso_type_schema_version"] = "int",
                ["value_version"] = "int",
                ["created_at"] = "datetimeoffset",
                ["last_updated_at"] = "datetimeoffset",
                ["expires_at"] = "datetimeoffset",
            },
            ["entity_keys"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["pool_id"] = "int",
                ["entity_type_id"] = "int",
                ["key_type_id"] = "int",
                ["entity_id"] = "uniqueidentifier",
                ["key_type_name"] = "nvarchar",
                ["key_type_version"] = "int",
                ["key_value"] = "uniqueidentifier",
                ["key_json"] = "nvarchar",
                ["timestamp"] = "datetimeoffset",
            },
            ["search_values"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["pool_id"] = "int",
                ["entity_type_id"] = "int",
                ["entity_id"] = "uniqueidentifier",
                ["field_path"] = "uniqueidentifier",
                ["field_path_text"] = "nvarchar",
                ["item_index"] = "int",
                ["string_value"] = "nvarchar",
                ["number_value"] = "decimal",
                ["datetime_value"] = "datetimeoffset",
                ["boolean_value"] = "bit",
                ["guid_value"] = "uniqueidentifier",
            },
            ["entity_links"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["pool_id"] = "int",
                ["link_type_id"] = "int",
                ["left_entity_type_id"] = "int",
                ["left_entity_id"] = "uniqueidentifier",
                ["right_entity_type_id"] = "int",
                ["right_entity_id"] = "uniqueidentifier",
                ["restrict_delete"] = "bit",
                ["created_at"] = "datetimeoffset",
            },
            ["outbox_subscriber_queue"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["sequence_number"] = "bigint",
                ["message_id"] = "uniqueidentifier",
                ["event_id"] = "uniqueidentifier",
                ["timestamp"] = "datetimeoffset",
                ["event_name"] = "nvarchar",
                ["subject_id"] = "uniqueidentifier",
                ["entity_type_id"] = "int",
                ["entity_type_name"] = "nvarchar",
                ["pool_id"] = "int",
                ["payload"] = "nvarchar",
                ["subscriber_name"] = "nvarchar",
                ["dso_type_schema_version"] = "int",
            },
        };

        var actualColumns = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var usableTables = new HashSet<string>(StringComparer.Ordinal);
        var usableColumns = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // Query actual columns from INFORMATION_SCHEMA
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $"""
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE,
                       CASE WHEN TABLE_NAME = LOWER(TABLE_NAME) THEN 1 ELSE 0 END,
                       CASE WHEN COLUMN_NAME = LOWER(COLUMN_NAME) THEN 1 ELSE 0 END
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = '{_schemaName}'
                ORDER BY TABLE_NAME, COLUMN_NAME
                """;
            Log.ExecutingSql(logger, cmd.CommandText);

            var actualColumnTypes = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var actualColumnNullability = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var tableName = reader.GetString(0);
                var columnName = reader.GetString(1);
                var dataType = reader.GetString(2);

                if (!actualColumns.TryGetValue(tableName, out var cols))
                {
                    cols = new HashSet<string>(StringComparer.Ordinal);
                    actualColumns[tableName] = cols;
                    actualColumnTypes[tableName] = new Dictionary<string, string>(StringComparer.Ordinal);
                    actualColumnNullability[tableName] = new Dictionary<string, bool>(StringComparer.Ordinal);
                    usableColumns[tableName] = new HashSet<string>(StringComparer.Ordinal);
                }

                _ = cols.Add(columnName);
                actualColumnTypes[tableName][columnName] = dataType;
                actualColumnNullability[tableName][columnName] = reader.GetString(3) == "YES";
                // SQL Server resolves identifiers according to the database's catalog collation.
                if (reader.GetInt32(4) != 0)
                {
                    _ = usableTables.Add(tableName);
                }

                if (reader.GetInt32(5) != 0)
                {
                    _ = usableColumns[tableName].Add(columnName);
                }
            }

            foreach (var (tableName, columns) in expectedColumns)
            {
                var actualTable = SchemaVerification.FindName(usableTables, tableName);
                if (actualTable is null)
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, null,
                        $"Table '{_schemaName}.{tableName}' is missing.",
                        SchemaVerificationErrorKind.MissingTable));
                    continue;
                }

                foreach (var (columnName, expectedType) in columns)
                {
                    var actualColumn = SchemaVerification.FindName(usableColumns[actualTable], columnName);
                    if (actualColumn is null)
                    {
                        errors.Add(new SchemaVerificationError(
                            tableName, columnName,
                            $"Column '{columnName}' is missing from table '{_schemaName}.{tableName}'.",
                            SchemaVerificationErrorKind.MissingColumn));
                    }
                    else
                    {
                        if (!string.Equals(actualColumnTypes[actualTable][actualColumn], expectedType, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(new SchemaVerificationError(
                                tableName, columnName,
                                $"Column '{columnName}' in table '{_schemaName}.{tableName}' has type '{actualColumnTypes[actualTable][actualColumn]}' but expected '{expectedType}'.",
                                SchemaVerificationErrorKind.WrongType));
                        }

                        SchemaVerification.VerifyNullability(
                            errors, tableName, columnName, actualColumnNullability[actualTable][actualColumn]);
                    }
                }
            }
        }

        var actualKeys = new List<SchemaVerification.KeyColumn>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT t.name, k.name, k.type, c.name, CAST(ic.key_ordinal AS int)
                FROM sys.key_constraints AS k
                JOIN sys.tables AS t ON t.object_id = k.parent_object_id
                JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                JOIN sys.indexes AS i ON i.object_id = t.object_id AND i.index_id = k.unique_index_id
                JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE s.name = @schema AND i.is_disabled = 0 AND ic.key_ordinal > 0
                ORDER BY t.name, k.name, ic.key_ordinal
                """;
            _ = cmd.Parameters.AddWithValue("@schema", _schemaName);
            Log.ExecutingSql(logger, cmd.CommandText);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                actualKeys.Add(new SchemaVerification.KeyColumn(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2) == "PK",
                    reader.GetString(3), reader.GetInt32(4)));
            }
        }

        SchemaVerification.VerifyKeys(errors, actualColumns, actualKeys);

        // Verify required indexes
        var expectedIndexes = new (string Table, string Index, string Definition)[]
        {
            ("entities", $"IX_{_schemaName}_entities_expires_at", "entities_expires_at"),
            ("entities", $"IX_{_schemaName}_entities_entity_type_name", "entities_entity_type_name"),
            ("entities", $"IX_{_schemaName}_entities_created_at", "entities_created_at"),
            ("entities", $"IX_{_schemaName}_entities_last_updated_at", "entities_last_updated_at"),
            ("entity_keys", $"IX_{_schemaName}_entity_keys_entity_type_id_entity_id", "entity_keys_entity_type_id_entity_id"),
            ("search_values", $"IX_{_schemaName}_search_values_string_value", "search_values_string_value"),
            ("search_values", $"IX_{_schemaName}_search_values_number_value", "search_values_number_value"),
            ("search_values", $"IX_{_schemaName}_search_values_datetime_value", "search_values_datetime_value"),
            ("search_values", $"IX_{_schemaName}_search_values_boolean_value", "search_values_boolean_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_string_value", "search_values_array_string_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_number_value", "search_values_array_number_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_datetime_value", "search_values_array_datetime_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_boolean_value", "search_values_array_boolean_value"),
            ("search_values", $"IX_{_schemaName}_search_values_guid_value", "search_values_guid_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_guid_value", "search_values_array_guid_value"),
            ("entity_links", $"IX_{_schemaName}_entity_links_right_entity", "entity_links_right_entity"),
            ("entity_links", $"IX_{_schemaName}_entity_links_left_fk", "entity_links_left_fk"),
            ("entity_links", $"IX_{_schemaName}_entity_links_right_fk", "entity_links_right_fk"),
            ("outbox_subscriber_queue", $"IX_{_schemaName}_outbox_subscriber_queue_subscriber", "outbox_subscriber_queue_subscriber"),
            ("outbox_subscriber_queue", $"IX_{_schemaName}_outbox_subscriber_queue_pool_id", "outbox_subscriber_queue_pool_id"),
        };

        var actualIndexes = new List<SchemaVerification.IndexColumn>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = """
                SELECT t.name AS table_name, i.name AS index_name, c.name, CAST(ic.key_ordinal AS int)
                FROM sys.indexes i
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                LEFT JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0
                LEFT JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE s.name = @schema
                  AND i.name IS NOT NULL
                  AND i.is_disabled = 0
                ORDER BY t.name, i.name, ic.key_ordinal
                """;
            _ = cmd.Parameters.AddWithValue("@schema", _schemaName);
            Log.ExecutingSql(logger, cmd.CommandText);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                actualIndexes.Add(new SchemaVerification.IndexColumn(
                    reader.GetString(0), reader.GetString(1),
                    await reader.IsDBNullAsync(2, ct) ? null : reader.GetString(2),
                    await reader.IsDBNullAsync(3, ct) ? 0 : reader.GetInt32(3)));
            }
        }

        SchemaVerification.VerifyIndexes(errors, actualColumns, expectedIndexes, actualIndexes, (tableName, indexName) =>
            new SchemaVerificationError(
                tableName, null,
                $"Index '{indexName}' is missing from table '{_schemaName}.{tableName}'.",
                SchemaVerificationErrorKind.MissingIndex));

        // Verify required foreign keys. entity_keys/search_values are checked by the shared
        // SchemaVerification.VerifyForeignKeys. entity_links has two FKs to the same table, so
        // they are checked below by constraint name instead, plus the full ordered source-to-target
        // column mapping, the referenced table/schema and the delete rule (both NO ACTION, since a
        // foreign key's ON DELETE action belongs to the constraint, not the row — Restrict is
        // enforced per-row via restrict_delete and the engine's own delete logic).
        var expectedForeignKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["entity_keys"] = $"FK_{_schemaName}_entity_keys_entities",
            ["search_values"] = $"FK_{_schemaName}_search_values_entities",
        };

        var expectedNamedForeignKeys = new (string Table, string ConstraintName, string DeleteRule, (string Source, string Target)[] Columns)[]
        {
            ("entity_links", $"FK_{_schemaName}_entity_links_left", "NO_ACTION",
                [("pool_id", "pool_id"), ("left_entity_type_id", "entity_type_id"), ("left_entity_id", "entity_id")]),
            ("entity_links", $"FK_{_schemaName}_entity_links_right", "NO_ACTION",
                [("pool_id", "pool_id"), ("right_entity_type_id", "entity_type_id"), ("right_entity_id", "entity_id")]),
        };

        var actualForeignKeys = new List<SchemaVerification.ForeignKeyColumn>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = """
                SELECT t.name, fk.name, target.name, CASE WHEN target.schema_id = t.schema_id THEN 1 ELSE 0 END,
                       source_column.name, target_column.name,
                       fk.delete_referential_action_desc, fkc.constraint_column_id
                FROM sys.foreign_keys fk
                INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.tables target ON target.object_id = fk.referenced_object_id
                INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                INNER JOIN sys.columns source_column ON source_column.object_id = fkc.parent_object_id AND source_column.column_id = fkc.parent_column_id
                INNER JOIN sys.columns target_column ON target_column.object_id = fkc.referenced_object_id AND target_column.column_id = fkc.referenced_column_id
                WHERE s.name = @schema AND fk.is_disabled = 0 AND fk.is_not_trusted = 0
                ORDER BY t.name, fk.name, fkc.constraint_column_id
                """;
            _ = cmd.Parameters.AddWithValue("@schema", _schemaName);
            Log.ExecutingSql(logger, cmd.CommandText);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                actualForeignKeys.Add(new SchemaVerification.ForeignKeyColumn(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetInt32(3) != 0,
                    reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt32(7)));
            }
        }

        SchemaVerification.VerifyForeignKeys(errors, actualColumns, actualForeignKeys, expectedForeignKeys);

        var entitiesTableName = SchemaVerification.FindName(actualColumns.Keys, "entities");
        foreach (var (table, constraintName, deleteRule, expectedFkColumns) in expectedNamedForeignKeys)
        {
            var group = actualForeignKeys
                .Where(fk => string.Equals(fk.Table, table, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(fk.Constraint, constraintName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(fk => fk.Position)
                .ToArray();

            var matches = group.Length == expectedFkColumns.Length &&
                group[0].IsSameSchema &&
                entitiesTableName is not null &&
                string.Equals(group[0].ReferencedTable, entitiesTableName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(group[0].DeleteRule, deleteRule, StringComparison.OrdinalIgnoreCase) &&
                expectedFkColumns.All(expected => group.Any(fk =>
                    string.Equals(fk.Column, expected.Source, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(fk.ReferencedColumn, expected.Target, StringComparison.OrdinalIgnoreCase)));

            if (!matches)
            {
                errors.Add(new SchemaVerificationError(
                    table, null,
                    $"Foreign key '{constraintName}' on '{_schemaName}.{table}' is missing, references the wrong table/schema or columns, or has the wrong delete action.",
                    SchemaVerificationErrorKind.MissingForeignKey));
            }
        }

        // Verify required user-defined table types (TVPs)
        var expectedTypes = new[]
        {
            "KeyTableType",
            "SearchValueTableType",
            "EntityIdTableType",
            "ExpiredEntityKeyTableType",
        };

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $"""
                SELECT t.name
                FROM sys.table_types t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'{_schemaName}'
                """;
            Log.ExecutingSql(logger, cmd.CommandText);

            var actualTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                _ = actualTypes.Add(reader.GetString(0));
            }

            foreach (var typeName in expectedTypes)
            {
                if (!actualTypes.Contains(typeName))
                {
                    errors.Add(new SchemaVerificationError(
                        _schemaName, null,
                        $"User-defined table type '{_schemaName}.{typeName}' is missing.",
                        SchemaVerificationErrorKind.MissingUserDefinedType));
                }
            }
        }

        return new SchemaVerificationResult(errors);
    }

    string IStorageInstanceSchema.BuildMigrationScript(DatabaseSchemaVersion fromVersion)
    {
        var scripts = MigrationScriptLoader.GetScripts(typeof(MsSqlStorageEngine).Assembly, fromVersion, _schemaName);
        return string.Join(Environment.NewLine + "GO" + Environment.NewLine, scripts.Select(s => s.Sql));
    }

    /// <summary>
    /// Returns whether the database error says that a Storage table or table type does not exist:
    /// SQL Server error 208 (Invalid object name) or 351 (Cannot find data type, for a table-valued parameter).
    /// </summary>
    private static bool IsMissingStorageTable(SqlException ex) => ex.Number is 208 or 351;

    /// <summary>
    /// Returns whether an exception is a SQL Server deadlock (error 1205), worth retrying the
    /// whole operation from a fresh connection and transaction. 1205 rolls back the whole
    /// transaction, so a statement-level retry isn't enough.
    /// </summary>
    private static bool IsDeadlock(Exception ex) => ex is SqlException { Number: 1205 };

    /// <summary>
    /// Qualified table names <see cref="SqlWhereClauseBuilder"/> needs to compile a link filter, matching the
    /// qualified names used elsewhere in this engine.
    /// </summary>
    private StorageTableNames LinkFilterTableNames => new(
        $"[{_schemaName}].[entities]",
        $"[{_schemaName}].[entity_links]");

    async Task<CreateResult> IPartitionedStorage.CreateAsync<TDso>(
        Storage.UuidV7 id,
        TDso value,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
    {
        try
        {
            var createOp = CreateOperation.For(id, value, keys, searchFieldCollection, expiration);

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);

            var outcome = await ExecuteCreateCoreAsync(connection, (SqlTransaction)transaction, createOp, ct);

            if (outcome == OperationOutcome.Success)
            {
                if (outboxEvents is { Count: > 0 })
                {
                    await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
                }
                await transaction.CommitAsync(ct);
            }

            return outcome switch
            {
                OperationOutcome.Success => CreateResult.Success,
                OperationOutcome.AlreadyExists => CreateResult.AlreadyExists,
                OperationOutcome.KeyConflict => CreateResult.KeyConflict,
                _ => throw new InvalidOperationException($"Unexpected outcome from create operation: {outcome}")
            };
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<StoreGetResult> IPartitionedStorage.TryReadAsync(
        EntityType entityType,
        Storage.UuidV7 id,
        Ct ct)
    {
        try
        {
            Log.ReadingDso(logger, entityType, id.Value);

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $"""
                SELECT value, dso_type_schema_version, value_version, created_at, last_updated_at
                FROM [{_schemaName}].[entities]
                WHERE entity_type_id = @entityTypeId AND entity_id = @entityId AND pool_id = @poolId
                """;

            _ = cmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
            _ = cmd.Parameters.AddWithValue("@entityId", SqlServerGuidConverter.ToSqlServer(id.Value));
            _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);

            Log.ExecutingSql(logger, cmd.CommandText);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return StoreGetResult.NotFound();
            }

            var jsonValue = reader.GetString(0);
            var dsoTypeVersion = reader.GetInt32(1);
            var valueVersion = reader.GetInt32(2);
            var created = reader.GetDateTimeOffset(3);
            var lastUpdated = reader.GetDateTimeOffset(4);

            var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
            var dsoType = dataStorageTypeRegistry.Get(version);
            var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

            return StoreGetResult.IsFound(item, id.Value, valueVersion, created, lastUpdated);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<StoreGetResult> IPartitionedStorage.TryReadAsync(
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
    {
        try
        {
            Log.ReadingDso(logger, entityType, key.Value);

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $"""
                SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at
                FROM [{_schemaName}].[entity_keys] i
                INNER JOIN [{_schemaName}].[entities] v
                    ON i.entity_type_id = v.entity_type_id
                    AND i.entity_id = v.entity_id
                    AND i.pool_id = v.pool_id
                WHERE i.entity_type_id = @entityTypeId
                  AND i.key_type_id = @keyTypeId
                  AND i.key_type_version = @keyTypeVersion
                  AND i.key_value = @keyValue
                  AND i.pool_id = @poolId
                """;

            _ = cmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
            _ = cmd.Parameters.AddWithValue("@keyTypeId", (int)key.DskVersion.KeyType.Id);
            _ = cmd.Parameters.AddWithValue("@keyTypeVersion", (int)key.DskVersion.SchemaVersion);
            _ = cmd.Parameters.AddWithValue("@keyValue", key.Value);
            _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);

            Log.ExecutingSql(logger, cmd.CommandText);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return StoreGetResult.NotFound();
            }

            var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
            var jsonValue = reader.GetString(1);
            var dsoTypeVersion = reader.GetInt32(2);
            var valueVersion = reader.GetInt32(3);
            var created = reader.GetDateTimeOffset(4);
            var lastUpdated = reader.GetDateTimeOffset(5);

            var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
            var dsoType = dataStorageTypeRegistry.Get(version);
            var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

            return StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<IReadOnlyList<StoreGetResult>> IPartitionedStorage.TryReadManyAsync(
        EntityType entityType,
        IReadOnlySet<Storage.UuidV7> ids,
        int maximum,
        Ct ct)
    {
        try
        {
            if (ids.Count > maximum)
            {
                throw new InvalidOperationException(
                    $"The number of requested IDs ({ids.Count}) exceeds the maximum allowed ({maximum}).");
            }

            Log.ReadingDsos(logger, entityType, ids.Count);

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $"""
                SELECT e.entity_id, e.value, e.dso_type_schema_version, e.value_version, e.created_at, e.last_updated_at
                FROM [{_schemaName}].[entities] e
                INNER JOIN @entityIds ids ON e.entity_id = ids.entity_id
                WHERE e.entity_type_id = @entityTypeId AND e.pool_id = @poolId
                """;

            _ = cmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
            _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);

            var idsTable = new DataTable();
            _ = idsTable.Columns.Add("entity_id", typeof(Guid));
            foreach (var id in ids)
            {
                _ = idsTable.Rows.Add(SqlServerGuidConverter.ToSqlServer(id.Value));
            }

            var idsParam = cmd.Parameters.AddWithValue("@entityIds", idsTable);
            idsParam.SqlDbType = SqlDbType.Structured;
            idsParam.TypeName = $"[{_schemaName}].[EntityIdTableType]";

            Log.ExecutingSql(logger, cmd.CommandText);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var results = new List<StoreGetResult>();
            while (await reader.ReadAsync(ct))
            {
                var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
                var jsonValue = reader.GetString(1);
                var dsoTypeVersion = reader.GetInt32(2);
                var valueVersion = reader.GetInt32(3);
                var created = reader.GetDateTimeOffset(4);
                var lastUpdated = reader.GetDateTimeOffset(5);

                var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
                var dsoType = dataStorageTypeRegistry.Get(version);
                var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                results.Add(StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated));
            }

            return results;
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<UpdateResult> IPartitionedStorage.UpdateAsync<TDso>(
        Storage.UuidV7 id,
        TDso dso,
        int expectedEntityVersion,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration? expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
    {
        try
        {
            var updateOp = UpdateOperation.For(id, dso, expectedEntityVersion, keys, searchFieldCollection, expiration);

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);

            var outcome = await ExecuteUpdateCoreAsync(connection, (SqlTransaction)transaction, updateOp, ct);

            if (outcome == OperationOutcome.Success)
            {
                if (outboxEvents is { Count: > 0 })
                {
                    await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
                }
                await transaction.CommitAsync(ct);
            }

            return outcome switch
            {
                OperationOutcome.Success => UpdateResult.Success,
                OperationOutcome.DoesNotExist => UpdateResult.DoesNotExist,
                OperationOutcome.UnexpectedVersion => UpdateResult.UnexpectedVersion,
                OperationOutcome.KeyConflict => UpdateResult.KeyConflict,
                _ => throw new InvalidOperationException($"Unexpected outcome from update operation: {outcome}")
            };
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<DeleteResult> IPartitionedStorage.DeleteAsync(EntityType entityType, Storage.UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            return await DeadlockRetry.RunAsync(
                async _ =>
                {
                    var deleteOp = DeleteOperation.ById(entityType, id);

                    await using var connection = OpenConnection();
                    await connection.OpenAsync(ct);
                    await using var transaction = await connection.BeginTransactionAsync(ct);

                    var (outcome, entityDeleted) = await ExecuteDeleteCoreAsync(connection, (SqlTransaction)transaction, deleteOp, ct);

                    if (outcome == OperationOutcome.StillLinked)
                    {
                        return DeleteResult.StillLinked;
                    }

                    if (entityDeleted && outboxEvents is { Count: > 0 })
                    {
                        await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
                    }
                    await transaction.CommitAsync(ct);

                    return DeleteResult.Success;
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<DeleteResult> IPartitionedStorage.DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            return await DeadlockRetry.RunAsync(
                async _ =>
                {
                    var deleteOp = DeleteOperation.ByKey(entityType, key);

                    await using var connection = OpenConnection();
                    await connection.OpenAsync(ct);
                    await using var transaction = await connection.BeginTransactionAsync(ct);

                    var (outcome, entityDeleted) = await ExecuteDeleteCoreAsync(connection, (SqlTransaction)transaction, deleteOp, ct);

                    if (outcome == OperationOutcome.StillLinked)
                    {
                        return DeleteResult.StillLinked;
                    }

                    if (entityDeleted && outboxEvents is { Count: > 0 })
                    {
                        await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
                    }
                    await transaction.CommitAsync(ct);

                    return DeleteResult.Success;
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    /// <inheritdoc/>
    async Task<LinkResult> IPartitionedStorage.LinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            return await DeadlockRetry.RunAsync(
                async _ =>
                {
                    await using var connection = OpenConnection();
                    await connection.OpenAsync(ct);
                    await using var transaction = await connection.BeginTransactionAsync(ct);
                    var outcome = await ExecuteLinkCoreAsync(connection, (SqlTransaction)transaction, LinkOperation.For(definition, leftEntityId, rightEntityId), ct);
                    if (outcome == OperationOutcome.Success)
                    {
                        if (outboxEvents is { Count: > 0 })
                        {
                            await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
                        }
                        await transaction.CommitAsync(ct);
                    }
                    return outcome switch
                    {
                        OperationOutcome.AlreadyLinked => LinkResult.AlreadyLinked,
                        OperationOutcome.DoesNotExist => LinkResult.DoesNotExist,
                        _ => LinkResult.Success
                    };
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    /// <inheritdoc/>
    async Task<UnlinkResult> IPartitionedStorage.UnlinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            return await DeadlockRetry.RunAsync(
                async attempt =>
                {
                    _ = attempt;
                    await using var connection = OpenConnection();
                    await connection.OpenAsync(ct);
                    await using var transaction = await connection.BeginTransactionAsync(ct);
                    _ = await ExecuteUnlinkCoreAsync(connection, (SqlTransaction)transaction, UnlinkOperation.For(definition, leftEntityId, rightEntityId), ct);
                    if (outboxEvents is { Count: > 0 })
                    {
                        await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
                    }
                    await transaction.CommitAsync(ct);
                    return UnlinkResult.Success;
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private async Task<OperationOutcome> ExecuteLinkCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        LinkOperation op,
        Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            INSERT INTO [{_schemaName}].[entity_links] (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id, restrict_delete)
            VALUES (@poolId, @linkTypeId, @leftEntityTypeId, @leftEntityId, @rightEntityTypeId, @rightEntityId, @restrictDelete)
            """;
        _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@linkTypeId", (int)op.Definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@leftEntityTypeId", (int)op.Definition.Left.Id);
        _ = cmd.Parameters.AddWithValue("@leftEntityId", SqlServerGuidConverter.ToSqlServer(op.LeftEntityId.Value));
        _ = cmd.Parameters.AddWithValue("@rightEntityTypeId", (int)op.Definition.Right.Id);
        _ = cmd.Parameters.AddWithValue("@rightEntityId", SqlServerGuidConverter.ToSqlServer(op.RightEntityId.Value));
        _ = cmd.Parameters.AddWithValue("@restrictDelete", op.Definition.OnRightDelete == LinkDeleteBehavior.Restrict);

        Log.ExecutingSql(logger, cmd.CommandText);

        try
        {
            _ = await cmd.ExecuteNonQueryAsync(ct);
            return OperationOutcome.Success;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            // PK or unique constraint violation — link already exists
            return OperationOutcome.AlreadyLinked;
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            // Foreign key violation — the left or right entity doesn't exist in this pool with
            // the definition's entity type.
            return OperationOutcome.DoesNotExist;
        }
    }
    private async Task<OperationOutcome> ExecuteUnlinkCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        UnlinkOperation op,
        Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            DELETE FROM [{_schemaName}].[entity_links]
            WHERE pool_id = @poolId
              AND link_type_id = @linkTypeId
              AND left_entity_id = @leftEntityId
              AND right_entity_id = @rightEntityId
            """;
        _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@linkTypeId", (int)op.Definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@leftEntityId", SqlServerGuidConverter.ToSqlServer(op.LeftEntityId.Value));
        _ = cmd.Parameters.AddWithValue("@rightEntityId", SqlServerGuidConverter.ToSqlServer(op.RightEntityId.Value));

        Log.ExecutingSql(logger, cmd.CommandText);

        _ = await cmd.ExecuteNonQueryAsync(ct);
        return OperationOutcome.Success;
    }

    private async Task ExecuteOutboxInsertBatchCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
    {
        var rows = new List<(OutboxEvent Evt, IOutboxSubscription Subscription)>();
        foreach (var evt in outboxEvents)
        {
            foreach (var subscription in outboxSubscriptions.GetMatchingSubscriptions(evt.EventName, evt.EntityTypeId))
            {
                rows.Add((evt, subscription));
            }
        }
        if (rows.Count == 0)
        {
            return;
        }

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;

        var valueRows = new List<string>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var (evt, subscription) = rows[i];
            valueRows.Add($"(@messageId{i}, @eventId{i}, @timestamp{i}, @eventName{i}, @subjectId{i}, @entityTypeId{i}, @entityTypeName{i}, @poolId, @payload{i}, @subscriptionName{i}, @dsoTypeSchemaVersion{i})");
            _ = cmd.Parameters.AddWithValue($"@messageId{i}", Guid.CreateVersion7());
            _ = cmd.Parameters.AddWithValue($"@eventId{i}", evt.Id.Value);
            _ = cmd.Parameters.AddWithValue($"@timestamp{i}", evt.Timestamp);
            _ = cmd.Parameters.AddWithValue($"@eventName{i}", evt.EventName.ToString());
            _ = cmd.Parameters.AddWithValue($"@subjectId{i}", SqlServerGuidConverter.ToSqlServer(evt.SubjectId.Value));
            _ = cmd.Parameters.AddWithValue($"@entityTypeId{i}", evt.EntityTypeId);
            _ = cmd.Parameters.AddWithValue($"@entityTypeName{i}", evt.EntityTypeName);
            _ = cmd.Parameters.AddWithValue($"@payload{i}", evt.Payload);
            _ = cmd.Parameters.AddWithValue($"@subscriptionName{i}", subscription.SubscriberName.ToString());
            _ = cmd.Parameters.AddWithValue($"@dsoTypeSchemaVersion{i}", evt.DsoTypeSchemaVersion.HasValue ? evt.DsoTypeSchemaVersion.Value : DBNull.Value);
        }
        _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);

        cmd.CommandText = $"""
            INSERT INTO [{_schemaName}].[outbox_subscriber_queue]
            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version)
            VALUES
            {string.Join(",\n            ", valueRows)}
            """;

        Log.ExecutingSql(logger, cmd.CommandText);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    async Task<BatchResult> IPartitionedStorage.ExecuteBatchAsync(
        IReadOnlyList<IStorageOperation> operations,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
    {
        try
        {
            if (operations.Count == 0)
            {
                return new BatchResult(true, []);
            }

            return await DeadlockRetry.RunAsync(
                async _ =>
                {
                    await using var connection = OpenConnection();
                    await connection.OpenAsync(ct);
                    await using var transaction = await connection.BeginTransactionAsync(ct);

                    var results = new List<OperationResult>();

                    for (var i = 0; i < operations.Count; i++)
                    {
                        var outcome = operations[i] switch
                        {
                            CreateOperation create => await ExecuteCreateCoreAsync(connection, (SqlTransaction)transaction, create, ct),
                            UpdateOperation update => await ExecuteUpdateCoreAsync(connection, (SqlTransaction)transaction, update, ct),
                            DeleteOperation delete => (await ExecuteDeleteCoreAsync(connection, (SqlTransaction)transaction, delete, ct)).Outcome,
                            LinkOperation link => await ExecuteLinkCoreAsync(connection, (SqlTransaction)transaction, link, ct),
                            UnlinkOperation unlink => await ExecuteUnlinkCoreAsync(connection, (SqlTransaction)transaction, unlink, ct),
                            _ => throw new InvalidOperationException($"Unknown operation type: {operations[i].GetType().Name}")
                        };

                        results.Add(new OperationResult(i, outcome));

                        if (outcome is not OperationOutcome.Success and not OperationOutcome.AlreadyLinked)
                        {
                            // Fail-fast: stop processing on first failure
                            // Transaction is rolled back automatically on dispose
                            return new BatchResult(false, results);
                        }
                    }

                    // All operations succeeded — INSERT outbox events before committing
                    if (outboxEvents is { Count: > 0 })
                    {
                        await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
                    }

                    await transaction.CommitAsync(ct);
                    return new BatchResult(true, results);
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<OutboxEventsPage> ICrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, Ct ct)
    {
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $"""
                SELECT TOP (@count) sequence_number, message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version
                FROM [{_schemaName}].[outbox_subscriber_queue]
                WHERE subscriber_name = @subscriber_name
                ORDER BY sequence_number ASC
                """;

            _ = cmd.Parameters.AddWithValue("@count", count + 1);
            _ = cmd.Parameters.AddWithValue("@subscriber_name", subscriptionName.ToString());

            Log.ExecutingSql(logger, cmd.CommandText);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var events = new List<PersistedOutboxEvent>();
            while (await reader.ReadAsync(ct))
            {
                var dsoTypeSchemaVersion = await reader.IsDBNullAsync(11, ct) ? null : (int?)reader.GetInt32(11);
                var entityTypeId = reader.GetInt32(6);
                var entityTypeName = reader.GetString(7);
                var payload = reader.GetString(9);

                IDataStorageObject? dso = null;
                if (dsoTypeSchemaVersion.HasValue)
                {
                    var entityType = new EntityType((uint)entityTypeId, entityTypeName);
                    var dsoVersion = new DataStorageObjectVersion(entityType, (uint)dsoTypeSchemaVersion.Value);
                    if (dataStorageTypeRegistry.TryGet(dsoVersion, out var dsoType))
                    {
                        try
                        {
                            dso = (IDataStorageObject?)JsonSerializer.Deserialize(payload, dsoType);
                        }
                        catch (JsonException)
                        {
                            // Malformed payload — leave Dso null so the handler can
                            // fall back to raw Payload or drop the event.
                        }
                    }
                }

                events.Add(new PersistedOutboxEvent
                {
                    SequenceNumber = reader.GetInt64(0),
                    MessageId = reader.GetGuid(1),
                    EventId = reader.GetGuid(2),
                    Timestamp = reader.GetDateTimeOffset(3),
                    EventName = OutboxEventName.Create(reader.GetString(4)),
                    SubjectId = Storage.UuidV7.From(SqlServerGuidConverter.ToUuidV7(reader.GetGuid(5))),
                    EntityTypeId = entityTypeId,
                    EntityTypeName = entityTypeName,
                    PoolId = PoolId.Load(reader.GetInt32(8)),
                    Payload = payload,
                    SubscriberName = SubscriberName.Create(reader.GetString(10)),
                    Dso = dso,
                });
            }

            var hasMore = events.Count > count;
            if (hasMore)
            {
                events.RemoveAt(events.Count - 1);
            }

            return new OutboxEventsPage(events, hasMore);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task ICrossPartitionStorage.DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct)
    {
        try
        {
            if (ids.Count == 0)
            {
                return;
            }

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            const int MaxBatchSize = 1000;
            for (var offset = 0; offset < ids.Count; offset += MaxBatchSize)
            {
                var chunk = ids.Skip(offset).Take(MaxBatchSize).ToList();

                await using var cmd = connection.CreateCommand();
                cmd.CommandType = CommandType.Text;

                var paramNames = chunk.Select((_, i) => $"@id{i}").ToList();
                cmd.CommandText = $"""
                    DELETE FROM [{_schemaName}].[outbox_subscriber_queue]
                    WHERE message_id IN ({string.Join(", ", paramNames)})
                    """;

                for (var i = 0; i < chunk.Count; i++)
                {
                    _ = cmd.Parameters.AddWithValue(paramNames[i], chunk[i].Value);
                }

                Log.ExecutingSql(logger, cmd.CommandText);
                _ = await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<QueryResult<MetadataEnvelope>> IPartitionedStorage.QueryAsync(
        EntityType entityType,
        IQueryExpression filter,
        SortParameter sort,
        DataRange dataRange,
        Ct ct)
    {
        try
        {
            if (dataRange.TokenValue is not null)
            {
                return await QueryCursorAsync(entityType, filter, sort, dataRange.TokenValue, ct);
            }

            var (skip, take) = ResolveOffsetAndSize(dataRange);
            var entityTypeId = (int)entityType.Id;

            Log.QueryingDsos(logger, entityType, skip, take);

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            // Build WHERE clause and ORDER BY clause
            var queryClauses = BuildQueryClauses(cmd, filter, sort, skip, entityType);

            // Build main query using CTEs so total_count is correct even when page is beyond range.
            // all_matches: all qualifying rows (includes sort_value when sorting); total: count of all matches; paged: the requested page.
            // LEFT JOIN ensures total always returns one row even when paged is empty.
            string allMatchesSelect;
            string pagedOrderBy;
            string outerOrderBy;
            if (!sort.IsEmpty)
            {
                var sortColumn = GetSortColumnName(sort.Field!);
                var sortDirection = sort.Direction == SortDirection.Ascending ? "ASC" : "DESC";
                allMatchesSelect = $"SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at, {sortColumn} AS sort_value";
                // MsSql doesn't support NULLS LAST — use CASE to push NULLs to end
                pagedOrderBy = $"ORDER BY CASE WHEN sort_value IS NULL THEN 1 ELSE 0 END, sort_value {sortDirection}, entity_id ASC";
                outerOrderBy = $"ORDER BY CASE WHEN p.sort_value IS NULL THEN 1 ELSE 0 END, p.sort_value {sortDirection}, p.entity_id ASC";
            }
            else
            {
                allMatchesSelect = "SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at";
                pagedOrderBy = "ORDER BY entity_id ASC";
                outerOrderBy = "ORDER BY p.entity_id ASC";
            }

            var query = $"""
                WITH all_matches AS (
                    {allMatchesSelect}
                    FROM [{_schemaName}].[entities] v
                    {queryClauses.JoinClause}
                    WHERE v.entity_type_id = @entity_type_id
                      AND v.pool_id = @pool_id
                      AND ({queryClauses.WhereClause})
                ),
                total AS (
                    SELECT COUNT(*) AS total_count FROM all_matches
                ),
                paged AS (
                    SELECT * FROM all_matches
                    {pagedOrderBy}
                    OFFSET @offset ROWS
                    FETCH NEXT @limit ROWS ONLY
                )
                SELECT p.entity_id, p.value, p.dso_type_schema_version, p.value_version, p.created_at, p.last_updated_at, t.total_count
                FROM total t
                LEFT JOIN paged p ON 1=1
                {outerOrderBy}
                """;

            _ = cmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@offset", queryClauses.Offset);
            _ = cmd.Parameters.AddWithValue("@limit", take);

            cmd.CommandText = query;

            Log.ExecutingQuery(logger, query);

            // Execute query and deserialize results.
            // total_count is at column 6 (int in MsSql → reader.GetInt32).
            // When page is beyond range, LEFT JOIN yields one row with p.* = NULL — skip those.
            var items = new List<MetadataEnvelope>();
            var totalCount = 0;
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    if (totalCount == 0)
                    {
                        totalCount = reader.GetInt32(6);
                    }

                    // When page is beyond range, p.entity_id is NULL — skip deserializing
                    if (await reader.IsDBNullAsync(0, ct))
                    {
                        continue;
                    }

                    var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
                    var jsonValue = reader.GetString(1);
                    var dsoTypeVersion = reader.GetInt32(2);
                    var valueVersion = reader.GetInt32(3);
                    var created = reader.GetDateTimeOffset(4);
                    var lastUpdated = reader.GetDateTimeOffset(5);

                    var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
                    var dsoType = dataStorageTypeRegistry.Get(version);
                    var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                    items.Add(new MetadataEnvelope(item, entityId, valueVersion, created, lastUpdated));
                }
            }

            return new QueryResult<MetadataEnvelope>
            {
                Items = items,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / take),
                HasMoreData = skip + take < totalCount,
                HasPreviousData = skip > 0 && totalCount > 0
            };
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private async Task<QueryResult<MetadataEnvelope>> QueryCursorAsync(
        EntityType entityType,
        IQueryExpression filter,
        SortParameter sort,
        ContinuationTokenDataRange tokenRange,
        Ct ct)
    {
        var pageSize = tokenRange.Size.Value;
        var decoded = CursorToken.Decode(tokenRange.Start, sort, entityType);
        var traversal = decoded.Direction;
        var position = decoded.Position;

        var entityTypeId = (int)entityType.Id;

        Log.QueryingDsos(logger, entityType, 0, pageSize);

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        // Build WHERE clause and ORDER BY clause for cursor-based pagination
        var queryClauses = BuildCursorQueryClauses(cmd, filter, sort, position, traversal, entityType);

        // Build main query - fetch PageSize + 1 to determine if there are more pages
        // We select the sort value to use in the next token
        var query = $"""
            SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at, {queryClauses.SortColumnName}
            FROM [{_schemaName}].[entities] v
            {queryClauses.JoinClause}
            WHERE v.entity_type_id = @entity_type_id
              AND v.pool_id = @pool_id
              AND ({queryClauses.WhereClause})
              {queryClauses.SeekClause}
            {queryClauses.OrderByClause}
            OFFSET 0 ROWS
            FETCH NEXT @limit ROWS ONLY
            """;

        _ = cmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@limit", pageSize + 1);

        cmd.CommandText = query;

        Log.ExecutingQuery(logger, query);

        // Execute query and deserialize results
        var items = new List<(Guid Id, MetadataEnvelope Item, object? SortValue)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
                var jsonValue = reader.GetString(1);
                var dsoTypeVersion = reader.GetInt32(2);
                var valueVersion = reader.GetInt32(3);
                var created = reader.GetDateTimeOffset(4);
                var lastUpdated = reader.GetDateTimeOffset(5);

                var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
                var dsoType = dataStorageTypeRegistry.Get(version);
                var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                // Get the sort value from the last column (index 6)
                var sortValue = sort.IsEmpty ? null : await ReadSortValueAsync(reader, sort.Field!, 6, ct);
                items.Add((entityId, new MetadataEnvelope(item, entityId, valueVersion, created, lastUpdated), sortValue));
            }
        }

        var assembled = CursorPageAssembler.Assemble(
            items,
            pageSize,
            traversal,
            position,
            entityType,
            sort,
            row => (row.Id, row.SortValue));

        var resultItems = assembled.ItemsInRequestedOrder
            .Select(x => x.Item)
            .ToList();

        return new QueryResult<MetadataEnvelope>
        {
            Items = resultItems,
            NextToken = assembled.Next,
            PreviousToken = assembled.Previous,
            HasMoreData = assembled.HasMoreData,
            HasPreviousData = assembled.HasPreviousData
        };
    }

    async Task<QueryResult<ProjectedResult>> IPartitionedStorage.QueryFieldsAsync(
        EntityType entityType,
        IReadOnlyCollection<Field> fields,
        IQueryExpression filter,
        SortParameter sort,
        DataRange dataRange,
        Ct ct)
    {
        try
        {
            if (dataRange.TokenValue is not null)
            {
                return await QueryFieldsCursorAsync(entityType, fields, filter, sort, dataRange.TokenValue, ct);
            }

            var (skip, take) = ResolveOffsetAndSize(dataRange);
            var entityTypeId = (int)entityType.Id;

            Log.QueryingFieldsDsos(logger, entityType, fields.Count, skip, take);

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            // Build WHERE clause and ORDER BY clause
            var queryClauses = BuildQueryClauses(cmd, filter, sort, skip, entityType);

            // Use a CTE to get filtered IDs, then join to get field values
            // Use "select_field_" prefix to avoid collision with WHERE clause parameters that use "field_path_"
            var fieldPaths = fields.Select(f => f.Path).ToList();
            var fieldConditions = new List<string>();
            var paramIndex = 0;
            for (var i = 0; i < fieldPaths.Count; i++)
            {
                if (SystemFields.IsSystemField(fieldPaths[i]))
                {
                    continue;
                }

                _ = cmd.Parameters.AddWithValue($"@select_field_{paramIndex}", DeterministicGuidGenerator.Create(fieldPaths[i].ToUpperInvariant()));
                fieldConditions.Add($"field_sv.field_path = @select_field_{paramIndex}");
                paramIndex++;
            }
            var fieldConditionsClause = fieldConditions.Count > 0
                ? string.Join(" OR ", fieldConditions)
                : "1=0";

            // When we have sorting, we include the sort column in the CTE to enable proper ordering.
            // We use ROW_NUMBER to preserve the sort order in the final results after joining with field values.
            string cteSelect;
            string cteJoin;

            if (!sort.IsEmpty)
            {
                // Determine which column to select based on field type
                var sortColumn = GetSortColumnName(sort.Field!);

                // Include sort column and row number to preserve sort order
                cteSelect = $"SELECT v.entity_id, v.created_at, v.last_updated_at, v.value_version, {sortColumn} AS sort_value, ROW_NUMBER() OVER ({queryClauses.OrderByClause}) AS row_num";
                cteJoin = queryClauses.JoinClause;
            }
            else
            {
                cteSelect = "SELECT v.entity_id, v.created_at, v.last_updated_at, v.value_version, ROW_NUMBER() OVER (ORDER BY v.entity_id ASC) AS row_num";
                cteJoin = "";
            }

            var query = $"""
                WITH all_matches AS (
                    {cteSelect}
                    FROM [{_schemaName}].[entities] v
                    {cteJoin}
                    WHERE v.entity_type_id = @entity_type_id
                      AND v.pool_id = @pool_id
                      AND ({queryClauses.WhereClause})
                ),
                total AS (
                    SELECT COUNT(*) AS total_count FROM all_matches
                ),
                filtered_ids AS (
                    SELECT * FROM all_matches
                    ORDER BY row_num ASC
                    OFFSET @offset ROWS
                    FETCH NEXT @limit ROWS ONLY
                )
                SELECT
                    fi.entity_id,
                    field_sv.field_path_text,
                    field_sv.string_value,
                    field_sv.number_value,
                    field_sv.datetime_value,
                    field_sv.boolean_value,
                    field_sv.guid_value,
                    t.total_count,
                    fi.created_at,
                    fi.last_updated_at,
                    fi.value_version
                FROM total t
                LEFT JOIN filtered_ids fi ON 1=1
                LEFT JOIN [{_schemaName}].[search_values] field_sv
                  ON fi.entity_id = field_sv.entity_id
                  AND field_sv.entity_type_id = @entity_type_id
                  AND field_sv.pool_id = @pool_id
                  AND field_sv.item_index = -1
                  AND ({fieldConditionsClause})
                ORDER BY fi.row_num, fi.entity_id
                """;

            _ = cmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@offset", queryClauses.Offset);
            _ = cmd.Parameters.AddWithValue("@limit", take);

            cmd.CommandText = query;

            Log.ExecutingQuery(logger, query);

            // Execute query and build projected results.
            // total_count is at column 7: entity_id(0), field_path(1), string_value(2),
            // number_value(3), datetime_value(4), boolean_value(5), guid_value(6), total_count(7),
            // created_at(8), last_updated_at(9).
            // When page is beyond range, LEFT JOIN yields one row with fi.* = NULL — skip those.
            var resultsByid = new Dictionary<Guid, (Dictionary<string, object?> FieldValues, DateTimeOffset Created, DateTimeOffset LastUpdated, int Version)>();
            var orderedIds = new List<Guid>();
            var totalCount = 0;
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    if (totalCount == 0)
                    {
                        totalCount = reader.GetInt32(7);
                    }

                    // When page is beyond range, fi.entity_id is NULL — skip
                    if (await reader.IsDBNullAsync(0, ct))
                    {
                        continue;
                    }

                    var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
                    var fieldPath = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);

                    if (!resultsByid.TryGetValue(entityId, out var entry))
                    {
                        var fieldValues = new Dictionary<string, object?>();
                        orderedIds.Add(entityId);

                        // Initialize all requested fields as null
                        foreach (var field in fields)
                        {
                            fieldValues[field.Path] = null;
                        }

                        var created = reader.GetDateTimeOffset(8);
                        var lastUpdated = reader.GetDateTimeOffset(9);
                        var version = reader.GetInt32(10);

                        // Populate system fields from entity columns
                        foreach (var field in fields)
                        {
                            if (string.Equals(field.Path, SystemFields.Created, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(field.Path, SystemFields.CreatedAttributeName, StringComparison.OrdinalIgnoreCase))
                            {
                                fieldValues[field.Path] = created;
                            }
                            else if (string.Equals(field.Path, SystemFields.LastUpdated, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(field.Path, SystemFields.LastUpdatedAttributeName, StringComparison.OrdinalIgnoreCase))
                            {
                                fieldValues[field.Path] = lastUpdated;
                            }
                        }

                        entry = (fieldValues, created, lastUpdated, version);
                        resultsByid[entityId] = entry;
                    }

                    if (fieldPath != null && entry.FieldValues.ContainsKey(fieldPath))
                    {
                        // Find the field definition to determine which typed column to read from
                        var field = fields.First(f => f.Path == fieldPath);

                        // Extract the value from the correct typed column based on field type
                        var value = await ReadFieldValueAsync(reader, field.Type, 2, ct);
                        entry.FieldValues[fieldPath] = value;
                    }
                }
            }

            var items = orderedIds
                .Select(id => new ProjectedResult(id, resultsByid[id].FieldValues))
                .ToList();

            return new QueryResult<ProjectedResult>
            {
                Items = items,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / take),
                HasMoreData = skip + take < totalCount,
                HasPreviousData = skip > 0 && totalCount > 0
            };
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private async Task<QueryResult<ProjectedResult>> QueryFieldsCursorAsync(
        EntityType entityType,
        IReadOnlyCollection<Field> fields,
        IQueryExpression filter,
        SortParameter sort,
        ContinuationTokenDataRange tokenRange,
        Ct ct)
    {
        var pageSize = tokenRange.Size.Value;
        var decoded = CursorToken.Decode(tokenRange.Start, sort, entityType);
        var traversal = decoded.Direction;
        var position = decoded.Position;

        var entityTypeId = (int)entityType.Id;

        Log.QueryingFieldsDsos(logger, entityType, fields.Count, 0, pageSize);

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        // Build WHERE clause and ORDER BY clause for cursor-based pagination
        var queryClauses = BuildCursorQueryClauses(cmd, filter, sort, position, traversal, entityType);

        // Use a CTE to get filtered IDs with cursor paging, then join to get field values
        // Use "select_field_" prefix to avoid collision with WHERE clause parameters that use "field_path_"
        var fieldPaths = fields.Select(f => f.Path).ToList();
        var fieldConditions = new List<string>();
        var paramIndex = 0;
        for (var i = 0; i < fieldPaths.Count; i++)
        {
            if (SystemFields.IsSystemField(fieldPaths[i]))
            {
                continue;
            }

            _ = cmd.Parameters.AddWithValue($"@select_field_{paramIndex}", DeterministicGuidGenerator.Create(fieldPaths[i].ToUpperInvariant()));
            fieldConditions.Add($"field_sv.field_path = @select_field_{paramIndex}");
            paramIndex++;
        }
        var fieldConditionsClause = fieldConditions.Count > 0
            ? string.Join(" OR ", fieldConditions)
            : "1=0";

        var query = $"""
            WITH filtered_ids AS (
                SELECT v.entity_id, v.created_at, v.last_updated_at, v.value_version, {queryClauses.SortColumnName} AS sort_value, ROW_NUMBER() OVER ({queryClauses.OrderByClause}) AS row_num
                FROM [{_schemaName}].[entities] v
                {queryClauses.JoinClause}
                WHERE v.entity_type_id = @entity_type_id
                  AND v.pool_id = @pool_id
                  AND ({queryClauses.WhereClause})
                  {queryClauses.SeekClause}
                {queryClauses.OrderByClause}
                OFFSET 0 ROWS
                FETCH NEXT @limit ROWS ONLY
            )
            SELECT
                fi.entity_id,
                field_sv.field_path_text,
                field_sv.string_value,
                field_sv.number_value,
                field_sv.datetime_value,
                field_sv.boolean_value,
                field_sv.guid_value,
                fi.sort_value,
                fi.created_at,
                fi.last_updated_at,
                fi.value_version
            FROM filtered_ids fi
            LEFT JOIN [{_schemaName}].[search_values] field_sv
              ON fi.entity_id = field_sv.entity_id
              AND field_sv.entity_type_id = @entity_type_id
              AND field_sv.pool_id = @pool_id
              AND field_sv.item_index = -1
              AND ({fieldConditionsClause})
            ORDER BY fi.row_num, fi.entity_id
            """;

        _ = cmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@limit", pageSize + 1);

        cmd.CommandText = query;

        Log.ExecutingQuery(logger, query);

        // Execute query and build projected results
        var resultsByid = new Dictionary<Guid, (Dictionary<string, object?> FieldValues, DateTimeOffset Created, DateTimeOffset LastUpdated, object? SortValue, int Version)>();
        var orderedIds = new List<Guid>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
                var fieldPath = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);

                if (!resultsByid.TryGetValue(entityId, out var entry))
                {
                    var fieldValues = new Dictionary<string, object?>();

                    // Initialize all requested fields as null
                    foreach (var field in fields)
                    {
                        fieldValues[field.Path] = null;
                    }

                    // Get sort value from column 7, created_at from 8, last_updated_at from 9, value_version from 10
                    var sortValue = sort.IsEmpty ? null : await ReadSortValueAsync(reader, sort.Field!, 7, ct);
                    var created = reader.GetDateTimeOffset(8);
                    var lastUpdated = reader.GetDateTimeOffset(9);
                    var version = reader.GetInt32(10);

                    // Populate system fields from entity columns
                    foreach (var field in fields)
                    {
                        if (string.Equals(field.Path, SystemFields.Created, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(field.Path, SystemFields.CreatedAttributeName, StringComparison.OrdinalIgnoreCase))
                        {
                            fieldValues[field.Path] = created;
                        }
                        else if (string.Equals(field.Path, SystemFields.LastUpdated, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(field.Path, SystemFields.LastUpdatedAttributeName, StringComparison.OrdinalIgnoreCase))
                        {
                            fieldValues[field.Path] = lastUpdated;
                        }
                    }

                    entry = (fieldValues, created, lastUpdated, sortValue, version);
                    resultsByid[entityId] = entry;
                    orderedIds.Add(entityId);
                }

                if (fieldPath != null && entry.FieldValues.ContainsKey(fieldPath))
                {
                    // Find the field definition to determine which typed column to read from
                    var field = fields.First(f => f.Path == fieldPath);

                    // Extract the value from the correct typed column based on field type
                    var value = await ReadFieldValueAsync(reader, field.Type, 2, ct);
                    entry.FieldValues[fieldPath] = value;
                }
            }
        }

        var itemsList = orderedIds.Select(id => (Id: id, Row: resultsByid[id])).ToList();

        var assembled = CursorPageAssembler.Assemble(
            itemsList,
            pageSize,
            traversal,
            position,
            entityType,
            sort,
            row => (row.Id, row.Row.SortValue));

        var items = assembled.ItemsInRequestedOrder
            .Select(item => new ProjectedResult(item.Id, item.Row.FieldValues))
            .ToList();

        return new QueryResult<ProjectedResult>
        {
            Items = items,
            NextToken = assembled.Next,
            PreviousToken = assembled.Previous,
            HasMoreData = assembled.HasMoreData,
            HasPreviousData = assembled.HasPreviousData
        };
    }

    private async Task<OperationOutcome> ExecuteCreateCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CreateOperation op,
        Ct ct)
    {
        var dsoVersion = op.DsoVersion;
        var entityType = dsoVersion.EntityType;
        var jsonDso = JsonSerializer.Serialize(op.Value);

        // Resolve expiration
        var expiresAt = op.Expiration.Resolve(timeProvider);
        if (expiresAt.HasValue && expiresAt.Value <= timeProvider.GetUtcNow())
        {
            // Already expired — noop, return success without storing
            return OperationOutcome.Success;
        }

        Log.CreatingDso(logger, entityType, op.Id.Value, dsoVersion.SchemaVersion);

        try
        {
            // Insert the main entities record
            await using (var valuesCmd = connection.CreateCommand())
            {
                valuesCmd.Transaction = transaction;
                valuesCmd.CommandType = CommandType.Text;
                valuesCmd.CommandText = $"""
                    INSERT INTO [{_schemaName}].[entities] (
                        entity_type_id,
                        entity_type_name,
                        entity_id,
                        original_entity_id,
                        value,
                        dso_type_schema_version,
                        value_version,
                        created_at,
                        last_updated_at,
                        pool_id,
                        expires_at
                    )
                    VALUES (
                        @entityTypeId,
                        @entityTypeName,
                        @entityId,
                        @originalEntityId,
                        @value,
                        @dsoTypeSchemaVersion,
                        1,
                        @now,
                        @now,
                        @poolId,
                        @expiresAt
                    )
                    """;

                _ = valuesCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
                _ = valuesCmd.Parameters.AddWithValue("@entityTypeName", entityType.Name);
                _ = valuesCmd.Parameters.AddWithValue("@entityId", SqlServerGuidConverter.ToSqlServer(op.Id.Value));
                _ = valuesCmd.Parameters.AddWithValue("@originalEntityId", op.Id.Value);
                _ = valuesCmd.Parameters.AddWithValue("@value", jsonDso);
                _ = valuesCmd.Parameters.AddWithValue("@dsoTypeSchemaVersion", (int)dsoVersion.SchemaVersion);
                _ = valuesCmd.Parameters.AddWithValue("@now", timeProvider.GetUtcNow());
                _ = valuesCmd.Parameters.AddWithValue("@poolId", PoolId.Value);
                // expiresAt is null when Expiration.Never — DBNull.Value is needed for SQL NULL
#pragma warning disable CA1508 // Avoid dead conditional code — false positive: Expiration.Never.Resolve() returns null
                _ = valuesCmd.Parameters.AddWithValue("@expiresAt", (object?)expiresAt ?? DBNull.Value);
#pragma warning restore CA1508

                Log.ExecutingSql(logger, valuesCmd.CommandText);
                _ = await valuesCmd.ExecuteNonQueryAsync(ct);
            }

            // Bulk insert keys using TVP
            if (op.Keys.Count > 0)
            {
                await using var keysCmd = connection.CreateCommand();
                keysCmd.Transaction = transaction;
                keysCmd.CommandType = CommandType.Text;
                keysCmd.CommandText = $"""
                    INSERT INTO [{_schemaName}].[entity_keys] (
                        entity_type_id,
                        key_type_id,
                        key_type_name,
                        key_type_version,
                        key_value,
                        key_json,
                        entity_id,
                        pool_id
                    )
                    SELECT
                        @entityTypeId,
                        key_type_id,
                        key_type_name,
                        key_type_version,
                        key_value,
                        key_json,
                        @entityId,
                        @poolId
                    FROM @keys
                    """;

                _ = keysCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
                _ = keysCmd.Parameters.AddWithValue("@entityId", SqlServerGuidConverter.ToSqlServer(op.Id.Value));
                _ = keysCmd.Parameters.AddWithValue("@poolId", PoolId.Value);

                var keysTable = new DataTable();
                _ = keysTable.Columns.Add("key_type_id", typeof(int));
                _ = keysTable.Columns.Add("key_type_name", typeof(string));
                _ = keysTable.Columns.Add("key_type_version", typeof(int));
                _ = keysTable.Columns.Add("key_value", typeof(Guid));
                _ = keysTable.Columns.Add("key_json", typeof(string));

                foreach (var key in op.Keys)
                {
                    _ = keysTable.Rows.Add(
                        (int)key.DskVersion.KeyType.Id,
                        key.DskVersion.KeyType.Name,
                        (int)key.DskVersion.SchemaVersion,
                        key.Value,
                        (object?)key.KeyJsonValue ?? DBNull.Value
                    );
                }

                var keysParam = keysCmd.Parameters.AddWithValue("@keys", keysTable);
                keysParam.SqlDbType = SqlDbType.Structured;
                keysParam.TypeName = $"[{_schemaName}].[KeyTableType]";

                Log.ExecutingSql(logger, keysCmd.CommandText);
                _ = await keysCmd.ExecuteNonQueryAsync(ct);
            }

            // Bulk insert search fields using TVP
            if (op.SearchFieldCollection.Count > 0)
            {
                await using var searchCmd = connection.CreateCommand();
                searchCmd.Transaction = transaction;
                searchCmd.CommandType = CommandType.Text;
                searchCmd.CommandText = $"""
                    INSERT INTO [{_schemaName}].[search_values] (
                        entity_type_id,
                        entity_id,
                        field_path,
                        field_path_text,
                        item_index,
                        string_value,
                        number_value,
                        datetime_value,
                        boolean_value,
                        guid_value,
                        pool_id
                    )
                    SELECT
                        @entityTypeId,
                        @entityId,
                        field_path,
                        field_path_text,
                        item_index,
                        string_value,
                        number_value,
                        datetime_value,
                        boolean_value,
                        guid_value,
                        @poolId
                    FROM @searchValues
                    """;

                _ = searchCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
                _ = searchCmd.Parameters.AddWithValue("@entityId", SqlServerGuidConverter.ToSqlServer(op.Id.Value));
                _ = searchCmd.Parameters.AddWithValue("@poolId", PoolId.Value);

                var searchTable = new DataTable();
                _ = searchTable.Columns.Add("field_path", typeof(Guid));
                _ = searchTable.Columns.Add("field_path_text", typeof(string));
                _ = searchTable.Columns.Add("item_index", typeof(int));
                _ = searchTable.Columns.Add("string_value", typeof(string));
                _ = searchTable.Columns.Add("number_value", typeof(decimal));
                _ = searchTable.Columns.Add("datetime_value", typeof(DateTimeOffset));
                _ = searchTable.Columns.Add("boolean_value", typeof(bool));
                _ = searchTable.Columns.Add("guid_value", typeof(Guid));

                foreach (var field in op.SearchFieldCollection)
                {
                    _ = searchTable.Rows.Add(
                        field.FieldPathId,
                        field.FieldPath,
                        field.ItemIndex ?? -1,
                        (object?)field.StringValue ?? DBNull.Value,
                        field.NumberValue.HasValue ? field.NumberValue.Value : DBNull.Value,
                        field.DateTimeValue.HasValue ? field.DateTimeValue.Value : DBNull.Value,
                        field.BooleanValue.HasValue ? field.BooleanValue.Value : DBNull.Value,
                        field.GuidValue.HasValue ? field.GuidValue.Value : DBNull.Value
                    );
                }

                var searchParam = searchCmd.Parameters.AddWithValue("@searchValues", searchTable);
                searchParam.SqlDbType = SqlDbType.Structured;
                searchParam.TypeName = $"[{_schemaName}].[SearchValueTableType]";

                Log.ExecutingSql(logger, searchCmd.CommandText);
                _ = await searchCmd.ExecuteNonQueryAsync(ct);
            }

            return OperationOutcome.Success;
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            // Unique constraint violation
            // Check if it's the main entities table (ID already exists)
            if (ex.Message.Contains($"PK_{_schemaName}_entities", StringComparison.OrdinalIgnoreCase))
            {
                return OperationOutcome.AlreadyExists;
            }

            // Otherwise, it's a key conflict in the entity_keys table
            if (ex.Message.Contains($"PK_{_schemaName}_entity_keys", StringComparison.OrdinalIgnoreCase))
            {
                return OperationOutcome.KeyConflict;
            }

            throw;
        }
    }

    private async Task<OperationOutcome> ExecuteUpdateCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        UpdateOperation op,
        Ct ct)
    {
        var dsoVersion = op.DsoVersion;
        var entityType = dsoVersion.EntityType;
        var jsonDso = JsonSerializer.Serialize(op.Value);

        // Resolve expiration
        DateTimeOffset? expiresAt = null;
        var hasExpirationChange = op.Expiration is not null;
        if (hasExpirationChange)
        {
            expiresAt = op.Expiration!.Resolve(timeProvider);
        }

        Log.UpdatingDso(logger, entityType, op.Id.Value, dsoVersion.SchemaVersion, op.ExpectedEntityVersion);

        try
        {
            // Combine version check, deletes, and update into a single batch
            await using (var batchCmd = connection.CreateCommand())
            {
                batchCmd.Transaction = transaction;
                batchCmd.CommandType = CommandType.Text;

                var expiresAtSql = hasExpirationChange
                    ? "expires_at = @expiresAt,"
                    : ""; // Don't change existing expires_at when expiration is null

                batchCmd.CommandText = $"""
                    -- Read current version with row lock and verify
                    DECLARE @actualVersion INT;
                    SELECT @actualVersion = value_version
                    FROM [{_schemaName}].[entities] WITH (UPDLOCK, ROWLOCK)
                    WHERE entity_type_id = @entityTypeId AND entity_id = @entityId AND pool_id = @poolId;

                    IF @actualVersion IS NULL
                    BEGIN
                        SELECT -1 AS Result; -- DoesNotExist
                        RETURN;
                    END

                    IF @actualVersion != @expectedVersion
                    BEGIN
                        SELECT -2 AS Result; -- UnexpectedVersion
                        RETURN;
                    END

                    -- Delete existing keys and search fields
                    DELETE FROM [{_schemaName}].[entity_keys]
                    WHERE entity_type_id = @entityTypeId AND entity_id = @entityId AND pool_id = @poolId;

                    DELETE FROM [{_schemaName}].[search_values]
                    WHERE entity_type_id = @entityTypeId AND entity_id = @entityId AND pool_id = @poolId;

                    -- Update the main values record
                    UPDATE [{_schemaName}].[entities]
                    SET
                        entity_type_name = @entityTypeName,
                        value = @value,
                        dso_type_schema_version = @dsoTypeSchemaVersion,
                        value_version = value_version + 1,
                        {expiresAtSql}
                        last_updated_at = @now
                    WHERE entity_type_id = @entityTypeId AND entity_id = @entityId AND pool_id = @poolId;

                    SELECT 0 AS Result; -- Success
                    """;

                _ = batchCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
                _ = batchCmd.Parameters.AddWithValue("@entityId", SqlServerGuidConverter.ToSqlServer(op.Id.Value));
                _ = batchCmd.Parameters.AddWithValue("@poolId", PoolId.Value);
                _ = batchCmd.Parameters.AddWithValue("@expectedVersion", op.ExpectedEntityVersion);
                _ = batchCmd.Parameters.AddWithValue("@entityTypeName", entityType.Name);
                _ = batchCmd.Parameters.AddWithValue("@value", jsonDso);
                _ = batchCmd.Parameters.AddWithValue("@dsoTypeSchemaVersion", (int)dsoVersion.SchemaVersion);
                _ = batchCmd.Parameters.AddWithValue("@now", timeProvider.GetUtcNow());
                if (hasExpirationChange)
                {
                    // expiresAt is null when Expiration.Never — DBNull.Value is needed for SQL NULL
#pragma warning disable CA1508 // Avoid dead conditional code — false positive: Expiration.Never.Resolve() returns null
                    _ = batchCmd.Parameters.AddWithValue("@expiresAt", (object?)expiresAt ?? DBNull.Value);
#pragma warning restore CA1508
                }

                Log.ExecutingSql(logger, batchCmd.CommandText);
                var result = (int)(await batchCmd.ExecuteScalarAsync(ct))!;

                if (result == -1)
                {
                    return OperationOutcome.DoesNotExist;
                }

                if (result == -2)
                {
                    return OperationOutcome.UnexpectedVersion;
                }
            }

            // Bulk insert new keys using TVP
            if (op.Keys.Count > 0)
            {
                await using var keysCmd = connection.CreateCommand();
                keysCmd.Transaction = transaction;
                keysCmd.CommandType = CommandType.Text;
                keysCmd.CommandText = $"""
                    INSERT INTO [{_schemaName}].[entity_keys] (
                        entity_type_id,
                        key_type_id,
                        key_type_name,
                        key_type_version,
                        key_value,
                        key_json,
                        entity_id,
                        pool_id
                    )
                    SELECT
                        @entityTypeId,
                        key_type_id,
                        key_type_name,
                        key_type_version,
                        key_value,
                        key_json,
                        @entityId,
                        @poolId
                    FROM @keys
                    """;

                _ = keysCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
                _ = keysCmd.Parameters.AddWithValue("@entityId", SqlServerGuidConverter.ToSqlServer(op.Id.Value));
                _ = keysCmd.Parameters.AddWithValue("@poolId", PoolId.Value);

                var keysTable = new DataTable();
                _ = keysTable.Columns.Add("key_type_id", typeof(int));
                _ = keysTable.Columns.Add("key_type_name", typeof(string));
                _ = keysTable.Columns.Add("key_type_version", typeof(int));
                _ = keysTable.Columns.Add("key_value", typeof(Guid));
                _ = keysTable.Columns.Add("key_json", typeof(string));

                foreach (var key in op.Keys)
                {
                    _ = keysTable.Rows.Add(
                        (int)key.DskVersion.KeyType.Id,
                        key.DskVersion.KeyType.Name,
                        (int)key.DskVersion.SchemaVersion,
                        key.Value,
                        (object?)key.KeyJsonValue ?? DBNull.Value
                    );
                }

                var keysParam = keysCmd.Parameters.AddWithValue("@keys", keysTable);
                keysParam.SqlDbType = SqlDbType.Structured;
                keysParam.TypeName = $"[{_schemaName}].[KeyTableType]";

                Log.ExecutingSql(logger, keysCmd.CommandText);
                _ = await keysCmd.ExecuteNonQueryAsync(ct);
            }

            // Bulk insert new search fields using TVP
            if (op.SearchFieldCollection.Count > 0)
            {
                await using var searchCmd = connection.CreateCommand();
                searchCmd.Transaction = transaction;
                searchCmd.CommandType = CommandType.Text;
                searchCmd.CommandText = $"""
                    INSERT INTO [{_schemaName}].[search_values] (
                        entity_type_id,
                        entity_id,
                        field_path,
                        field_path_text,
                        item_index,
                        string_value,
                        number_value,
                        datetime_value,
                        boolean_value,
                        guid_value,
                        pool_id
                    )
                    SELECT
                        @entityTypeId,
                        @entityId,
                        field_path,
                        field_path_text,
                        item_index,
                        string_value,
                        number_value,
                        datetime_value,
                        boolean_value,
                        guid_value,
                        @poolId
                    FROM @searchValues
                    """;

                _ = searchCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
                _ = searchCmd.Parameters.AddWithValue("@entityId", SqlServerGuidConverter.ToSqlServer(op.Id.Value));
                _ = searchCmd.Parameters.AddWithValue("@poolId", PoolId.Value);

                var searchTable = new DataTable();
                _ = searchTable.Columns.Add("field_path", typeof(Guid));
                _ = searchTable.Columns.Add("field_path_text", typeof(string));
                _ = searchTable.Columns.Add("item_index", typeof(int));
                _ = searchTable.Columns.Add("string_value", typeof(string));
                _ = searchTable.Columns.Add("number_value", typeof(decimal));
                _ = searchTable.Columns.Add("datetime_value", typeof(DateTimeOffset));
                _ = searchTable.Columns.Add("boolean_value", typeof(bool));
                _ = searchTable.Columns.Add("guid_value", typeof(Guid));

                foreach (var field in op.SearchFieldCollection)
                {
                    _ = searchTable.Rows.Add(
                        field.FieldPathId,
                        field.FieldPath,
                        field.ItemIndex ?? -1,
                        (object?)field.StringValue ?? DBNull.Value,
                        field.NumberValue.HasValue ? field.NumberValue.Value : DBNull.Value,
                        field.DateTimeValue.HasValue ? field.DateTimeValue.Value : DBNull.Value,
                        field.BooleanValue.HasValue ? field.BooleanValue.Value : DBNull.Value,
                        field.GuidValue.HasValue ? field.GuidValue.Value : DBNull.Value
                    );
                }

                var searchParam = searchCmd.Parameters.AddWithValue("@searchValues", searchTable);
                searchParam.SqlDbType = SqlDbType.Structured;
                searchParam.TypeName = $"[{_schemaName}].[SearchValueTableType]";

                Log.ExecutingSql(logger, searchCmd.CommandText);
                _ = await searchCmd.ExecuteNonQueryAsync(ct);
            }

            return OperationOutcome.Success;
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            // Key conflict during update
            if (ex.Message.Contains($"PK_{_schemaName}_entity_keys", StringComparison.OrdinalIgnoreCase))
            {
                return OperationOutcome.KeyConflict;
            }

            throw;
        }
    }

    private async Task<(OperationOutcome Outcome, bool EntityDeleted)> ExecuteDeleteCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        DeleteOperation op,
        Ct ct)
    {
        var entityType = op.EntityType;
        Guid entityId;

        if (op.Id is not null)
        {
            Log.DeletingDso(logger, entityType, op.Id.Value);
            entityId = SqlServerGuidConverter.ToSqlServer(op.Id.Value);
        }
        else if (op.Key is not null)
        {
            var key = op.Key;

            await using var resolveCmd = connection.CreateCommand();
            resolveCmd.Transaction = transaction;
            resolveCmd.CommandType = CommandType.Text;
            resolveCmd.CommandText = $"""
                SELECT entity_id
                FROM [{_schemaName}].[entity_keys]
                WHERE entity_type_id = @entityTypeId
                  AND key_type_id = @keyTypeId
                  AND key_type_version = @keyTypeVersion
                  AND key_value = @keyValue
                  AND pool_id = @poolId
                """;
            _ = resolveCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
            _ = resolveCmd.Parameters.AddWithValue("@poolId", PoolId.Value);
            _ = resolveCmd.Parameters.AddWithValue("@keyTypeId", (int)key.DskVersion.KeyType.Id);
            _ = resolveCmd.Parameters.AddWithValue("@keyTypeVersion", (int)key.DskVersion.SchemaVersion);
            _ = resolveCmd.Parameters.AddWithValue("@keyValue", key.Value);
            Log.ExecutingSql(logger, resolveCmd.CommandText);

            var resolved = await resolveCmd.ExecuteScalarAsync(ct);
            if (resolved is not Guid resolvedEntityId)
            {
                return (OperationOutcome.Success, false);
            }

            entityId = resolvedEntityId;
        }
        else
        {
            return (OperationOutcome.Success, false);
        }

        return await DeleteEntityAndLinksAsync(connection, transaction, entityType, entityId, allowRetry: true, ct);
    }

    /// <summary>
    /// Deletes links where the given entity is the left side, and right-side links that are not
    /// restricted (both foreign keys on <c>entity_links</c> are <c>NO ACTION</c> on SQL Server, so
    /// the engine performs this cleanup itself, in the same transaction, before deleting the
    /// entity), then deletes the entity itself. If a restricted right-side link remains, the
    /// entity delete's 547 is reported as <see cref="OperationOutcome.StillLinked"/> after
    /// re-checking for a restricted link (not by parsing the constraint name), rather than by
    /// assuming it's the only possible cause.
    /// </summary>
    private async Task<(OperationOutcome Outcome, bool EntityDeleted)> DeleteEntityAndLinksAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        EntityType entityType,
        Guid entityId,
        bool allowRetry,
        Ct ct)
    {
        await using (var linkDeleteCmd = connection.CreateCommand())
        {
            linkDeleteCmd.Transaction = transaction;
            linkDeleteCmd.CommandType = CommandType.Text;
            linkDeleteCmd.CommandText = $"""
                DELETE FROM [{_schemaName}].[entity_links]
                WHERE pool_id = @poolId
                  AND (
                    (left_entity_type_id = @entityTypeId AND left_entity_id = @entityId)
                    OR (right_entity_type_id = @entityTypeId AND right_entity_id = @entityId AND restrict_delete = 0)
                  )
                """;
            _ = linkDeleteCmd.Parameters.AddWithValue("@poolId", PoolId.Value);
            _ = linkDeleteCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
            _ = linkDeleteCmd.Parameters.AddWithValue("@entityId", entityId);
            Log.ExecutingSql(logger, linkDeleteCmd.CommandText);
            _ = await linkDeleteCmd.ExecuteNonQueryAsync(ct);
        }

        await using var deleteCmd = connection.CreateCommand();
        deleteCmd.Transaction = transaction;
        deleteCmd.CommandType = CommandType.Text;
        deleteCmd.CommandText = $"""
            DELETE FROM [{_schemaName}].[entities]
            OUTPUT deleted.entity_id
            WHERE entity_type_id = @entityTypeId AND entity_id = @entityId AND pool_id = @poolId
            """;
        _ = deleteCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
        _ = deleteCmd.Parameters.AddWithValue("@entityId", entityId);
        _ = deleteCmd.Parameters.AddWithValue("@poolId", PoolId.Value);

        Log.ExecutingSql(logger, deleteCmd.CommandText);

        try
        {
            var result = await deleteCmd.ExecuteScalarAsync(ct);
            var deletedEntityId = result is Guid guid ? guid : (Guid?)null;
            return (OperationOutcome.Success, deletedEntityId.HasValue);
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            if (await HasRestrictedRightLinkAsync(connection, transaction, entityType, entityId, ct))
            {
                return (OperationOutcome.StillLinked, false);
            }

            if (!allowRetry)
            {
                throw;
            }

            // No restricted link remains: a cascading link must have been inserted concurrently
            // between the link-cleanup statement and the entity-delete statement. Retry the
            // two-statement delete once — the window is sub-statement, so a repeat failure
            // indicates a genuine problem, not just a race.
            Log.RetryingDeleteAfterLinkRace(logger, entityType, entityId);
            return await DeleteEntityAndLinksAsync(connection, transaction, entityType, entityId, allowRetry: false, ct);
        }
    }

    /// <summary>
    /// Returns whether a restricted (<c>restrict_delete = 1</c>) link still exists with the given
    /// entity as the right side.
    /// </summary>
    private async Task<bool> HasRestrictedRightLinkAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        EntityType entityType,
        Guid entityId,
        Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM [{_schemaName}].[entity_links]
                WHERE pool_id = @poolId
                  AND right_entity_type_id = @entityTypeId
                  AND right_entity_id = @entityId
                  AND restrict_delete = 1
            ) THEN 1 ELSE 0 END
            """;
        _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@entityId", entityId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture) != 0;
    }



    /// <summary>
    /// Builds the WHERE clause, JOIN clause, ORDER BY clause, and calculates the offset for a query.
    /// </summary>
    private QueryClauses BuildQueryClauses(
        SqlCommand cmd,
        IQueryExpression filter,
        SortParameter sort,
        int offset,
        EntityType entityType)
    {
        // Build WHERE clause using shared SqlWhereClauseBuilder
        var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect, LinkFilterTableNames, entityType);
        var whereClause = whereBuilder.BuildWhereClause(filter);

        // Build JOIN clause and ORDER BY clause
        string joinClause;
        string orderByClause;

        if (!sort.IsEmpty)
        {
            var sortFieldPath = sort.Field!.Path;

            // Determine which column to sort on based on field type
            var sortColumn = GetSortColumnName(sort.Field!);

            // Timestamp fields are columns on the entities table — no JOIN needed
            if (SystemFields.IsSystemField(sortFieldPath))
            {
                joinClause = "";
            }
            else
            {
                // We'll use a LEFT JOIN to get the sort field value
                // SQL Server doesn't support NULLS LAST syntax, so we use CASE expression
                joinClause = $"""
                    LEFT JOIN [{_schemaName}].[search_values] sort_sv
                      ON v.entity_type_id = sort_sv.entity_type_id
                      AND v.entity_id = sort_sv.entity_id
                      AND v.pool_id = sort_sv.pool_id
                      AND sort_sv.field_path = @sort_field_path
                      AND sort_sv.item_index = -1
                    """;

                _ = cmd.Parameters.AddWithValue("@sort_field_path", DeterministicGuidGenerator.Create(sortFieldPath.ToUpperInvariant()));
            }

            // Use CASE expression to handle NULLS LAST behavior
            if (sort.Direction == SortDirection.Ascending)
            {
                orderByClause = $"""
                    ORDER BY
                      CASE WHEN {sortColumn} IS NULL THEN 1 ELSE 0 END,
                      {sortColumn} ASC,
                      v.entity_id ASC
                    """;
            }
            else
            {
                orderByClause = $"""
                    ORDER BY
                      CASE WHEN {sortColumn} IS NULL THEN 1 ELSE 0 END,
                      {sortColumn} DESC,
                      v.entity_id ASC
                    """;
            }
        }
        else
        {
            joinClause = "";
            orderByClause = "ORDER BY v.entity_id ASC";
        }


        return new QueryClauses(whereClause, joinClause, orderByClause, offset);
    }

    /// <summary>
    /// Builds the WHERE clause, JOIN clause, ORDER BY clause, and seek clause for cursor-based pagination.
    /// </summary>
    private CursorQueryClauses BuildCursorQueryClauses(
        SqlCommand cmd,
        IQueryExpression filter,
        SortParameter sort,
        CursorPosition position,
        CursorDirection traversal,
        EntityType entityType)
    {
        var (whereClause, joinClause, sortColumn) = BuildCursorFilterAndJoin(cmd, filter, sort, entityType);

        var builder = new KeysetSqlBuilder(Dialect, sortColumn, sort.IsEmpty ? SortDirection.Ascending : sort.Direction);
        var orderByClause = $"ORDER BY {builder.OrderBy(traversal)}";
        var predicate = builder.StrictlyAfter(traversal, position, "@last_sort_value", "@last_id");
        var seekClause = predicate.Length == 0 ? "" : $"AND ({predicate})";

        BindCursorPositionParameters(cmd, position, sortColumn);

        return new CursorQueryClauses(whereClause, joinClause, orderByClause, seekClause, sortColumn ?? "NULL");
    }

    /// <summary>
    /// Builds the WHERE clause, JOIN clause, and sort column for a cursor query, without the
    /// ORDER BY/seek predicate (shared between the main query and the opposite-side probe).
    /// </summary>
    private (string WhereClause, string JoinClause, string? SortColumn) BuildCursorFilterAndJoin(
        SqlCommand cmd,
        IQueryExpression filter,
        SortParameter sort,
        EntityType entityType)
    {
        var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect, LinkFilterTableNames, entityType);
        var whereClause = whereBuilder.BuildWhereClause(filter);

        if (sort.IsEmpty)
        {
            return (whereClause, "", null);
        }

        var sortFieldPath = sort.Field!.Path;
        var sortColumn = GetSortColumnName(sort.Field!);

        string joinClause;
        if (SystemFields.IsSystemField(sortFieldPath))
        {
            joinClause = "";
        }
        else
        {
            joinClause = $"""
                LEFT JOIN [{_schemaName}].[search_values] sort_sv
                  ON v.entity_type_id = sort_sv.entity_type_id
                  AND v.entity_id = sort_sv.entity_id
                  AND v.pool_id = sort_sv.pool_id
                  AND sort_sv.field_path = @sort_field_path
                  AND sort_sv.item_index = -1
                """;

            _ = cmd.Parameters.AddWithValue("@sort_field_path", DeterministicGuidGenerator.Create(sortFieldPath.ToUpperInvariant()));
        }

        return (whereClause, joinClause, sortColumn);
    }

    /// <summary>
    /// Binds <c>@last_id</c> and, when the seek predicate references it, <c>@last_sort_value</c>
    /// for a decoded cursor key position, using the same per-kind conversions as insertion.
    /// </summary>
    private static void BindCursorPositionParameters(SqlCommand cmd, CursorPosition position, string? sortColumn)
    {
        if (position.Kind != CursorPositionKind.Key)
        {
            return;
        }

        _ = cmd.Parameters.AddWithValue("@last_id", SqlServerGuidConverter.ToSqlServer(position.Id));

        if (sortColumn is null || position.ValueKind == CursorValueKind.Null)
        {
            return;
        }

        object boundValue = position.Value switch
        {
            string s => s,
            decimal d => d,
            DateTimeOffset dto => dto,
            bool b => b,
            Guid g => g,
            _ => throw new InvalidOperationException($"Unsupported cursor sort value type: {position.Value!.GetType().Name}")
        };

        _ = cmd.Parameters.AddWithValue("@last_sort_value", boundValue);
    }

    /// <summary>
    /// Gets the SQL column name for sorting based on field type.
    /// </summary>
    private static string GetSortColumnName(Field field)
    {
        if (SystemFields.IsSystemField(field.Path))
        {
            return field is DateTimeField
                ? string.Equals(field.Path, SystemFields.Created, StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(field.Path, SystemFields.CreatedAttributeName, StringComparison.OrdinalIgnoreCase)
                    ? "v.created_at"
                    : "v.last_updated_at"
                : throw new InvalidOperationException($"System field '{field.Path}' must use DateTimeField, not {field.GetType().Name}.");
        }

        return field switch
        {
            StringField => "sort_sv.string_value",
            NumberField => "sort_sv.number_value",
            DateTimeField => "sort_sv.datetime_value",
            BooleanField => "sort_sv.boolean_value",
            GuidField or ExactMatchField => "sort_sv.guid_value",
            _ => throw new InvalidOperationException($"Unsupported field type for sorting: {field.GetType().Name}")
        };
    }

    /// <summary>
    /// Reads a field value from the database reader.
    /// The columnIndex parameter should point to the string_value column (column 2),
    /// and this method will offset appropriately based on field type.
    /// </summary>
    private static async Task<object?> ReadFieldValueAsync(SqlDataReader reader, FieldType fieldType, int stringValueColumnIndex, Ct ct)
    {
        // Calculate the correct column index based on field type
        // Columns are: string_value (stringValueColumnIndex), number_value (+1), datetime_value (+2), boolean_value (+3), guid_value (+4)
        var columnIndex = fieldType switch
        {
            FieldType.String => stringValueColumnIndex,
            FieldType.Number => stringValueColumnIndex + 1,
            FieldType.DateTime => stringValueColumnIndex + 2,
            FieldType.Boolean => stringValueColumnIndex + 3,
            FieldType.Guid => stringValueColumnIndex + 4,
            _ => throw new InvalidOperationException($"Unsupported field type: {fieldType}")
        };

        if (await reader.IsDBNullAsync(columnIndex, ct))
        {
            return null;
        }

        return fieldType switch
        {
            FieldType.String => reader.GetString(columnIndex),
            FieldType.Number => reader.GetDecimal(columnIndex),
            FieldType.DateTime => reader.GetDateTimeOffset(columnIndex),
            FieldType.Boolean => reader.GetBoolean(columnIndex),
            FieldType.Guid => reader.GetGuid(columnIndex),
            _ => throw new InvalidOperationException($"Unsupported field type: {fieldType}")
        };
    }

    /// <summary>
    /// Reads a sort value from a database reader for the specified field type.
    /// </summary>
    private static async Task<object?> ReadSortValueAsync(SqlDataReader reader, Field sortField, int columnIndex, Ct ct)
    {
        if (await reader.IsDBNullAsync(columnIndex, ct))
        {
            return null;
        }

        return sortField switch
        {
            StringField => reader.GetString(columnIndex),
            NumberField => reader.GetDecimal(columnIndex),
            DateTimeField => reader.GetDateTimeOffset(columnIndex),
            BooleanField => reader.GetBoolean(columnIndex),
            GuidField or ExactMatchField => reader.GetGuid(columnIndex),
            _ => throw new InvalidOperationException($"Unsupported field type for sorting: {sortField.GetType().Name}")
        };
    }

    private static readonly ISqlDialect Dialect = new MsSqlDialect();

    private static (int Skip, int Take) ResolveOffsetAndSize(DataRange dataRange)
    {
        if (dataRange.OffsetValue is not null)
        {
            return ((int)dataRange.OffsetValue.Skip.Value, dataRange.OffsetValue.Take.Value);
        }

        if (dataRange.PageValue is not null)
        {
            var page = dataRange.PageValue.Page.Value;
            var size = dataRange.PageValue.PageSize.Value;
            return ((page - 1) * size, size);
        }

        return (0, DataRangeSize.Default.Value);
    }

    private (StringBuilder JoinSql, string LastFilterColumn) BuildLinkJoins(
        LinkQueryDescriptor query,
        SqlCommand cmd)
    {
        var joinSql = new StringBuilder();
        var whereLastJoin = "";

        for (var i = 0; i < query.Joins.Count; i++)
        {
            var join = query.Joins[i];
            var linkTypeParam = $"@lt{i}";
            _ = cmd.Parameters.AddWithValue(linkTypeParam, (int)join.Definition.Link.Id);

            // Which side of this link corresponds to the source (entities table / previous join)?
            string sourceSide;
            string filterSide;
            if (join.Direction == LinkJoinDirection.LeftToRight)
            {
                sourceSide = "left_entity_id";
                filterSide = "right_entity_id";
            }
            else
            {
                sourceSide = "right_entity_id";
                filterSide = "left_entity_id";
            }

            if (i == 0)
            {
                // First join: links entity table to first link table
                _ = joinSql.AppendLine(CultureInfo.InvariantCulture,
                    $"JOIN [{_schemaName}].[entity_links] l0 ON l0.{sourceSide} = e.entity_id AND l0.link_type_id = {linkTypeParam} AND l0.pool_id = @pool_id");
            }
            else
            {
                // Subsequent joins: link previous join's filter side to this join's source side
                var prevJoin = query.Joins[i - 1];
                string prevFilterSide;
                if (prevJoin.Direction == LinkJoinDirection.LeftToRight)
                {
                    prevFilterSide = "right_entity_id";
                }
                else
                {
                    prevFilterSide = "left_entity_id";
                }

                _ = joinSql.AppendLine(CultureInfo.InvariantCulture,
                    $"JOIN [{_schemaName}].[entity_links] l{i} ON l{i}.{sourceSide} = l{i - 1}.{prevFilterSide} AND l{i}.link_type_id = {linkTypeParam} AND l{i}.pool_id = @pool_id");
            }

            if (i == query.Joins.Count - 1)
            {
                whereLastJoin = $"l{i}.{filterSide}";
            }
        }

        return (joinSql, whereLastJoin);
    }

    async Task<QueryResult<LinkedEnvelope>> IPartitionedStorage.QueryLinksByAnchorAsync(
        LinkQueryDescriptor query,
        DataRange dataRange,
        Ct ct)
    {
        try
        {
            if (query.WhereEntityIds is null)
            {
                throw new ArgumentException("QueryLinksByAnchorAsync requires WhereAny to be set.", nameof(query));
            }

            if (dataRange.TokenValue is not null)
            {
                throw new NotSupportedException("Cursor-based pagination is not supported for link queries.");
            }

            var (skip, take) = ResolveOffsetAndSize(dataRange);

            if (query.WhereEntityIds.Count == 0)
            {
                return new QueryResult<LinkedEnvelope> { Items = [], HasMoreData = false, HasPreviousData = false };
            }

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var (joinSql, anchorColumn) = BuildLinkJoins(query, cmd);
            var idsTable = new DataTable();
            _ = idsTable.Columns.Add("entity_id", typeof(Guid));
            foreach (var id in query.WhereEntityIds)
            {
                _ = idsTable.Rows.Add(SqlServerGuidConverter.ToSqlServer(id.Value));
            }

            var idsParam = cmd.Parameters.AddWithValue("@anchorIds", idsTable);
            idsParam.SqlDbType = SqlDbType.Structured;
            idsParam.TypeName = $"[{_schemaName}].[EntityIdTableType]";

            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@source_entity_type_id", (int)query.SourceEntityType.Id);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take + 1);

            // Dedupe and page on ids only, then fetch the values for the page, so the sort doesn't carry `value` for every skipped row.
            cmd.CommandText = $"""
                SELECT p.anchor_id, ent.entity_id, ent.value, ent.dso_type_schema_version, ent.value_version, ent.created_at, ent.last_updated_at
                FROM (
                    SELECT DISTINCT {anchorColumn} AS anchor_id, e.entity_id
                    FROM [{_schemaName}].[entities] e
                    {joinSql}
                    WHERE e.entity_type_id = @source_entity_type_id
                      AND e.pool_id = @pool_id
                      AND {anchorColumn} IN (SELECT entity_id FROM @anchorIds)
                    ORDER BY anchor_id, e.entity_id
                    OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
                ) p
                JOIN [{_schemaName}].[entities] ent ON ent.entity_id = p.entity_id AND ent.pool_id = @pool_id AND ent.entity_type_id = @source_entity_type_id
                ORDER BY p.anchor_id, ent.entity_id
                """;
            Log.ExecutingQuery(logger, cmd.CommandText);

            var items = new List<LinkedEnvelope>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var anchorId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
                    var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(1));
                    var jsonValue = reader.GetString(2);
                    var dsoTypeVersion = reader.GetInt32(3);
                    var valueVersion = reader.GetInt32(4);
                    var created = reader.GetDateTimeOffset(5);
                    var lastUpdated = reader.GetDateTimeOffset(6);

                    var version = new DataStorageObjectVersion(query.SourceEntityType, (uint)dsoTypeVersion);
                    var dsoType = dataStorageTypeRegistry.Get(version);
                    var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                    items.Add(new LinkedEnvelope(anchorId, new MetadataEnvelope(item, entityId, valueVersion, created, lastUpdated)));
                }
            }

            var hasMore = items.Count > take;
            if (hasMore)
            {
                items.RemoveAt(items.Count - 1);
            }

            return new QueryResult<LinkedEnvelope>
            {
                Items = items,
                HasMoreData = hasMore,
                HasPreviousData = skip > 0 && items.Count > 0
            };
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<IReadOnlyDictionary<Storage.UuidV7, long>> IPartitionedStorage.CountLinksAsync(
        LinkQueryDescriptor query,
        Ct ct)
    {
        try
        {
            if (query.WhereEntityIds is null)
            {
                throw new ArgumentException("CountLinksAsync requires WhereAny to be set.", nameof(query));
            }

            if (query.WhereEntityIds.Count == 0)
            {
                return new Dictionary<Storage.UuidV7, long>();
            }

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var (joinSql, anchorColumn) = BuildLinkJoins(query, cmd);
            var idsTable = new DataTable();
            _ = idsTable.Columns.Add("entity_id", typeof(Guid));
            foreach (var id in query.WhereEntityIds)
            {
                _ = idsTable.Rows.Add(SqlServerGuidConverter.ToSqlServer(id.Value));
            }

            var idsParam = cmd.Parameters.AddWithValue("@anchorIds", idsTable);
            idsParam.SqlDbType = SqlDbType.Structured;
            idsParam.TypeName = $"[{_schemaName}].[EntityIdTableType]";

            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@source_entity_type_id", (int)query.SourceEntityType.Id);

            cmd.CommandText = $"""
                SELECT {anchorColumn} AS anchor_id, COUNT_BIG(DISTINCT e.entity_id)
                FROM [{_schemaName}].[entities] e
                {joinSql}
                WHERE e.entity_type_id = @source_entity_type_id
                  AND e.pool_id = @pool_id
                  AND {anchorColumn} IN (SELECT entity_id FROM @anchorIds)
                GROUP BY {anchorColumn}
                """;
            Log.ExecutingSql(logger, cmd.CommandText);

            var counts = query.WhereEntityIds.ToDictionary(id => id, _ => 0L);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                counts[SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0))] = reader.GetInt64(1);
            }

            return counts;
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    /// <inheritdoc/>
    async Task<QueryResult<MetadataEnvelope>> IPartitionedStorage.QueryLinksAsync(
        LinkQueryDescriptor query,
        DataRange dataRange,
        Ct ct)
    {
        try
        {
            if (query.WhereEntityIds is not null)
            {
                throw new ArgumentException(
                    "WhereAny is not supported by QueryLinksAsync. Use QueryLinksByAnchorAsync.", nameof(query));
            }

            if (dataRange.TokenValue is not null)
            {
                throw new NotSupportedException("Cursor-based pagination is not supported for link queries.");
            }

            var (skip, take) = ResolveOffsetAndSize(dataRange);
            var sourceEntityTypeId = (int)query.SourceEntityType.Id;

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var (joinSql, whereLastJoin) = BuildLinkJoins(query, cmd);

            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@source_entity_type_id", sourceEntityTypeId);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take);

            string whereClause;
            if (query.WhereEntityId is not null)
            {
                _ = cmd.Parameters.AddWithValue("@where_entity_id", SqlServerGuidConverter.ToSqlServer(query.WhereEntityId.Value));
                whereClause = $"{whereLastJoin} = @where_entity_id";
            }
            else
            {
                whereClause = "1=1";
            }

            var mainQuery = $"""
                SELECT DISTINCT e.entity_id, e.value, e.dso_type_schema_version, e.value_version, e.created_at, e.last_updated_at
                FROM [{_schemaName}].[entities] e
                {joinSql}
                WHERE e.entity_type_id = @source_entity_type_id
                  AND e.pool_id = @pool_id
                  AND {whereClause}
                ORDER BY e.entity_id
                OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY
                """;

            cmd.CommandText = mainQuery;
            Log.ExecutingQuery(logger, mainQuery);

            var items = new List<MetadataEnvelope>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
                    var jsonValue = reader.GetString(1);
                    var dsoTypeVersion = reader.GetInt32(2);
                    var valueVersion = reader.GetInt32(3);
                    var created = reader.GetDateTimeOffset(4);
                    var lastUpdated = reader.GetDateTimeOffset(5);

                    var version = new DataStorageObjectVersion(query.SourceEntityType, (uint)dsoTypeVersion);
                    var dsoType = dataStorageTypeRegistry.Get(version);
                    var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                    items.Add(new MetadataEnvelope(item, entityId, valueVersion, created, lastUpdated));
                }
            }

            // Count query — reuse same join/where but count distinct entities
            var countQuery = $"""
                SELECT COUNT(DISTINCT e.entity_id)
                FROM [{_schemaName}].[entities] e
                {joinSql}
                WHERE e.entity_type_id = @source_entity_type_id
                  AND e.pool_id = @pool_id
                  AND {whereClause}
                """;

            await using var countCmd = connection.CreateCommand();
            countCmd.CommandType = CommandType.Text;
            _ = countCmd.Parameters.AddWithValue("@source_entity_type_id", sourceEntityTypeId);
            _ = countCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            if (query.WhereEntityId is not null)
            {
                _ = countCmd.Parameters.AddWithValue("@where_entity_id", SqlServerGuidConverter.ToSqlServer(query.WhereEntityId.Value));
            }

            for (var i = 0; i < query.Joins.Count; i++)
            {
                _ = countCmd.Parameters.AddWithValue($"@lt{i}", (int)query.Joins[i].Definition.Link.Id);
            }

            countCmd.CommandText = countQuery;
            Log.ExecutingSql(logger, countQuery);
            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);

            return new QueryResult<MetadataEnvelope>
            {
                Items = items,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / take),
                HasMoreData = skip + take < totalCount,
                HasPreviousData = skip > 0 && totalCount > 0
            };
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<long> IPartitionedStorage.CountAsync(
        EntityType entityType,
        IQueryExpression? filter,
        Ct ct)
    {
        try
        {
            var entityTypeId = (int)entityType.Id;

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            string whereClause;
            if (filter is null or AllExpression)
            {
                whereClause = "1=1";
            }
            else
            {
                var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect, LinkFilterTableNames, entityType);
                whereClause = whereBuilder.BuildWhereClause(filter);
            }

            var query = $"""
                SELECT COUNT(*)
                FROM [{_schemaName}].[entities] v
                WHERE v.entity_type_id = @entity_type_id
                  AND v.pool_id = @pool_id
                  AND ({whereClause})
                """;

            _ = cmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

            cmd.CommandText = query;

            Log.ExecutingQuery(logger, query);

            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<int> ICrossPartitionStorage.PurgeExpiredAsync(int batchSize, Ct ct)
    {
        try
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, StorageConstants.TtlCleanupMaxBatchSize);

            var now = timeProvider.GetUtcNow();

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = (SqlTransaction)transaction;
            cmd.CommandType = CommandType.Text;

            var sql = new StringBuilder();

            // Step 1: Lock expired rows into a temp table
            _ = sql.AppendLine(CultureInfo.InvariantCulture, $"""
                SELECT TOP (@batchSize) pool_id, entity_id, entity_type_id, entity_type_name, value, dso_type_schema_version, NEWID() AS event_id
                INTO #_expired
                FROM [{_schemaName}].[entities] WITH (UPDLOCK, ROWLOCK, READPAST)
                WHERE expires_at IS NOT NULL AND expires_at <= @now;
                """);
            _ = cmd.Parameters.AddWithValue("@now", now);
            _ = cmd.Parameters.AddWithValue("@batchSize", batchSize);

            // Step 2: Insert outbox events per matching subscription
            if (!outboxSubscriptions.IsEmpty)
            {
                var eventName = OutboxEventName.EntityExpired;
                _ = cmd.Parameters.AddWithValue("@eventName", eventName.ToString());

                var subscriptionIndex = 0;
                foreach (var subscription in outboxSubscriptions.Subscriptions)
                {
                    if (subscription.EventNames.Count > 0 && !subscription.EventNames.Contains(eventName))
                    {
                        continue;
                    }

                    var subParam = $"@sub{subscriptionIndex}";
                    _ = cmd.Parameters.AddWithValue(subParam, subscription.SubscriberName.ToString());

                    _ = sql.Append(CultureInfo.InvariantCulture, $"""
                        INSERT INTO [{_schemaName}].[outbox_subscriber_queue]
                        (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version)
                        SELECT NEWID(), event_id, @now, @eventName, entity_id, entity_type_id, entity_type_name, pool_id, value, {subParam}, dso_type_schema_version
                        FROM #_expired
                        """);

                    if (subscription.EntityTypeIds.Count > 0)
                    {
                        var typeIds = string.Join(", ", subscription.EntityTypeIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
                        _ = sql.Append(CultureInfo.InvariantCulture, $" WHERE entity_type_id IN ({typeIds})");
                    }

                    _ = sql.AppendLine(";");
                    subscriptionIndex++;
                }
            }

            // Step 3: Delete all links touching an expired entity, on either side. Both foreign keys
            // on entity_links are NO ACTION on SQL Server, so entity_links never cascades; these
            // explicit deletes stand in for the cascade the database can't do; restricted links are
            // removed too — purging an expired entity overrides Restrict.
            _ = sql.AppendLine(CultureInfo.InvariantCulture, $"""
                DELETE el FROM [{_schemaName}].[entity_links] el
                INNER JOIN #_expired e ON el.pool_id = e.pool_id
                    AND (
                        (el.left_entity_id = e.entity_id AND el.left_entity_type_id = e.entity_type_id)
                        OR (el.right_entity_id = e.entity_id AND el.right_entity_type_id = e.entity_type_id)
                    );
                """);

            // Step 4: Delete entities, then return the count from the temp table
            _ = sql.Append(CultureInfo.InvariantCulture, $"""
                DELETE e FROM [{_schemaName}].[entities] e
                INNER JOIN #_expired ek ON e.pool_id = ek.pool_id AND e.entity_type_id = ek.entity_type_id AND e.entity_id = ek.entity_id
                WHERE e.expires_at <= @now;
                SELECT COUNT(*) FROM #_expired;
                """);

            cmd.CommandText = sql.ToString();
            Log.ExecutingSql(logger, cmd.CommandText);
            var deleted = (int)(await cmd.ExecuteScalarAsync(ct))!;

            await transaction.CommitAsync(ct);
            return deleted;
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    Task<PurgeResult> ICrossPartitionStorage.PurgePoolAsync(PoolId poolId, Ct ct) => ((ICrossPartitionStorage)this).PurgePoolAsync(poolId, StorageConstants.PurgePoolDefaultBatchSize, ct);

    async Task<PurgeResult> ICrossPartitionStorage.PurgePoolAsync(PoolId poolId, int batchSize, Ct ct)
    {
        try
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

            var totalOutboxDeleted = 0;
            var totalLinksDeleted = 0;
            var totalEntitiesDeleted = 0;

            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            // Drain entity_links completely before any entity batch: entity_links no longer
            // cascades on SQL Server (both FKs are NO ACTION), so purging it explicitly first
            // avoids a foreign key violation on the later entity deletes. It must be fully
            // drained (not just one batch per entity batch), because an independent entity batch
            // could otherwise select an entity that still has links left over from a batch size
            // smaller than its in-degree.
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                await using var transaction = await connection.BeginTransactionAsync(ct);

                await using var linksCmd = connection.CreateCommand();
                linksCmd.Transaction = (SqlTransaction)transaction;
                linksCmd.CommandType = CommandType.Text;
                linksCmd.CommandText = $"""
                    DELETE TOP (@batchSize) FROM [{_schemaName}].[entity_links]
                    WHERE pool_id = @pool_id;
                    """;
                _ = linksCmd.Parameters.AddWithValue("@batchSize", batchSize);
                _ = linksCmd.Parameters.AddWithValue("@pool_id", poolId.Value);
                Log.ExecutingSql(logger, linksCmd.CommandText);
                var batchLinks = await linksCmd.ExecuteNonQueryAsync(ct);

                await transaction.CommitAsync(ct);
                totalLinksDeleted += batchLinks;

                if (batchLinks == 0)
                {
                    break;
                }
            }

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                await using var transaction = await connection.BeginTransactionAsync(ct);

                await using var outboxCmd = connection.CreateCommand();
                outboxCmd.Transaction = (SqlTransaction)transaction;
                outboxCmd.CommandType = CommandType.Text;
                outboxCmd.CommandText = $"""
                    DELETE TOP (@batchSize) FROM [{_schemaName}].[outbox_subscriber_queue]
                    WHERE pool_id = @pool_id;
                    """;
                _ = outboxCmd.Parameters.AddWithValue("@batchSize", batchSize);
                _ = outboxCmd.Parameters.AddWithValue("@pool_id", poolId.Value);
                Log.ExecutingSql(logger, outboxCmd.CommandText);
                var batchOutbox = await outboxCmd.ExecuteNonQueryAsync(ct);

                await using var entitiesCmd = connection.CreateCommand();
                entitiesCmd.Transaction = (SqlTransaction)transaction;
                entitiesCmd.CommandType = CommandType.Text;
                entitiesCmd.CommandText = $"""
                    DELETE TOP (@batchSize) FROM [{_schemaName}].[entities]
                    WHERE pool_id = @pool_id;
                    """;
                _ = entitiesCmd.Parameters.AddWithValue("@batchSize", batchSize);
                _ = entitiesCmd.Parameters.AddWithValue("@pool_id", poolId.Value);
                Log.ExecutingSql(logger, entitiesCmd.CommandText);
                var batchEntities = await entitiesCmd.ExecuteNonQueryAsync(ct);

                await transaction.CommitAsync(ct);

                totalOutboxDeleted += batchOutbox;
                totalEntitiesDeleted += batchEntities;

                if (batchOutbox + batchEntities == 0)
                {
                    break;
                }
            }

            return new PurgeResult(totalEntitiesDeleted, totalLinksDeleted, totalOutboxDeleted);
        }
        catch (SqlException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private sealed record QueryClauses(string WhereClause, string JoinClause, string OrderByClause, int Offset);

    private sealed record CursorQueryClauses(
        string WhereClause,
        string JoinClause,
        string OrderByClause,
        string SeekClause,
        string SortColumnName);

}
