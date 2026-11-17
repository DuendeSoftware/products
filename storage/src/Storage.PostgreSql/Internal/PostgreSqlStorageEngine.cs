// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data;
using System.Diagnostics;
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
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.PostgreSql.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
internal sealed class PostgreSqlStorageEngine(
    NpgsqlDataSource dataSource,
    PostgreSqlStorageEngineOptions options,
    DataStorageTypeRegistry dataStorageTypeRegistry,
    TimeProvider timeProvider,
    OutboxSubscriptions outboxSubscriptions,
    ILogger<PostgreSqlStorageEngine> logger) : StoreBase, IPartitionedStorage, ICrossPartitionStorage, IStorageInstanceSchema
{
    private const int RequiredSchemaVersion = 3;
    private static readonly ISqlDialect Dialect = new PostgreSqlDialect();

#pragma warning disable CA1308 // PostgreSQL folds unquoted identifiers to lowercase; we must match migration behavior
    private readonly string _schemaName = options.SchemaName.ToLowerInvariant();
    private readonly string _entities = FormatQualifiedTable(options.SchemaName, "entities");
    private readonly string _entityKeys = FormatQualifiedTable(options.SchemaName, "entity_keys");
    private readonly string _searchValues = FormatQualifiedTable(options.SchemaName, "search_values");
    private readonly string _entityLinks = FormatQualifiedTable(options.SchemaName, "entity_links");
    private readonly string _outboxSubscriptionQueue = FormatQualifiedTable(options.SchemaName, "outbox_subscriber_queue");

    private static string FormatQualifiedTable(string schema, string table) =>
        $"{Dialect.QuoteIdentifier(schema.ToLowerInvariant())}.{Dialect.QuoteIdentifier(table)}";
#pragma warning restore CA1308

    async Task<CheckSchemaVersionResult> IStorageInstanceSchema.CheckVersionAsync(Ct ct)
    {
        Log.CheckingSchemaVersion(logger);

        await using var cmd = dataSource.CreateCommand(
            "SELECT obj_description(oid, 'pg_namespace') FROM pg_catalog.pg_namespace WHERE nspname = $1");
        cmd.CommandType = CommandType.Text;
        _ = cmd.Parameters.AddWithValue(_schemaName);
        Log.ExecutingSql(logger, cmd.CommandText);
        var scalar = await cmd.ExecuteScalarAsync(ct);

        if (scalar is null or DBNull)
        {
            return new CheckSchemaVersionResult(0, RequiredSchemaVersion);
        }

        var comment = ((string)scalar).Trim();

        if (comment is "standard public schema" or "")
        {
            return new CheckSchemaVersionResult(0, RequiredSchemaVersion);
        }

        try
        {
            var schemaComment = JsonSerializer.Deserialize<SchemaComment>(comment)!;
            return new CheckSchemaVersionResult(schemaComment.Version, RequiredSchemaVersion);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid database schema comment", ex);
        }
    }

    async Task IStorageInstanceSchema.MigrateAsync(Ct ct)
    {
        Log.MigratingSchema(logger, _schemaName);

        var versionResult = await ((IStorageInstanceSchema)this).CheckVersionAsync(ct);
        var currentVersion = new DatabaseSchemaVersion((int)versionResult.CurrentVersion);

        var scripts = MigrationScriptLoader.GetScripts(typeof(PostgreSqlStorageEngine).Assembly, currentVersion, _schemaName);
        foreach (var (targetVersion, sql) in scripts)
        {
            Log.ExecutingMigrationStep(logger, currentVersion.Value, targetVersion);

            await using var cnn = await dataSource.OpenConnectionAsync(ct);
            await using var tx = await cnn.BeginTransactionAsync(ct);

            await using var cmd = cnn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            Log.ExecutingSql(logger, cmd.CommandText);
            _ = await cmd.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);

            currentVersion = new DatabaseSchemaVersion(targetVersion);
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
        var errors = new List<SchemaVerificationError>();

        // Expected tables and their columns (table -> column -> data_type)
        var expectedColumns = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["entities"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["pool_id"] = "integer",
                ["entity_type_id"] = "integer",
                ["entity_id"] = "uuid",
                ["entity_type_name"] = "text",
                ["value"] = "jsonb",
                ["dso_type_schema_version"] = "integer",
                ["value_version"] = "integer",
                ["created_at"] = "timestamp with time zone",
                ["last_updated_at"] = "timestamp with time zone",
                ["expires_at"] = "timestamp with time zone",
            },
            ["entity_keys"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["pool_id"] = "integer",
                ["entity_type_id"] = "integer",
                ["key_type_id"] = "integer",
                ["key_type_version"] = "integer",
                ["key_type_name"] = "text",
                ["key_value"] = "uuid",
                ["key_json"] = "jsonb",
                ["entity_id"] = "uuid",
                ["timestamp"] = "timestamp without time zone",
            },
            ["search_values"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["entity_type_id"] = "integer",
                ["entity_id"] = "uuid",
                ["field_path"] = "uuid",
                ["field_path_text"] = "text",
                ["item_index"] = "integer",
                ["string_value"] = "text",
                ["number_value"] = "numeric",
                ["datetime_value"] = "timestamp with time zone",
                ["boolean_value"] = "boolean",
                ["guid_value"] = "uuid",
                ["pool_id"] = "integer",
            },
            ["entity_links"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["pool_id"] = "integer",
                ["link_type_id"] = "integer",
                ["left_entity_type_id"] = "integer",
                ["left_entity_id"] = "uuid",
                ["right_entity_type_id"] = "integer",
                ["right_entity_id"] = "uuid",
                ["restrict_delete"] = "boolean",
                ["created_at"] = "timestamp without time zone",
            },
            ["outbox_subscriber_queue"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["sequence_number"] = "bigint",
                ["message_id"] = "uuid",
                ["event_id"] = "uuid",
                ["timestamp"] = "timestamp with time zone",
                ["event_name"] = "text",
                ["subject_id"] = "uuid",
                ["entity_type_id"] = "integer",
                ["entity_type_name"] = "text",
                ["pool_id"] = "integer",
                ["payload"] = "jsonb",
                ["subscriber_name"] = "text",
                ["dso_type_schema_version"] = "integer",
            },
        };

        // Query actual columns from information_schema
        await using var colCmd = dataSource.CreateCommand(
            """
            SELECT table_name, column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = $1
            ORDER BY table_name, column_name
            """);
        _ = colCmd.Parameters.AddWithValue(_schemaName);
        Log.ExecutingSql(logger, colCmd.CommandText);

        var actualColumns = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var actualColumnTypes = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var actualColumnNullability = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);

        await using (var reader = await colCmd.ExecuteReaderAsync(ct))
        {
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
                }

                _ = cols.Add(columnName);
                actualColumnTypes[tableName][columnName] = dataType;
                actualColumnNullability[tableName][columnName] = reader.GetString(3) == "YES";
            }
        }

        // Check each expected table and column
        foreach (var (tableName, columns) in expectedColumns)
        {
            if (!actualColumns.TryGetValue(tableName, out var tableColumns))
            {
                errors.Add(new SchemaVerificationError(
                    tableName, null,
                    $"Table '{_schemaName}.{tableName}' is missing.",
                    SchemaVerificationErrorKind.MissingTable));
                continue;
            }

            foreach (var (columnName, expectedType) in columns)
            {
                if (!tableColumns.Contains(columnName))
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, columnName,
                        $"Column '{columnName}' is missing from table '{_schemaName}.{tableName}'.",
                        SchemaVerificationErrorKind.MissingColumn));
                }
                else
                {
                    var actualType = actualColumnTypes[tableName][columnName];
                    if (!string.Equals(actualType, expectedType, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new SchemaVerificationError(
                            tableName, columnName,
                            $"Column '{columnName}' in '{_schemaName}.{tableName}' has type '{actualType}', expected '{expectedType}'.",
                            SchemaVerificationErrorKind.WrongType));
                    }

                    SchemaVerification.VerifyNullability(
                        errors, tableName, columnName, actualColumnNullability[tableName][columnName]);
                }
            }
        }

        await using var keyCmd = dataSource.CreateCommand(
            """
            SELECT tbl.relname, key.conname,
                   CASE key.contype WHEN 'p' THEN 'PRIMARY KEY' ELSE 'UNIQUE' END,
                   attr.attname, cols.ordinality::integer
            FROM pg_catalog.pg_constraint AS key
            JOIN pg_catalog.pg_class AS tbl ON tbl.oid = key.conrelid
            JOIN pg_catalog.pg_namespace AS ns ON ns.oid = tbl.relnamespace
            CROSS JOIN LATERAL unnest(key.conkey) WITH ORDINALITY AS cols(attnum, ordinality)
            JOIN pg_catalog.pg_attribute AS attr ON attr.attrelid = tbl.oid AND attr.attnum = cols.attnum
            WHERE ns.nspname = $1 AND key.contype IN ('p', 'u')
            ORDER BY tbl.relname, key.conname, cols.ordinality
            """);
        _ = keyCmd.Parameters.AddWithValue(_schemaName);
        Log.ExecutingSql(logger, keyCmd.CommandText);

        var actualKeys = new List<SchemaVerification.KeyColumn>();
        await using (var reader = await keyCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                actualKeys.Add(new SchemaVerification.KeyColumn(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2) == "PRIMARY KEY",
                    reader.GetString(3), reader.GetInt32(4)));
            }
        }

        SchemaVerification.VerifyKeys(errors, actualColumns, actualKeys);

        // Check expected indexes
        var expectedIndexes = new (string Table, string Index, string Definition)[]
        {
            ("entities", "entities_expires_at_index", "entities_expires_at"),
            ("entities", "entities_created_at_index", "entities_created_at"),
            ("entities", "entities_last_updated_at_index", "entities_last_updated_at"),
            ("entity_keys", "entity_keys_entity_type_id_entity_id_index", "entity_keys_entity_type_id_entity_id"),
            ("search_values", "search_values_string_value_index", "search_values_string_value"),
            ("search_values", "search_values_number_value_index", "search_values_number_value"),
            ("search_values", "search_values_datetime_value_index", "search_values_datetime_value"),
            ("search_values", "search_values_boolean_value_index", "search_values_boolean_value"),
            ("search_values", "search_values_array_string_value_index", "search_values_array_string_value"),
            ("search_values", "search_values_array_number_value_index", "search_values_array_number_value"),
            ("search_values", "search_values_array_datetime_value_index", "search_values_array_datetime_value"),
            ("search_values", "search_values_array_boolean_value_index", "search_values_array_boolean_value"),
            ("search_values", "search_values_guid_value_index", "search_values_guid_value"),
            ("search_values", "search_values_array_guid_value_index", "search_values_array_guid_value"),
            ("entity_links", "entity_links_right_entity_index", "entity_links_right_entity"),
            ("entity_links", "entity_links_left_fk_index", "entity_links_left_fk"),
            ("entity_links", "entity_links_right_fk_index", "entity_links_right_fk"),
            ("outbox_subscriber_queue", "outbox_subscriber_queue_subscriber_index", "outbox_subscriber_queue_subscriber"),
            ("outbox_subscriber_queue", "outbox_subscriber_queue_pool_id_index", "outbox_subscriber_queue_pool_id"),
        };

        await using var idxCmd = dataSource.CreateCommand(
            """
            SELECT tbl.relname, idx.relname, attr.attname, cols.ordinality::integer
            FROM pg_catalog.pg_index AS i
            JOIN pg_catalog.pg_class AS tbl ON tbl.oid = i.indrelid
            JOIN pg_catalog.pg_class AS idx ON idx.oid = i.indexrelid
            JOIN pg_catalog.pg_namespace AS ns ON ns.oid = tbl.relnamespace
            LEFT JOIN LATERAL unnest(i.indkey::smallint[]) WITH ORDINALITY AS cols(attnum, ordinality)
                ON cols.ordinality <= i.indnkeyatts
            LEFT JOIN pg_catalog.pg_attribute AS attr ON attr.attrelid = tbl.oid AND attr.attnum = cols.attnum
            WHERE ns.nspname = $1 AND i.indisvalid
            ORDER BY tbl.relname, idx.relname, cols.ordinality
            """);
        _ = idxCmd.Parameters.AddWithValue(_schemaName);
        Log.ExecutingSql(logger, idxCmd.CommandText);

        var actualIndexes = new List<SchemaVerification.IndexColumn>();
        await using (var reader = await idxCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                actualIndexes.Add(new SchemaVerification.IndexColumn(
                    reader.GetString(0), reader.GetString(1),
                    await reader.IsDBNullAsync(2, ct) ? null : reader.GetString(2),
                    await reader.IsDBNullAsync(3, ct) ? 0 : reader.GetInt32(3)));
            }
        }

        SchemaVerification.VerifyIndexes(errors, actualColumns, expectedIndexes, actualIndexes, (_, indexName) =>
            new SchemaVerificationError(
                indexName, null,
                $"Index '{indexName}' is missing from schema '{_schemaName}'.",
                SchemaVerificationErrorKind.MissingIndex));

        // Check foreign keys. entity_keys/search_values are checked by the shared
        // SchemaVerification.VerifyForeignKeys (from table, to table) pair logic.
        // entity_links has two FKs to the same table, so a (from table, to table) pair can't tell
        // them apart: they are checked below by constraint name instead, plus the full ordered
        // source-to-target column mapping, the referenced table/schema and the delete rule
        // (confdeltype: 'a' = NO ACTION for both, since a foreign key's ON DELETE action belongs
        // to the constraint, not the row — Restrict is enforced per-row via restrict_delete).
        var expectedNamedForeignKeys = new (string Table, string ConstraintName, (string Source, string Target)[] Columns)[]
        {
            ("entity_links", "fk_entity_links_left",
                [("pool_id", "pool_id"), ("left_entity_type_id", "entity_type_id"), ("left_entity_id", "entity_id")]),
            ("entity_links", "fk_entity_links_right",
                [("pool_id", "pool_id"), ("right_entity_type_id", "entity_type_id"), ("right_entity_id", "entity_id")]),
        };

        await using var fkCmd = dataSource.CreateCommand(
            """
            SELECT source.relname, fk.conname, target.relname, target_ns.nspname,
                   source_col.attname, target_col.attname,
                   CASE fk.confdeltype
                       WHEN 'c' THEN 'CASCADE'
                       WHEN 'a' THEN 'NO ACTION'
                       WHEN 'r' THEN 'RESTRICT'
                       WHEN 'n' THEN 'SET NULL'
                       WHEN 'd' THEN 'SET DEFAULT'
                   END,
                   cols.ordinality::integer
            FROM pg_catalog.pg_constraint AS fk
            JOIN pg_catalog.pg_class AS source ON source.oid = fk.conrelid
            JOIN pg_catalog.pg_namespace AS source_ns ON source_ns.oid = source.relnamespace
            JOIN pg_catalog.pg_class AS target ON target.oid = fk.confrelid
            JOIN pg_catalog.pg_namespace AS target_ns ON target_ns.oid = target.relnamespace
            CROSS JOIN LATERAL unnest(fk.conkey, fk.confkey) WITH ORDINALITY AS cols(source_num, target_num, ordinality)
            JOIN pg_catalog.pg_attribute AS source_col ON source_col.attrelid = source.oid AND source_col.attnum = cols.source_num
            JOIN pg_catalog.pg_attribute AS target_col ON target_col.attrelid = target.oid AND target_col.attnum = cols.target_num
            WHERE fk.contype = 'f' AND source_ns.nspname = $1
            ORDER BY source.relname, fk.conname, cols.ordinality
            """);
        _ = fkCmd.Parameters.AddWithValue(_schemaName);
        Log.ExecutingSql(logger, fkCmd.CommandText);

        var actualForeignKeys = new List<SchemaVerification.ForeignKeyColumn>();
        await using (var reader = await fkCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                actualForeignKeys.Add(new SchemaVerification.ForeignKeyColumn(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    string.Equals(reader.GetString(3), _schemaName, StringComparison.Ordinal),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt32(7)));
            }
        }

        SchemaVerification.VerifyForeignKeys(
            errors, actualColumns, actualForeignKeys, referencedTableNamesAreCaseSensitive: true);

        var entitiesTableName = SchemaVerification.FindName(actualColumns.Keys, "entities");
        foreach (var (fromTable, constraintName, expectedFkColumns) in expectedNamedForeignKeys)
        {
            var group = actualForeignKeys
                .Where(fk => string.Equals(fk.Table, fromTable, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(fk.Constraint, constraintName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(fk => fk.Position)
                .ToArray();

            var matches = group.Length == expectedFkColumns.Length &&
                group[0].IsSameSchema &&
                entitiesTableName is not null &&
                string.Equals(group[0].ReferencedTable, entitiesTableName, StringComparison.Ordinal) &&
                string.Equals(group[0].DeleteRule, "NO ACTION", StringComparison.OrdinalIgnoreCase) &&
                expectedFkColumns.All(expected => group.Any(fk =>
                    string.Equals(fk.Column, expected.Source, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(fk.ReferencedColumn, expected.Target, StringComparison.OrdinalIgnoreCase)));

            if (!matches)
            {
                errors.Add(new SchemaVerificationError(
                    fromTable, null,
                    $"Foreign key '{constraintName}' on '{_schemaName}.{fromTable}' is missing, references the wrong table/schema or columns, or has the wrong ON DELETE action.",
                    SchemaVerificationErrorKind.MissingForeignKey));
            }
        }

        return new SchemaVerificationResult(errors);
    }

    string IStorageInstanceSchema.BuildMigrationScript(DatabaseSchemaVersion fromVersion)
    {
        var scripts = MigrationScriptLoader.GetScripts(typeof(PostgreSqlStorageEngine).Assembly, fromVersion, _schemaName);
        var sb = new StringBuilder();
        foreach (var (_, sql) in scripts)
        {
            _ = sb.AppendLine(sql);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns whether the database error says that a table does not exist: PostgreSQL SQLSTATE 42P01 (undefined_table).
    /// </summary>
    private static bool IsMissingStorageTable(PostgresException ex) => string.Equals(ex.SqlState, PostgresErrorCodes.UndefinedTable, StringComparison.Ordinal);

    /// <summary>
    /// Returns whether an exception is a PostgreSQL deadlock (SQLSTATE 40P01), worth retrying the
    /// whole operation from a fresh connection and transaction.
    /// </summary>
    private static bool IsDeadlock(Exception ex) =>
        ex is PostgresException pgEx && string.Equals(pgEx.SqlState, PostgresErrorCodes.DeadlockDetected, StringComparison.Ordinal);

    /// <summary>
    /// Qualified table names <see cref="SqlWhereClauseBuilder"/> needs to compile a link filter, matching the
    /// qualified names used elsewhere in this engine.
    /// </summary>
    private StorageTableNames LinkFilterTableNames => new(_entities, _entityLinks);

    /// <summary>
    /// Creates a new entity in the store.
    /// </summary>
    async Task<CreateResult> IPartitionedStorage.CreateAsync<TDso>(
        Storage.UuidV7 id,
        TDso dso,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
    {
        try
        {
            var createOp = CreateOperation.For(id, dso, keys, searchFieldCollection, expiration);

            await using var cnn = await dataSource.OpenConnectionAsync(ct);
            await using var tx = await cnn.BeginTransactionAsync(ct);

            var outcome = await ExecuteCreateCoreAsync(cnn, tx, createOp, ct);

            if (outcome == OperationOutcome.Success)
            {
                if (outboxEvents is { Count: > 0 })
                {
                    await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
                }
                await tx.CommitAsync(ct);
            }

            return outcome switch
            {
                OperationOutcome.Success => CreateResult.Success,
                OperationOutcome.AlreadyExists => CreateResult.AlreadyExists,
                OperationOutcome.KeyConflict => CreateResult.KeyConflict,
                _ => throw new InvalidOperationException($"Unexpected outcome from create operation: {outcome}")
            };
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

            await using var cmd = dataSource.CreateCommand(
                $"""
                 SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at
                 FROM {_entities} v
                 WHERE v.entity_type_id = $1 AND v.entity_id = $2 AND v.pool_id = $3
                 """);

            _ = cmd.Parameters.AddWithValue((int)entityType.Id);
            _ = cmd.Parameters.AddWithValue(id.Value);
            _ = cmd.Parameters.AddWithValue(PoolId.Value);
            Log.ExecutingSql(logger, cmd.CommandText);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return StoreGetResult.NotFound();
            }

            var entityId = reader.GetGuid(0);
            var jsonValue = reader.GetString(1);
            var dsoTypeVersion = reader.GetInt32(2);
            var valueVersion = reader.GetInt32(3);
            var created = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
            var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);

            var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
            var dsoType = dataStorageTypeRegistry.Get(version);
            var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

            return StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated);
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

            var keyGuid = key.Value;
            var keyTypeId = (int)key.DskVersion.KeyType.Id;
            var keyTypeVersion = (int)key.DskVersion.SchemaVersion;

            await using var cmd = dataSource.CreateCommand(
                $"""
                 SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at
                 FROM {_entityKeys} i
                 INNER JOIN {_entities} v ON i.entity_type_id = v.entity_type_id AND i.entity_id = v.entity_id
                 WHERE i.entity_type_id = @entity_type_id
                     AND i.key_type_id = @key_type_id
                     AND i.key_type_version = @key_type_version
                     AND i.key_value = @key_value
                     AND i.pool_id = @pool_id
                     AND v.pool_id = @pool_id
                 """);

            _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
            _ = cmd.Parameters.AddWithValue("@key_type_id", keyTypeId);
            _ = cmd.Parameters.AddWithValue("@key_type_version", keyTypeVersion);
            _ = cmd.Parameters.AddWithValue("@key_value", keyGuid);
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

            Log.ExecutingSql(logger, cmd.CommandText);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return StoreGetResult.NotFound();
            }

            var entityId = reader.GetGuid(0);
            var jsonValue = reader.GetString(1);
            var dsoTypeVersion = reader.GetInt32(2);
            var valueVersion = reader.GetInt32(3);
            var created = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
            var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);

            var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
            var dsoType = dataStorageTypeRegistry.Get(version);
            var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

            return StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated);
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

            var idArray = ids.Select(id => id.Value).ToArray();

            await using var cmd = dataSource.CreateCommand(
                $"""
                 SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at
                 FROM {_entities} v
                 WHERE v.entity_type_id = @entityTypeId AND v.entity_id = ANY(@ids) AND v.pool_id = @poolId
                 """);

            _ = cmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
            _ = cmd.Parameters.Add(new NpgsqlParameter("@ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = idArray });
            _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);

            Log.ExecutingSql(logger, cmd.CommandText);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var results = new List<StoreGetResult>();
            while (await reader.ReadAsync(ct))
            {
                var entityId = reader.GetGuid(0);
                var jsonValue = reader.GetString(1);
                var dsoTypeVersion = reader.GetInt32(2);
                var valueVersion = reader.GetInt32(3);
                var created = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
                var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);

                var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
                var dsoType = dataStorageTypeRegistry.Get(version);
                var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;


                results.Add(StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated));
            }

            return results;
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    /// <summary>
    /// Updates an existing entity in the store.
    /// </summary>
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

            await using var cnn = await dataSource.OpenConnectionAsync(ct);
            await using var tx = await cnn.BeginTransactionAsync(ct);

            var outcome = await ExecuteUpdateCoreAsync(cnn, tx, updateOp, ct);

            if (outcome == OperationOutcome.Success)
            {
                if (outboxEvents is { Count: > 0 })
                {
                    await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
                }
                await tx.CommitAsync(ct);
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
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

                    await using var cnn = await dataSource.OpenConnectionAsync(ct);
                    await using var tx = await cnn.BeginTransactionAsync(ct);

                    var (outcome, entityDeleted) = await ExecuteDeleteCoreAsync(cnn, tx, deleteOp, ct);

                    if (outcome == OperationOutcome.StillLinked)
                    {
                        return DeleteResult.StillLinked;
                    }

                    if (entityDeleted && outboxEvents is { Count: > 0 })
                    {
                        await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
                    }

                    await tx.CommitAsync(ct);
                    return DeleteResult.Success;
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

                    await using var cnn = await dataSource.OpenConnectionAsync(ct);
                    await using var tx = await cnn.BeginTransactionAsync(ct);

                    var (outcome, entityDeleted) = await ExecuteDeleteCoreAsync(cnn, tx, deleteOp, ct);

                    if (outcome == OperationOutcome.StillLinked)
                    {
                        return DeleteResult.StillLinked;
                    }

                    if (entityDeleted && outboxEvents is { Count: > 0 })
                    {
                        await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
                    }

                    await tx.CommitAsync(ct);
                    return DeleteResult.Success;
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private int AddInserts(StringBuilder builder,
        NpgsqlCommand cmd,
        IReadOnlyCollection<DataStorageKey> keys)
    {
        if (keys.Count == 0)
        {
            return 0;
        }

        var keyTypeIds = new int[keys.Count];
        var keyTypeNames = new string[keys.Count];
        var keyValues = new Guid[keys.Count];
        var keyJsons = new string?[keys.Count];
        var keyTypeVersions = new int[keys.Count];

        var i = 0;
        foreach (var key in keys)
        {
            keyTypeIds[i] = (int)key.DskVersion.KeyType.Id;
            keyTypeNames[i] = key.DskVersion.KeyType.Name;
            keyValues[i] = key.Value;
            keyJsons[i] = key.KeyJsonValue;
            keyTypeVersions[i] = (int)key.DskVersion.SchemaVersion;
            ++i;
        }

        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
             INSERT INTO {_entityKeys} (entity_type_id, key_type_id, key_type_name, key_value, key_json, key_type_version, entity_id, pool_id)
             SELECT @entity_type_id, key_type_id, key_type_name, key_value, key_json, key_type_version, @entity_id, @pool_id
             FROM UNNEST(@key_type_ids, @key_type_names, @key_values, @key_jsons, @key_type_versions)
               AS t(key_type_id, key_type_name, key_value, key_json, key_type_version);
             """);

        var keyTypeIdsParam = new NpgsqlParameter("@key_type_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer);
        keyTypeIdsParam.Value = keyTypeIds;
        _ = cmd.Parameters.Add(keyTypeIdsParam);

        var keyTypeNamesParam = new NpgsqlParameter("@key_type_names", NpgsqlDbType.Array | NpgsqlDbType.Text);
        keyTypeNamesParam.Value = keyTypeNames;
        _ = cmd.Parameters.Add(keyTypeNamesParam);

        var keyValuesParam = new NpgsqlParameter("@key_values", NpgsqlDbType.Array | NpgsqlDbType.Uuid);
        keyValuesParam.Value = keyValues;
        _ = cmd.Parameters.Add(keyValuesParam);

        var keyJsonsParam = new NpgsqlParameter("@key_jsons", NpgsqlDbType.Array | NpgsqlDbType.Jsonb);
        keyJsonsParam.Value = keyJsons;
        _ = cmd.Parameters.Add(keyJsonsParam);

        var keyTypeVersionsParam = new NpgsqlParameter("@key_type_versions", NpgsqlDbType.Array | NpgsqlDbType.Integer);
        keyTypeVersionsParam.Value = keyTypeVersions;
        _ = cmd.Parameters.Add(keyTypeVersionsParam);

        return keys.Count;
    }

    private int AddSearchFieldInserts(
        StringBuilder builder,
        NpgsqlCommand cmd,
        SearchFieldCollection? searchFields)
    {
        if (searchFields is null || searchFields.Count == 0)
        {
            return 0;
        }

        var fieldPaths = new Guid[searchFields.Count];
        var fieldPathTexts = new string[searchFields.Count];
        var itemIndexes = new int[searchFields.Count];
        var stringValues = new string?[searchFields.Count];
        var numberValues = new decimal?[searchFields.Count];
        var datetimeValues = new DateTime?[searchFields.Count];
        var booleanValues = new bool?[searchFields.Count];
        var guidValues = new Guid?[searchFields.Count];

        var i = 0;
        foreach (var field in searchFields)
        {
            fieldPaths[i] = field.FieldPathId;
            fieldPathTexts[i] = field.FieldPath;
            itemIndexes[i] = field.ItemIndex ?? -1;
            stringValues[i] = field.StringValue;
            numberValues[i] = field.NumberValue;
            // Convert DateTimeOffset to UTC timestamp for PostgreSQL
            datetimeValues[i] = field.DateTimeValue?.UtcDateTime;
            booleanValues[i] = field.BooleanValue;
            guidValues[i] = field.GuidValue;
            ++i;
        }

        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
             INSERT INTO {_searchValues} (entity_type_id, entity_id, field_path, field_path_text, item_index, string_value, number_value, datetime_value, boolean_value, guid_value, pool_id)
             SELECT @entity_type_id, @entity_id, field_path, field_path_text, item_index, string_value, number_value, datetime_value, boolean_value, guid_value, @pool_id
             FROM UNNEST(@sf_field_paths, @sf_field_path_texts, @sf_item_indexes, @sf_string_values, @sf_number_values, @sf_datetime_values, @sf_boolean_values, @sf_guid_values)
               AS t(field_path, field_path_text, item_index, string_value, number_value, datetime_value, boolean_value, guid_value);
             """);

        var sfFieldPathsParam = new NpgsqlParameter("@sf_field_paths", NpgsqlDbType.Array | NpgsqlDbType.Uuid);
        sfFieldPathsParam.Value = fieldPaths;
        _ = cmd.Parameters.Add(sfFieldPathsParam);

        var sfFieldPathTextsParam = new NpgsqlParameter("@sf_field_path_texts", NpgsqlDbType.Array | NpgsqlDbType.Text);
        sfFieldPathTextsParam.Value = fieldPathTexts;
        _ = cmd.Parameters.Add(sfFieldPathTextsParam);

        var sfItemIndexesParam = new NpgsqlParameter("@sf_item_indexes", NpgsqlDbType.Array | NpgsqlDbType.Integer);
        sfItemIndexesParam.Value = itemIndexes;
        _ = cmd.Parameters.Add(sfItemIndexesParam);

        var sfStringValuesParam = new NpgsqlParameter("@sf_string_values", NpgsqlDbType.Array | NpgsqlDbType.Text);
        sfStringValuesParam.Value = stringValues;
        _ = cmd.Parameters.Add(sfStringValuesParam);

        var sfNumberValuesParam = new NpgsqlParameter("@sf_number_values", NpgsqlDbType.Array | NpgsqlDbType.Numeric);
        sfNumberValuesParam.Value = numberValues;
        _ = cmd.Parameters.Add(sfNumberValuesParam);

        var sfDatetimeValuesParam = new NpgsqlParameter("@sf_datetime_values", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz);
        sfDatetimeValuesParam.Value = datetimeValues;
        _ = cmd.Parameters.Add(sfDatetimeValuesParam);

        var sfBooleanValuesParam = new NpgsqlParameter("@sf_boolean_values", NpgsqlDbType.Array | NpgsqlDbType.Boolean);
        sfBooleanValuesParam.Value = booleanValues;
        _ = cmd.Parameters.Add(sfBooleanValuesParam);

        var sfGuidValuesParam = new NpgsqlParameter("@sf_guid_values", NpgsqlDbType.Array | NpgsqlDbType.Uuid);
        sfGuidValuesParam.Value = guidValues;
        _ = cmd.Parameters.Add(sfGuidValuesParam);

        return searchFields.Count;
    }

    /// <summary>
    /// Queries entities with the specified pagination strategy.
    /// </summary>
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

            // Build WHERE clause and ORDER BY clause
            await using var cmd = dataSource.CreateCommand();
            var queryClauses = BuildQueryClauses(cmd, filter, sort, entityType);

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
                pagedOrderBy = $"ORDER BY sort_value {sortDirection} NULLS LAST, entity_id ASC";
                outerOrderBy = $"ORDER BY p.sort_value {sortDirection} NULLS LAST, p.entity_id ASC";
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
                    FROM {_entities} v
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
                    OFFSET @offset LIMIT @limit
                )
                SELECT p.entity_id, p.value, p.dso_type_schema_version, p.value_version, p.created_at, p.last_updated_at, t.total_count
                FROM total t
                LEFT JOIN paged p ON TRUE
                {outerOrderBy}
                """;

            _ = cmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take);

            cmd.CommandText = query;

            Log.ExecutingQuery(logger, query);

            // Execute query and deserialize results.
            // total_count is at column 6 (bigint in PG → Convert.ToInt32).
            // When page is beyond range, LEFT JOIN yields one row with p.* = NULL — skip those.
            var items = new List<MetadataEnvelope>();
            var totalCount = 0;
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    if (totalCount == 0)
                    {
                        totalCount = Convert.ToInt32(reader.GetInt64(6));
                    }

                    // When page is beyond range, p.entity_id is NULL — skip deserializing
                    if (await reader.IsDBNullAsync(0, ct))
                    {
                        continue;
                    }

                    var entityId = reader.GetGuid(0);
                    var jsonValue = reader.GetString(1);
                    var dsoTypeVersion = reader.GetInt32(2);
                    var created = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
                    var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);

                    var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
                    var dsoType = dataStorageTypeRegistry.Get(version);
                    var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                    items.Add(new MetadataEnvelope(item, entityId, reader.GetInt32(3), created, lastUpdated));
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
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    /// <summary>
    /// Queries for specific field values with the specified pagination strategy.
    /// </summary>
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

            // Build WHERE clause and ORDER BY clause
            await using var cmd = dataSource.CreateCommand();
            var queryClauses = BuildQueryClauses(cmd, filter, sort, entityType);

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
                // Use the JOIN and ORDER BY parts directly from queryClauses
                cteJoin = queryClauses.JoinClause;

                // Determine which column to select based on field type
                var sortColumn = GetSortColumnName(sort.Field!);

                // Include sort column and row number to preserve sort order
                cteSelect = $"SELECT v.entity_id, v.created_at, v.last_updated_at, v.value_version, {sortColumn} AS sort_value, ROW_NUMBER() OVER ({queryClauses.OrderByClause}) AS row_num";
            }
            else
            {
                cteSelect = "SELECT v.entity_id, v.created_at, v.last_updated_at, v.value_version, ROW_NUMBER() OVER (ORDER BY v.entity_id ASC) AS row_num";
                cteJoin = "";
            }

            var query = $"""
                WITH all_matches AS (
                    {cteSelect}
                    FROM {_entities} v
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
                    OFFSET @offset LIMIT @limit
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
                LEFT JOIN filtered_ids fi ON TRUE
                LEFT JOIN {_searchValues} field_sv
                  ON fi.entity_id = field_sv.entity_id
                  AND field_sv.entity_type_id = @entity_type_id
                  AND field_sv.pool_id = @pool_id
                  AND field_sv.item_index = -1
                  AND ({fieldConditionsClause})
                ORDER BY fi.row_num, fi.entity_id
                """;

            _ = cmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take);

            cmd.CommandText = query;

            Log.ExecutingQuery(logger, query);

            // Execute query and build projected results
            var resultsById = new Dictionary<Guid, (Dictionary<string, object?> FieldValues, DateTimeOffset Created, DateTimeOffset LastUpdated, int Version)>();
            var orderedIds = new List<Guid>();
            var totalCount = 0;
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    if (totalCount == 0)
                    {
                        // total_count is at column 7: entity_id(0), field_path(1), string_value(2),
                        // number_value(3), datetime_value(4), boolean_value(5), guid_value(6), total_count(7)
                        totalCount = Convert.ToInt32(reader.GetInt64(7));
                    }

                    // When page is beyond range, fi.entity_id is NULL (LEFT JOIN returns no filtered rows)
                    if (await reader.IsDBNullAsync(0, ct))
                    {
                        continue;
                    }

                    var entityId = reader.GetGuid(0);
                    var fieldPath = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);

                    if (!resultsById.TryGetValue(entityId, out var entry))
                    {
                        var fieldValues = new Dictionary<string, object?>();
                        orderedIds.Add(entityId);

                        // Initialize all requested fields as null
                        foreach (var field in fields)
                        {
                            fieldValues[field.Path] = null;
                        }

                        var created = await reader.GetFieldValueAsync<DateTimeOffset>(8, ct);
                        var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(9, ct);
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
                        resultsById[entityId] = entry;
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
                .Select(id => new ProjectedResult(id, resultsById[id].FieldValues))
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
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    /// <summary>
    /// Queries entities with cursor-based pagination.
    /// </summary>
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

        await using var cmd = dataSource.CreateCommand();
        var queryClauses = BuildCursorQueryClauses(cmd, filter, sort, position, traversal, entityType);

        // Build main query - fetch PageSize + 1 to determine if there are more pages
        // We select the sort value to use in the next token
        var query = $"""
            SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at, {queryClauses.SortColumnName}
            FROM {_entities} v
            {queryClauses.JoinClause}
            WHERE v.entity_type_id = @entity_type_id
              AND v.pool_id = @pool_id
              AND ({queryClauses.WhereClause})
              {queryClauses.SeekClause}
            {queryClauses.OrderByClause}
            LIMIT @limit
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
                var entityId = reader.GetGuid(0);
                var jsonValue = reader.GetString(1);
                var dsoTypeVersion = reader.GetInt32(2);
                var created = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
                var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);

                var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
                var dsoType = dataStorageTypeRegistry.Get(version);
                var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                var envelope = new MetadataEnvelope(item, entityId, reader.GetInt32(3), created, lastUpdated);

                // Get the sort value from column 6
                var sortValue = sort.IsEmpty ? null : await ReadSortValueAsync(reader, sort.Field!, 6, ct);
                items.Add((entityId, envelope, sortValue));
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

        var resultItems = assembled.ItemsInRequestedOrder.Select(x => x.Item).ToList();
        return new QueryResult<MetadataEnvelope>
        {
            Items = resultItems,
            NextToken = assembled.Next,
            PreviousToken = assembled.Previous,
            HasMoreData = assembled.HasMoreData,
            HasPreviousData = assembled.HasPreviousData
        };
    }

    /// <summary>
    /// Queries for specific field values with cursor-based pagination.
    /// </summary>
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

        await using var cmd = dataSource.CreateCommand();
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
                FROM {_entities} v
                {queryClauses.JoinClause}
                WHERE v.entity_type_id = @entity_type_id
                  AND v.pool_id = @pool_id
                  AND ({queryClauses.WhereClause})
                  {queryClauses.SeekClause}
                {queryClauses.OrderByClause}
                LIMIT @limit
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
            LEFT JOIN {_searchValues} field_sv
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
        var resultsById = new Dictionary<Guid, (Dictionary<string, object?> FieldValues, object? SortValue, DateTimeOffset Created, DateTimeOffset LastUpdated, int Version)>();
        var orderedIds = new List<Guid>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = reader.GetGuid(0);
                var fieldPath = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);

                if (!resultsById.TryGetValue(entityId, out var entry))
                {
                    var fieldValues = new Dictionary<string, object?>();

                    // Initialize all requested fields as null
                    foreach (var field in fields)
                    {
                        fieldValues[field.Path] = null;
                    }

                    var sortValue = sort.IsEmpty ? null : await ReadSortValueAsync(reader, sort.Field!, 7, ct);
                    var created = await reader.GetFieldValueAsync<DateTimeOffset>(8, ct);
                    var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(9, ct);
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

                    entry = (fieldValues, sortValue, created, lastUpdated, version);
                    resultsById[entityId] = entry;
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

        var itemsList = orderedIds.Select(id => (Id: id, resultsById[id])).ToList();

        var assembled = CursorPageAssembler.Assemble(
            itemsList,
            pageSize,
            traversal,
            position,
            entityType,
            sort,
            row => (row.Id, row.Item2.SortValue));

        var items = assembled.ItemsInRequestedOrder
            .Select(item => new ProjectedResult(item.Id, item.Item2.FieldValues))
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

    /// <summary>
    /// Builds the WHERE clause, ORDER BY clause, and calculates the offset for a query.
    /// </summary>
    private QueryClauses BuildQueryClauses(
        NpgsqlCommand cmd,
        IQueryExpression filter,
        SortParameter sort,
        EntityType entityType)
    {
        // Build WHERE clause
        var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect, LinkFilterTableNames, entityType);
        var whereClause = whereBuilder.BuildWhereClause(filter);

        // Build JOIN and ORDER BY clauses
        string joinClause;
        string orderByClause;
        if (!sort.IsEmpty)
        {
            var sortFieldPath = sort.Field!.Path;
            var sortDirection = sort.Direction == SortDirection.Ascending ? "ASC" : "DESC";

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
                joinClause = $"""
                    LEFT JOIN {_searchValues} sort_sv
                      ON v.entity_type_id = sort_sv.entity_type_id
                      AND v.entity_id = sort_sv.entity_id
                      AND v.pool_id = sort_sv.pool_id
                      AND sort_sv.field_path = @sort_field_path
                      AND sort_sv.item_index = -1
                    """;

                _ = cmd.Parameters.AddWithValue("@sort_field_path", DeterministicGuidGenerator.Create(sortFieldPath.ToUpperInvariant()));
            }

            orderByClause = $"""
                ORDER BY
                  {sortColumn} {sortDirection} NULLS LAST,
                  v.entity_id ASC
                """;
        }
        else
        {
            joinClause = "";
            orderByClause = "ORDER BY v.entity_id ASC";
        }

        return new QueryClauses(whereClause, joinClause, orderByClause);
    }

    /// <summary>
    /// Builds the WHERE clause, JOIN clause, ORDER BY clause, and seek clause for cursor-based pagination.
    /// </summary>
    private CursorQueryClauses BuildCursorQueryClauses(
        NpgsqlCommand cmd,
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
        NpgsqlCommand cmd,
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
                LEFT JOIN {_searchValues} sort_sv
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
    /// for a decoded cursor key position, using the same per-kind conversions as insertion. Binds
    /// the parameter type explicitly where PostgreSQL can't infer it from the predicate alone.
    /// </summary>
    private static void BindCursorPositionParameters(NpgsqlCommand cmd, CursorPosition position, string? sortColumn)
    {
        if (position.Kind != CursorPositionKind.Key)
        {
            return;
        }

        _ = cmd.Parameters.AddWithValue("@last_id", position.Id);

        if (sortColumn is null || position.ValueKind == CursorValueKind.Null)
        {
            return;
        }

        var param = position.Value switch
        {
            string s => new NpgsqlParameter("@last_sort_value", NpgsqlDbType.Text) { Value = s },
            decimal d => new NpgsqlParameter("@last_sort_value", NpgsqlDbType.Numeric) { Value = d },
            DateTimeOffset dto => new NpgsqlParameter("@last_sort_value", NpgsqlDbType.TimestampTz) { Value = dto.UtcDateTime },
            bool b => new NpgsqlParameter("@last_sort_value", NpgsqlDbType.Boolean) { Value = b },
            Guid g => new NpgsqlParameter("@last_sort_value", NpgsqlDbType.Uuid) { Value = g },
            _ => throw new InvalidOperationException($"Unsupported cursor sort value type: {position.Value!.GetType().Name}")
        };
        _ = cmd.Parameters.Add(param);
    }

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

        // Fallback — should not happen since TokenValue is checked before calling this
        return (0, DataRangeSize.Default.Value);
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
    private static async Task<object?> ReadFieldValueAsync(NpgsqlDataReader reader, FieldType fieldType, int stringValueColumnIndex, Ct ct)
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

#pragma warning disable CA1849 // GetFieldValue<T> cannot be awaited inside a switch expression
        return fieldType switch
        {
            FieldType.String => reader.GetString(columnIndex),
            FieldType.Number => reader.GetDecimal(columnIndex),
            FieldType.DateTime => reader.GetFieldValue<DateTimeOffset>(columnIndex),
            FieldType.Boolean => reader.GetBoolean(columnIndex),
            FieldType.Guid => reader.GetGuid(columnIndex),
            _ => throw new InvalidOperationException($"Unsupported field type: {fieldType}")
        };
#pragma warning restore CA1849
    }

    /// <summary>
    /// Reads a sort value from a database reader for the specified field type.
    /// </summary>
    private static async Task<object?> ReadSortValueAsync(NpgsqlDataReader reader, Field sortField, int columnIndex, Ct ct)
    {
        if (await reader.IsDBNullAsync(columnIndex, ct))
        {
            return null;
        }

#pragma warning disable CA1849 // GetFieldValue<T> cannot be awaited inside a switch expression
        return sortField switch
        {
            StringField => reader.GetString(columnIndex),
            NumberField => reader.GetDecimal(columnIndex),
            DateTimeField => reader.GetFieldValue<DateTimeOffset>(columnIndex),
            BooleanField => reader.GetBoolean(columnIndex),
            GuidField or ExactMatchField => reader.GetGuid(columnIndex),
            _ => throw new InvalidOperationException($"Unsupported field type for sorting: {sortField.GetType().Name}")
        };
#pragma warning restore CA1849
    }

    private sealed record QueryClauses(string WhereClause, string JoinClause, string OrderByClause);

    private sealed record CursorQueryClauses(
        string WhereClause,
        string JoinClause,
        string OrderByClause,
        string SeekClause,
        string SortColumnName);

    private sealed record SchemaComment(uint Version);

    /// <inheritdoc/>
    async Task<LinkResult> IPartitionedStorage.LinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            return await DeadlockRetry.RunAsync(
                async _ =>
                {
                    await using var cnn = await dataSource.OpenConnectionAsync(ct);
                    await using var tx = await cnn.BeginTransactionAsync(ct);
                    var outcome = await ExecuteLinkCoreAsync(cnn, tx, LinkOperation.For(definition, leftEntityId, rightEntityId), ct);
                    if (outcome == OperationOutcome.Success)
                    {
                        if (outboxEvents is { Count: > 0 })
                        {
                            await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
                        }
                        await tx.CommitAsync(ct);
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
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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
                    await using var cnn = await dataSource.OpenConnectionAsync(ct);
                    await using var tx = await cnn.BeginTransactionAsync(ct);
                    _ = await ExecuteUnlinkCoreAsync(cnn, tx, UnlinkOperation.For(definition, leftEntityId, rightEntityId), ct);
                    if (outboxEvents is { Count: > 0 })
                    {
                        await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
                    }
                    await tx.CommitAsync(ct);
                    return UnlinkResult.Success;
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private async Task<OperationOutcome> ExecuteLinkCoreAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
        LinkOperation op,
        Ct ct)
    {
        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            INSERT INTO {_entityLinks} (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id, restrict_delete)
            VALUES (@pool_id, @link_type_id, @left_entity_type_id, @left_entity_id, @right_entity_type_id, @right_entity_id, @restrict_delete)
            ON CONFLICT (pool_id, link_type_id, left_entity_id, right_entity_id) DO NOTHING
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@link_type_id", (int)op.Definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_type_id", (int)op.Definition.Left.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", op.LeftEntityId.Value);
        _ = cmd.Parameters.AddWithValue("@right_entity_type_id", (int)op.Definition.Right.Id);
        _ = cmd.Parameters.AddWithValue("@right_entity_id", op.RightEntityId.Value);
        _ = cmd.Parameters.AddWithValue("@restrict_delete", op.Definition.OnRightDelete == LinkDeleteBehavior.Restrict);

        Log.ExecutingSql(logger, cmd.CommandText);

        try
        {
            var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
            return rowsAffected == 0 ? OperationOutcome.AlreadyLinked : OperationOutcome.Success;
        }
        catch (PostgresException ex) when (string.Equals(ex.SqlState, PostgresErrorCodes.ForeignKeyViolation, StringComparison.Ordinal))
        {
            // ON CONFLICT DO NOTHING only covers the primary key, not foreign key violations, so
            // any 23503 reaching here is the left or right entity not existing in this pool with
            // the definition's entity type.
            return OperationOutcome.DoesNotExist;
        }
    }

    private async Task<OperationOutcome> ExecuteUnlinkCoreAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
        UnlinkOperation op,
        Ct ct)
    {
        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            DELETE FROM {_entityLinks}
            WHERE pool_id = @pool_id
              AND link_type_id = @link_type_id
              AND left_entity_id = @left_entity_id
              AND right_entity_id = @right_entity_id
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@link_type_id", (int)op.Definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", op.LeftEntityId.Value);
        _ = cmd.Parameters.AddWithValue("@right_entity_id", op.RightEntityId.Value);

        Log.ExecutingSql(logger, cmd.CommandText);

        _ = await cmd.ExecuteNonQueryAsync(ct);
        return OperationOutcome.Success;
    }

    private async Task ExecuteOutboxInsertBatchCoreAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
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

        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;

        var valueRows = new List<string>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var (evt, subscription) = rows[i];
            valueRows.Add($"(@message_id{i}, @event_id{i}, @timestamp{i}, @event_name{i}, @subject_id{i}, @entity_type_id{i}, @entity_type_name{i}, @pool_id, @payload{i}::jsonb, @subscriber_name{i}, @dso_type_schema_version{i})");
            _ = cmd.Parameters.AddWithValue($"@message_id{i}", Guid.CreateVersion7());
            _ = cmd.Parameters.AddWithValue($"@event_id{i}", evt.Id.Value);
            _ = cmd.Parameters.Add(new NpgsqlParameter($"@timestamp{i}", NpgsqlDbType.TimestampTz) { Value = evt.Timestamp });
            _ = cmd.Parameters.AddWithValue($"@event_name{i}", evt.EventName.ToString());
            _ = cmd.Parameters.AddWithValue($"@subject_id{i}", evt.SubjectId.Value);
            _ = cmd.Parameters.AddWithValue($"@entity_type_id{i}", evt.EntityTypeId);
            _ = cmd.Parameters.AddWithValue($"@entity_type_name{i}", evt.EntityTypeName);
            _ = cmd.Parameters.Add(new NpgsqlParameter($"@payload{i}", NpgsqlDbType.Jsonb) { Value = evt.Payload });
            _ = cmd.Parameters.AddWithValue($"@subscriber_name{i}", subscription.SubscriberName.ToString());
            _ = cmd.Parameters.Add(new NpgsqlParameter($"@dso_type_schema_version{i}", NpgsqlDbType.Integer) { Value = evt.DsoTypeSchemaVersion.HasValue ? evt.DsoTypeSchemaVersion.Value : DBNull.Value });
        }
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        cmd.CommandText = $"""
            INSERT INTO {_outboxSubscriptionQueue}
            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version)
            VALUES
            {string.Join(",\n            ", valueRows)}
            """;

        Log.ExecutingSql(logger, cmd.CommandText);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Executes multiple operations atomically in a single transaction.
    /// </summary>
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
                    await using var connection = await dataSource.OpenConnectionAsync(ct);
                    await using var transaction = await connection.BeginTransactionAsync(ct);

                    var results = new List<OperationResult>();

                    for (var i = 0; i < operations.Count; i++)
                    {
                        var outcome = operations[i] switch
                        {
                            CreateOperation create => await ExecuteCreateCoreAsync(connection, transaction, create, ct),
                            UpdateOperation update => await ExecuteUpdateCoreAsync(connection, transaction, update, ct),
                            DeleteOperation delete => (await ExecuteDeleteCoreAsync(connection, transaction, delete, ct)).Outcome,
                            LinkOperation link => await ExecuteLinkCoreAsync(connection, transaction, link, ct),
                            UnlinkOperation unlink => await ExecuteUnlinkCoreAsync(connection, transaction, unlink, ct),
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
                        await ExecuteOutboxInsertBatchCoreAsync(connection, transaction, outboxEvents, ct);
                    }

                    await transaction.CommitAsync(ct);
                    return new BatchResult(true, results);
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<OutboxEventsPage> ICrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, Ct ct)
    {
        try
        {
            await using var cnn = await dataSource.OpenConnectionAsync(ct);
            await using var cmd = cnn.CreateCommand();
            cmd.CommandText = $"""
                SELECT sequence_number, message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version
                FROM {_outboxSubscriptionQueue}
                WHERE subscriber_name = @subscriber_name
                ORDER BY sequence_number ASC
                LIMIT @limit
                """;
            _ = cmd.Parameters.AddWithValue("@subscriber_name", subscriptionName.ToString());
            _ = cmd.Parameters.AddWithValue("@limit", count + 1);

            Log.ExecutingSql(logger, cmd.CommandText);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var events = new List<PersistedOutboxEvent>();
            while (await reader.ReadAsync(ct))
            {
                var timestamp = await reader.GetFieldValueAsync<DateTimeOffset>(3, ct);
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
                    Timestamp = timestamp,
                    EventName = OutboxEventName.Create(reader.GetString(4)),
                    SubjectId = Storage.UuidV7.From(reader.GetGuid(5)),
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
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

            await using var cnn = await dataSource.OpenConnectionAsync(ct);

            const int MaxBatchSize = 1000;
            for (var offset = 0; offset < ids.Count; offset += MaxBatchSize)
            {
                var chunk = ids.Skip(offset).Take(MaxBatchSize).ToArray();

                await using var cmd = cnn.CreateCommand();
                cmd.CommandText = $"""
                    DELETE FROM {_outboxSubscriptionQueue}
                    WHERE message_id = ANY(@ids)
                    """;
                var guidIds = chunk.Select(id => id.Value).ToArray();
                _ = cmd.Parameters.Add(new NpgsqlParameter("@ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = guidIds });

                Log.ExecutingSql(logger, cmd.CommandText);
                _ = await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private async Task<OperationOutcome> ExecuteCreateCoreAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
        CreateOperation op,
        Ct ct)
    {
        var dsoVersion = op.DsoVersion;
        var dsoTypeId = (int)dsoVersion.EntityType.Id;
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

        var builder = new StringBuilder();

        // Insert the record
        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
             INSERT INTO {_entities} (entity_type_id, entity_type_name, entity_id, value, dso_type_schema_version, value_version, pool_id, expires_at, created_at, last_updated_at)
             VALUES (@entity_type_id, @entity_type_name, @entity_id, @value, @dso_type_schema_version, 1, @pool_id, @expires_at, @now, @now);
             """);

        await using var createCmd = cnn.CreateCommand();
        createCmd.Transaction = tx;
        _ = createCmd.Parameters.AddWithValue("@entity_type_id", dsoTypeId);
        _ = createCmd.Parameters.AddWithValue("@entity_type_name", entityType.Name);
        _ = createCmd.Parameters.AddWithValue("@entity_id", op.Id.Value);
        _ = createCmd.Parameters.AddWithValue("@value", NpgsqlDbType.Jsonb, jsonDso);
        _ = createCmd.Parameters.AddWithValue("@dso_type_schema_version", (int)dsoVersion.SchemaVersion);
        _ = createCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = createCmd.Parameters.AddWithValue("@now", NpgsqlDbType.TimestampTz, timeProvider.GetUtcNow().UtcDateTime);
        if (expiresAt.HasValue)
        {
            _ = createCmd.Parameters.AddWithValue("@expires_at", NpgsqlDbType.TimestampTz, expiresAt.Value.UtcDateTime);
        }
        else
        {
            _ = createCmd.Parameters.AddWithValue("@expires_at", DBNull.Value);
        }

        // Add an insert statement for each key
        _ = AddInserts(builder, createCmd, op.Keys);

        // Add insert statements for search fields
        _ = AddSearchFieldInserts(builder, createCmd, op.SearchFieldCollection);

        createCmd.CommandText = builder.ToString();

        // Create a savepoint before the operation so we can recover from unique violations
        // PostgreSQL aborts the transaction on errors, requiring rollback to a savepoint
        var savepointName = $"create_{Guid.NewGuid()}";
        await tx.SaveAsync(savepointName, ct);

        Log.ExecutingSql(logger, createCmd.CommandText);

        try
        {
            _ = await createCmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (string.Equals(ex.SqlState, PostgresErrorCodes.UniqueViolation,
                                               StringComparison.OrdinalIgnoreCase))
        {
            // Roll back to the savepoint to clear the aborted transaction state
            // This allows subsequent queries to execute within this transaction
            await tx.RollbackAsync(savepointName, ct);

            if (await AlreadyExistsInTransactionAsync(cnn, tx, entityType, op.Id.Value, ct))
            {
                return OperationOutcome.AlreadyExists;
            }

            // One of the keys already exists
            return OperationOutcome.KeyConflict;
        }

        return OperationOutcome.Success;
    }

    private async Task<OperationOutcome> ExecuteUpdateCoreAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
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

        await using var readVersionCmd = cnn.CreateCommand();
        readVersionCmd.Transaction = tx;

        // Read the current version of the entity, locking the row
        readVersionCmd.CommandText =
            $"SELECT value_version FROM {_entities} WHERE entity_type_id = @entity_type_id AND entity_id = @entity_id AND pool_id = @pool_id FOR UPDATE";

        _ = readVersionCmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = readVersionCmd.Parameters.AddWithValue("@entity_id", op.Id.Value);
        _ = readVersionCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        Log.ExecutingSql(logger, readVersionCmd.CommandText);

        var actualEntityVersion = (int?)await readVersionCmd.ExecuteScalarAsync(ct);

        if (actualEntityVersion == null)
        {
            return OperationOutcome.DoesNotExist;
        }

        if (actualEntityVersion != op.ExpectedEntityVersion)
        {
            return OperationOutcome.UnexpectedVersion;
        }

        var builder = new StringBuilder();

        var expiresAtSql = hasExpirationChange
            ? "expires_at = @expires_at,"
            : ""; // Don't change existing expires_at when expiration is null

        // Update the main DSO row, incrementing its version
        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
             UPDATE {_entities}
             SET
                 entity_type_name = @entity_type_name,
                 value = @value,
                 dso_type_schema_version = @dso_type_schema_version,
                 value_version = value_version + 1,
                 {expiresAtSql}
                 last_updated_at = @now
             WHERE
                 entity_type_id = @entity_type_id AND entity_id = @entity_id AND pool_id = @pool_id;
             """);

        // Create the command
        await using var updateCmd = cnn.CreateCommand();
        updateCmd.Transaction = tx;
        _ = updateCmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = updateCmd.Parameters.AddWithValue("@entity_type_name", entityType.Name);
        _ = updateCmd.Parameters.AddWithValue("@entity_id", op.Id.Value);
        _ = updateCmd.Parameters.AddWithValue("@value", NpgsqlDbType.Jsonb, jsonDso);
        _ = updateCmd.Parameters.AddWithValue("@dso_type_schema_version", (int)dsoVersion.SchemaVersion);
        _ = updateCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = updateCmd.Parameters.AddWithValue("@now", NpgsqlDbType.TimestampTz, timeProvider.GetUtcNow().UtcDateTime);
        if (hasExpirationChange)
        {
            if (expiresAt.HasValue)
            {
                _ = updateCmd.Parameters.AddWithValue("@expires_at", NpgsqlDbType.TimestampTz, expiresAt.Value.UtcDateTime);
            }
            else
            {
                _ = updateCmd.Parameters.AddWithValue("@expires_at", DBNull.Value);
            }
        }

        // Delete the existing keys
        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"DELETE FROM {_entityKeys} WHERE entity_type_id = @entity_type_id AND entity_id = @entity_id AND pool_id = @pool_id;");

        // Delete the existing search fields
        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"DELETE FROM {_searchValues} WHERE entity_type_id = @entity_type_id AND entity_id = @entity_id AND pool_id = @pool_id;");

        // re-insert the new keys
        _ = AddInserts(builder, updateCmd, op.Keys);

        // re-insert the new search fields
        _ = AddSearchFieldInserts(builder, updateCmd, op.SearchFieldCollection);

        updateCmd.CommandText = builder.ToString();

        // Create a savepoint before the operation so we can recover from unique violations
        // PostgreSQL aborts the transaction on errors, requiring rollback to a savepoint
        var savepointName = $"update_{Guid.NewGuid()}";
        await tx.SaveAsync(savepointName, ct);

        Log.ExecutingSql(logger, updateCmd.CommandText);

        try
        {
            _ = await updateCmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (string.Equals(ex.SqlState, PostgresErrorCodes.UniqueViolation,
                                               StringComparison.OrdinalIgnoreCase))
        {
            // Roll back to the savepoint to clear the aborted transaction state
            await tx.RollbackAsync(savepointName, ct);
            return OperationOutcome.KeyConflict;
        }

        return OperationOutcome.Success;
    }

    private async Task<(OperationOutcome Outcome, bool EntityDeleted)> ExecuteDeleteCoreAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
        DeleteOperation op,
        Ct ct)
    {
        var entityType = op.EntityType;

        // Resolve the id up front (by-key deletes look it up via entity_keys) so the same id is
        // used both to delete links where this entity is an endpoint and to delete the entity
        // itself. entity_links has no cascading FK (both FKs are NO ACTION): (1) deletes links
        // where the entity is the left side, and right-side links that are not restricted; (2)
        // deletes the entity. If a restricted right-side link remains, step 2's FK check fails and
        // this returns StillLinked. A PostgreSQL error aborts the transaction, so step 2 runs after
        // a savepoint that is rolled back to on failure, letting the re-check/retry run in the same
        // transaction. If step 2 fails but no restricted link is found on re-check (a cascade link
        // was inserted concurrently between the two statements), steps (1) and (2) are retried once.
        var entityId = op.Id?.Value;
        if (entityId is null && op.Key is not null)
        {
            entityId = await ResolveEntityIdByKeyAsync(cnn, tx, entityType, op.Key, ct);
        }

        if (entityId is null)
        {
            return (OperationOutcome.Success, false);
        }

        if (op.Id is not null)
        {
            Log.DeletingDso(logger, entityType, op.Id.Value);
        }

        const int maxAttempts = 2;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await DeleteCascadingLinksCoreAsync(cnn, tx, entityType, entityId.Value, ct);

            const string savepoint = "entity_delete";
            await tx.SaveAsync(savepoint, ct);

            await using var deleteCmd = cnn.CreateCommand();
            deleteCmd.Transaction = tx;
            deleteCmd.CommandText = $"""
                DELETE FROM {_entities}
                WHERE entity_type_id = @entity_type_id AND pool_id = @pool_id AND entity_id = @entity_id
                RETURNING entity_id
                """;
            _ = deleteCmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
            _ = deleteCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = deleteCmd.Parameters.AddWithValue("@entity_id", entityId.Value);

            Log.ExecutingSql(logger, deleteCmd.CommandText);

            object? result;
            try
            {
                result = await deleteCmd.ExecuteScalarAsync(ct);
            }
            catch (PostgresException ex) when (string.Equals(ex.SqlState, PostgresErrorCodes.ForeignKeyViolation, StringComparison.Ordinal))
            {
                await tx.RollbackAsync(savepoint, ct);

                if (await HasRestrictedRightLinkAsync(cnn, tx, entityType, entityId.Value, ct))
                {
                    return (OperationOutcome.StillLinked, false);
                }

                // No restricted link remains: a cascading link must have been inserted
                // concurrently after step (1) ran. Retry both statements once; if the retry
                // (the last attempt) hits this again with still no restricted link, let the
                // exception surface instead of trying a third time.
                if (attempt == maxAttempts - 1)
                {
                    throw;
                }

                continue;
            }

            var deletedEntityId = result is Guid guid ? guid : (Guid?)null;
            return (OperationOutcome.Success, deletedEntityId.HasValue);
        }

        throw new UnreachableException("The entity delete loop always returns or throws within its attempts.");
    }

    /// <summary>
    /// Resolves the entity id for a by-key delete, or <see langword="null"/> if no such entity exists.
    /// </summary>
    private async Task<Guid?> ResolveEntityIdByKeyAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
    {
        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT entity_id FROM {_entityKeys}
            WHERE entity_type_id = @entity_type_id
              AND key_type_id = @key_type_id
              AND key_type_version = @key_type_version
              AND key_value = @key_value
              AND pool_id = @pool_id
            """;
        _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@key_type_id", (int)key.DskVersion.KeyType.Id);
        _ = cmd.Parameters.AddWithValue("@key_type_version", (int)key.DskVersion.SchemaVersion);
        _ = cmd.Parameters.AddWithValue("@key_value", key.Value);
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    /// <summary>
    /// Deletes links where the given entity is the left side, and right-side links that are not
    /// restricted (<c>restrict_delete = false</c>). Restricted right-side links are left in place
    /// so the entity delete's foreign key check can refuse the delete.
    /// </summary>
    private async Task DeleteCascadingLinksCoreAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
        EntityType entityType,
        Guid entityId,
        Ct ct)
    {
        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            DELETE FROM {_entityLinks}
            WHERE pool_id = @pool_id
              AND (
                (left_entity_type_id = @entity_type_id AND left_entity_id = @entity_id)
                OR (right_entity_type_id = @entity_type_id AND right_entity_id = @entity_id AND restrict_delete = FALSE)
              )
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@entity_id", entityId);

        Log.ExecutingSql(logger, cmd.CommandText);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Returns whether a restricted (<c>restrict_delete = true</c>) link still exists with the
    /// given entity as the right side.
    /// </summary>
    private async Task<bool> HasRestrictedRightLinkAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
        EntityType entityType,
        Guid entityId,
        Ct ct)
    {
        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            SELECT EXISTS(
                SELECT 1 FROM {_entityLinks}
                WHERE pool_id = @pool_id
                  AND right_entity_type_id = @entity_type_id
                  AND right_entity_id = @entity_id
                  AND restrict_delete = TRUE
            )
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@entity_id", entityId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }



    private async Task<bool> AlreadyExistsInTransactionAsync(
        NpgsqlConnection cnn,
        NpgsqlTransaction tx,
        EntityType entityType,
        Guid entityId,
        Ct ct)
    {
        await using var checkExistsCmd = cnn.CreateCommand();
        checkExistsCmd.Transaction = tx;
        checkExistsCmd.CommandText =
            $"SELECT value_version FROM {_entities} WHERE entity_type_id = @entity_type_id AND entity_id = @entity_id AND pool_id = @pool_id";

        _ = checkExistsCmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = checkExistsCmd.Parameters.AddWithValue("@entity_id", entityId);
        _ = checkExistsCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        Log.ExecutingSql(logger, checkExistsCmd.CommandText);

        return (int?)await checkExistsCmd.ExecuteScalarAsync(ct) is not null;
    }

    private (StringBuilder JoinSql, string LastFilterColumn) BuildLinkJoins(
        LinkQueryDescriptor query,
        NpgsqlCommand cmd)
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
                    $"JOIN {_entityLinks} l0 ON l0.{sourceSide} = e.entity_id AND l0.link_type_id = {linkTypeParam} AND l0.pool_id = @pool_id");
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
                    $"JOIN {_entityLinks} l{i} ON l{i}.{sourceSide} = l{i - 1}.{prevFilterSide} AND l{i}.link_type_id = {linkTypeParam} AND l{i}.pool_id = @pool_id");
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

            await using var cmd = dataSource.CreateCommand();

            var (joinSql, anchorColumn) = BuildLinkJoins(query, cmd);
            var anchorIds = query.WhereEntityIds.Select(id => id.Value).ToArray();
            _ = cmd.Parameters.Add(new NpgsqlParameter("@anchor_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = anchorIds });
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@source_entity_type_id", (int)query.SourceEntityType.Id);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take + 1);

            // Dedupe and page on ids only, then fetch the values for the page, so the sort doesn't carry `value` for every skipped row.
            cmd.CommandText = $"""
                SELECT p.anchor_id, ent.entity_id, ent.value, ent.dso_type_schema_version, ent.value_version, ent.created_at, ent.last_updated_at
                FROM (
                    SELECT DISTINCT {anchorColumn} AS anchor_id, e.entity_id
                    FROM {_entities} e
                    {joinSql}
                    WHERE e.entity_type_id = @source_entity_type_id
                      AND e.pool_id = @pool_id
                      AND {anchorColumn} = ANY(@anchor_ids)
                    ORDER BY anchor_id, e.entity_id
                    OFFSET @offset LIMIT @limit
                ) p
                JOIN {_entities} ent ON ent.entity_id = p.entity_id AND ent.pool_id = @pool_id AND ent.entity_type_id = @source_entity_type_id
                ORDER BY p.anchor_id, ent.entity_id
                """;
            Log.ExecutingQuery(logger, cmd.CommandText);

            var items = new List<LinkedEnvelope>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var anchorId = Storage.UuidV7.From(reader.GetGuid(0));
                    var entityId = reader.GetGuid(1);
                    var jsonValue = reader.GetString(2);
                    var dsoTypeVersion = reader.GetInt32(3);
                    var valueVersion = reader.GetInt32(4);
                    var created = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);
                    var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(6, ct);

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
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

            await using var cmd = dataSource.CreateCommand();

            var (joinSql, anchorColumn) = BuildLinkJoins(query, cmd);
            var anchorIds = query.WhereEntityIds.Select(id => id.Value).ToArray();
            _ = cmd.Parameters.Add(new NpgsqlParameter("@anchor_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = anchorIds });
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@source_entity_type_id", (int)query.SourceEntityType.Id);

            cmd.CommandText = $"""
                SELECT {anchorColumn} AS anchor_id, COUNT(DISTINCT e.entity_id)
                FROM {_entities} e
                {joinSql}
                WHERE e.entity_type_id = @source_entity_type_id
                  AND e.pool_id = @pool_id
                  AND {anchorColumn} = ANY(@anchor_ids)
                GROUP BY {anchorColumn}
                """;
            Log.ExecutingSql(logger, cmd.CommandText);

            var counts = query.WhereEntityIds.ToDictionary(id => id, _ => 0L);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                counts[Storage.UuidV7.From(reader.GetGuid(0))] = reader.GetInt64(1);
            }

            return counts;
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

            await using var cmd = dataSource.CreateCommand();

            var (joinSql, whereLastJoin) = BuildLinkJoins(query, cmd);

            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@source_entity_type_id", sourceEntityTypeId);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take);

            string whereClause;
            if (query.WhereEntityId is not null)
            {
                _ = cmd.Parameters.AddWithValue("@where_entity_id", query.WhereEntityId.Value);
                whereClause = $"{whereLastJoin} = @where_entity_id";
            }
            else
            {
                whereClause = "1=1";
            }

            var mainQuery = $"""
                SELECT DISTINCT e.entity_id, e.value, e.dso_type_schema_version, e.value_version, e.created_at, e.last_updated_at
                FROM {_entities} e
                {joinSql}
                WHERE e.entity_type_id = @source_entity_type_id
                  AND e.pool_id = @pool_id
                  AND {whereClause}
                ORDER BY e.entity_id
                OFFSET @offset LIMIT @limit
                """;

            cmd.CommandText = mainQuery;
            Log.ExecutingQuery(logger, mainQuery);

            var items = new List<MetadataEnvelope>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var entityId = reader.GetGuid(0);
                    var jsonValue = reader.GetString(1);
                    var dsoTypeVersion = reader.GetInt32(2);
                    var valueVersion = reader.GetInt32(3);
                    var created = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
                    var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);

                    var version = new DataStorageObjectVersion(query.SourceEntityType, (uint)dsoTypeVersion);
                    var dsoType = dataStorageTypeRegistry.Get(version);
                    var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                    items.Add(new MetadataEnvelope(item, entityId, valueVersion, created, lastUpdated));
                }
            }

            // Count query
            var countQuery = $"""
                SELECT COUNT(DISTINCT e.entity_id)
                FROM {_entities} e
                {joinSql}
                WHERE e.entity_type_id = @source_entity_type_id
                  AND e.pool_id = @pool_id
                  AND {whereClause}
                """;

            await using var countCmd = dataSource.CreateCommand();
            _ = countCmd.Parameters.AddWithValue("@source_entity_type_id", sourceEntityTypeId);
            _ = countCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            if (query.WhereEntityId is not null)
            {
                _ = countCmd.Parameters.AddWithValue("@where_entity_id", query.WhereEntityId.Value);
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
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

            await using var cmd = dataSource.CreateCommand();

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
                FROM {_entities} v
                WHERE v.entity_type_id = @entity_type_id
                  AND v.pool_id = @pool_id
                  AND ({whereClause})
                """;

            _ = cmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

            cmd.CommandText = query;

            Log.ExecutingSql(logger, query);

            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

            await using var cnn = await dataSource.OpenConnectionAsync(ct);
            await using var tx = await cnn.BeginTransactionAsync(ct);

            await using var cmd = cnn.CreateCommand();
            cmd.Transaction = tx;

            var sql = new StringBuilder();

            // Step 1: Lock expired rows into a temp table
            _ = sql.AppendLine(CultureInfo.InvariantCulture, $"""
                CREATE TEMP TABLE _expired ON COMMIT DROP AS
                SELECT pool_id, entity_id, entity_type_id, entity_type_name, value, dso_type_schema_version, gen_random_uuid() AS event_id
                FROM {_entities}
                WHERE expires_at IS NOT NULL AND expires_at <= @now
                LIMIT @batchSize
                FOR UPDATE SKIP LOCKED;
                """);
            _ = cmd.Parameters.Add(new NpgsqlParameter("@now", NpgsqlDbType.TimestampTz) { Value = now.UtcDateTime });
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
                        INSERT INTO {_outboxSubscriptionQueue}
                        (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version)
                        SELECT gen_random_uuid(), event_id, @now, @eventName, entity_id, entity_type_id, entity_type_name, pool_id, value, {subParam}, dso_type_schema_version
                        FROM _expired
                        """);

                    if (subscription.EntityTypeIds.Count > 0)
                    {
                        var typesParam = $"@subTypes{subscriptionIndex}";
                        _ = cmd.Parameters.Add(new NpgsqlParameter(typesParam, NpgsqlDbType.Array | NpgsqlDbType.Integer)
                        {
                            Value = subscription.EntityTypeIds.ToArray()
                        });
                        _ = sql.Append(CultureInfo.InvariantCulture, $" WHERE entity_type_id = ANY({typesParam})");
                    }

                    _ = sql.AppendLine(";");
                    subscriptionIndex++;
                }
            }

            // Step 3: Delete all links touching an expired entity, on either side. entity_links no
            // longer cascades in the database (both FKs are NO ACTION), so purge must remove these
            // explicitly; restricted links are removed too — purging an expired entity overrides
            // Restrict.
            _ = sql.AppendLine(CultureInfo.InvariantCulture, $"""
                DELETE FROM {_entityLinks} el
                USING _expired e
                WHERE el.pool_id = e.pool_id
                  AND (
                    (el.left_entity_id = e.entity_id AND el.left_entity_type_id = e.entity_type_id)
                    OR (el.right_entity_id = e.entity_id AND el.right_entity_type_id = e.entity_type_id)
                  );
                """);

            // Step 4: Delete entities — last statement so ExecuteNonQueryAsync returns this count
            _ = sql.Append(CultureInfo.InvariantCulture, $"""
                DELETE FROM {_entities} e
                USING _expired
                WHERE e.pool_id = _expired.pool_id AND e.entity_type_id = _expired.entity_type_id AND e.entity_id = _expired.entity_id
                  AND e.expires_at <= @now;
                """);

            cmd.CommandText = sql.ToString();
            Log.ExecutingSql(logger, cmd.CommandText);
            var deleted = await cmd.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);
            return deleted;
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
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

            await using var cnn = await dataSource.OpenConnectionAsync(ct);

            // Drain entity_links completely before any entity batch: entity_links no longer
            // cascades in the database (both FKs are NO ACTION), so purging it explicitly first
            // avoids a foreign key violation on the later entity deletes, and it must be fully
            // drained (not just one batch per entity batch) because an independent entity batch
            // could otherwise select an entity that still has links left over from a batch size
            // smaller than its in-degree.
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                await using var tx = await cnn.BeginTransactionAsync(ct);

                await using var linksCmd = cnn.CreateCommand();
                linksCmd.Transaction = tx;
                linksCmd.CommandText = $"""
                    DELETE FROM {_entityLinks}
                    WHERE ctid IN (
                        SELECT ctid FROM {_entityLinks}
                        WHERE pool_id = @poolId
                        LIMIT @batchSize
                    )
                    """;
                _ = linksCmd.Parameters.AddWithValue("@poolId", poolId.Value);
                _ = linksCmd.Parameters.AddWithValue("@batchSize", batchSize);
                Log.ExecutingSql(logger, linksCmd.CommandText);
                var batchLinks = await linksCmd.ExecuteNonQueryAsync(ct);

                await tx.CommitAsync(ct);
                totalLinksDeleted += batchLinks;

                if (batchLinks == 0)
                {
                    break;
                }
            }

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                await using var tx = await cnn.BeginTransactionAsync(ct);

                await using var outboxCmd = cnn.CreateCommand();
                outboxCmd.Transaction = tx;
                outboxCmd.CommandText = $"""
                    DELETE FROM {_outboxSubscriptionQueue}
                    WHERE ctid IN (
                        SELECT ctid FROM {_outboxSubscriptionQueue}
                        WHERE pool_id = @poolId
                        LIMIT @batchSize
                    )
                    """;
                _ = outboxCmd.Parameters.AddWithValue("@poolId", poolId.Value);
                _ = outboxCmd.Parameters.AddWithValue("@batchSize", batchSize);
                Log.ExecutingSql(logger, outboxCmd.CommandText);
                var batchOutbox = await outboxCmd.ExecuteNonQueryAsync(ct);

                await using var entitiesCmd = cnn.CreateCommand();
                entitiesCmd.Transaction = tx;
                entitiesCmd.CommandText = $"""
                    DELETE FROM {_entities}
                    WHERE ctid IN (
                        SELECT ctid FROM {_entities}
                        WHERE pool_id = @poolId
                        LIMIT @batchSize
                    )
                    """;
                _ = entitiesCmd.Parameters.AddWithValue("@poolId", poolId.Value);
                _ = entitiesCmd.Parameters.AddWithValue("@batchSize", batchSize);
                Log.ExecutingSql(logger, entitiesCmd.CommandText);
                var batchEntities = await entitiesCmd.ExecuteNonQueryAsync(ct);

                await tx.CommitAsync(ct);

                totalOutboxDeleted += batchOutbox;
                totalEntitiesDeleted += batchEntities;

                if (batchOutbox + batchEntities == 0)
                {
                    break;
                }
            }

            return new PurgeResult(totalEntitiesDeleted, totalLinksDeleted, totalOutboxDeleted);
        }
        catch (PostgresException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

}
