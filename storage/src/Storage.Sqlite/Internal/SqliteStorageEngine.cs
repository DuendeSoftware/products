// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

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
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.Sqlite.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
internal sealed class SqliteStorageEngine(
    SqliteStorageEngineOptions options,
    DataStorageTypeRegistry dataStorageTypeRegistry,
    TimeProvider timeProvider,
    OutboxSubscriptions outboxSubscriptions,
    ILogger<SqliteStorageEngine> logger) : StoreBase, IPartitionedStorage, ICrossPartitionStorage, IStorageInstanceSchema
{
    private const int RequiredSchemaVersion = 3;
    private static readonly SqliteDialect Dialect = new();

    // SQLite's default schema is "main", used for SqlWhereClauseBuilder schema-qualified table names
    private const string SchemaName = "main";

    // Exposes the configured connection string for tests to assert which store instance
    // (default vs keyed) was resolved, without reflecting on compiler-generated field names.
    internal string? ConnectionString => options.ConnectionString;

    /// <summary>
    /// Creates, opens, and configures a new SQLite connection.
    /// Enables foreign key enforcement which is OFF by default in SQLite.
    /// </summary>
    private async Task<SqliteConnection> OpenConnectionAsync(Ct ct)
    {
        var cnn = new SqliteConnection(options.ConnectionString);
        await cnn.OpenAsync(ct);

        await using var pragmaCmd = cnn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON";
        _ = await pragmaCmd.ExecuteNonQueryAsync(ct);

        return cnn;
    }

    async Task<CheckSchemaVersionResult> IStorageInstanceSchema.CheckVersionAsync(Ct ct)
    {
        Log.CheckingSchemaVersion(logger);

        await using var connection = await OpenConnectionAsync(ct);

        // Check if __schema_info table exists
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='__schema_info' COLLATE NOCASE";
        Log.ExecutingSql(logger, checkCmd.CommandText);
        var tableExists = await checkCmd.ExecuteScalarAsync(ct);

        if (tableExists is null)
        {
            return new CheckSchemaVersionResult(0, RequiredSchemaVersion);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM __schema_info WHERE key = 'SchemaVersion'";
        Log.ExecutingSql(logger, cmd.CommandText);
        var scalar = await cmd.ExecuteScalarAsync(ct);

        if (scalar is null or DBNull)
        {
            return new CheckSchemaVersionResult(0, RequiredSchemaVersion);
        }

        try
        {
            var schemaComment = JsonSerializer.Deserialize<SchemaComment>((string)scalar)!;
            return new CheckSchemaVersionResult((uint)schemaComment.Version, RequiredSchemaVersion);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid database schema version info", ex);
        }
    }

    async Task IStorageInstanceSchema.MigrateAsync(Ct ct)
    {
        Log.MigratingSchema(logger, "sqlite");

        var versionResult = await ((IStorageInstanceSchema)this).CheckVersionAsync(ct);
        var currentVersion = new DatabaseSchemaVersion((int)versionResult.CurrentVersion);

        await using var connection = await OpenConnectionAsync(ct);

        foreach (var (_, sql) in MigrationScriptLoader.GetScripts(typeof(SqliteStorageEngine).Assembly, currentVersion))
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            Log.ExecutingSql(logger, cmd.CommandText);
            _ = await cmd.ExecuteNonQueryAsync(ct);

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
        Log.VerifyingSchema(logger, "sqlite");

        var errors = new List<SchemaVerificationError>();

        await using var connection = await OpenConnectionAsync(ct);

        // Expected tables and their required columns (name -> affinity/type hint)
        var expectedTables = new Dictionary<string, IReadOnlyList<(string Column, string TypeHint)>>
        {
            ["__schema_info"] =
            [
                ("key", "TEXT"),
                ("value", "TEXT")
            ],
            ["entities"] =
            [
                ("pool_id", "INTEGER"),
                ("entity_type_id", "INTEGER"),
                ("entity_id", "TEXT"),
                ("entity_type_name", "TEXT"),
                ("value", "TEXT"),
                ("dso_type_schema_version", "INTEGER"),
                ("value_version", "INTEGER"),
                ("created_at", "TEXT"),
                ("last_updated_at", "TEXT"),
                ("expires_at", "TEXT")
            ],
            ["entity_keys"] =
            [
                ("pool_id", "INTEGER"),
                ("entity_type_id", "INTEGER"),
                ("key_type_id", "INTEGER"),
                ("key_type_version", "INTEGER"),
                ("key_type_name", "TEXT"),
                ("key_value", "TEXT"),
                ("key_json", "TEXT"),
                ("entity_id", "TEXT"),
                ("timestamp", "TEXT")
            ],
            ["search_values"] =
            [
                ("entity_type_id", "INTEGER"),
                ("entity_id", "TEXT"),
                ("field_path", "BLOB"),
                ("field_path_text", "TEXT"),
                ("item_index", "INTEGER"),
                ("string_value", "TEXT"),
                ("number_value", "REAL"),
                ("datetime_value", "TEXT"),
                ("boolean_value", "INTEGER"),
                ("guid_value", "TEXT"),
                ("pool_id", "INTEGER")
            ],
            ["entity_links"] =
            [
                ("pool_id", "INTEGER"),
                ("link_type_id", "INTEGER"),
                ("left_entity_type_id", "INTEGER"),
                ("left_entity_id", "TEXT"),
                ("right_entity_type_id", "INTEGER"),
                ("right_entity_id", "TEXT"),
                ("restrict_delete", "INTEGER"),
                ("created_at", "TEXT")
            ],
            ["outbox_subscriber_queue"] =
            [
                ("sequence_number", "INTEGER"),
                ("message_id", "TEXT"),
                ("event_id", "TEXT"),
                ("timestamp", "TEXT"),
                ("event_name", "TEXT"),
                ("subject_id", "TEXT"),
                ("entity_type_id", "INTEGER"),
                ("entity_type_name", "TEXT"),
                ("pool_id", "INTEGER"),
                ("payload", "TEXT"),
                ("subscriber_name", "TEXT"),
                ("dso_type_schema_version", "INTEGER")
            ]
        };

        var actualTables = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var actualKeys = new List<SchemaVerification.KeyColumn>();

        // Check each expected table
        foreach (var (tableName, expectedColumns) in expectedTables)
        {
            await using var tableCmd = connection.CreateCommand();
            tableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@name COLLATE NOCASE";
            _ = tableCmd.Parameters.AddWithValue("@name", tableName);
            var tableExists = await tableCmd.ExecuteScalarAsync(ct);

            if (tableExists is null)
            {
                errors.Add(new SchemaVerificationError(tableName, null, $"Table '{tableName}' is missing.", SchemaVerificationErrorKind.MissingTable));
                continue;
            }

            var actualColumnNames = new HashSet<string>(StringComparer.Ordinal);
            actualTables.Add(tableName, actualColumnNames);
            var hasPrimaryKeyIndex = false;
            if (tableName == "outbox_subscriber_queue")
            {
                await using var primaryIndexCmd = connection.CreateCommand();
                primaryIndexCmd.CommandText =
                    "SELECT EXISTS(SELECT 1 FROM pragma_index_list('outbox_subscriber_queue') WHERE origin = 'pk')";
                hasPrimaryKeyIndex = Convert.ToInt64(await primaryIndexCmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) != 0;
            }

            // Check columns via PRAGMA table_info
            await using var colCmd = connection.CreateCommand();
            colCmd.CommandText = $"PRAGMA table_info({tableName})";
            await using var colReader = await colCmd.ExecuteReaderAsync(ct);

            var actualColumns = new Dictionary<string, (string Type, bool IsNullable, int PrimaryKeyPosition)>(StringComparer.OrdinalIgnoreCase);
            while (await colReader.ReadAsync(ct))
            {
                var colName = colReader.GetString(1);
                var colType = colReader.GetString(2).ToUpperInvariant();
                var primaryKeyPosition = colReader.GetInt32(5);
                actualColumns[colName] = (colType, colReader.GetInt32(3) == 0, primaryKeyPosition);
                _ = actualColumnNames.Add(colName);

                if (primaryKeyPosition > 0)
                {
                    actualKeys.Add(new SchemaVerification.KeyColumn(tableName, "PRIMARY", true, colName, primaryKeyPosition));
                }
            }

            foreach (var (colName, typeHint) in expectedColumns)
            {
                if (!actualColumns.TryGetValue(colName, out var actualColumn))
                {
                    errors.Add(new SchemaVerificationError(tableName, colName, $"Column '{tableName}.{colName}' is missing.", SchemaVerificationErrorKind.MissingColumn));
                }
                else
                {
                    if (!actualColumn.Type.Contains(typeHint, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new SchemaVerificationError(tableName, colName, $"Column '{tableName}.{colName}' has type '{actualColumn.Type}', expected '{typeHint}'.", SchemaVerificationErrorKind.WrongType));
                    }

                    // A rowid alias is non-null even when PRAGMA table_info reports notnull=0.
                    // INTEGER PRIMARY KEY DESC has a separate index and can actually store NULL.
                    var isRowIdAlias = tableName == "outbox_subscriber_queue" &&
                                       colName == "sequence_number" &&
                                       actualColumn.Type == "INTEGER" &&
                                       actualColumn.PrimaryKeyPosition == 1 &&
                                       !hasPrimaryKeyIndex;
                    SchemaVerification.VerifyNullability(
                        errors, tableName, colName, actualColumn.IsNullable && !isRowIdAlias, isSqlite: true);
                }
            }
        }

        var uniqueIndexes = new List<string>();
        await using (var uniqueCmd = connection.CreateCommand())
        {
            uniqueCmd.CommandText = """
                SELECT name
                FROM pragma_index_list('outbox_subscriber_queue')
                WHERE "unique" = 1 AND origin = 'u'
                """;
            await using var reader = await uniqueCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                uniqueIndexes.Add(reader.GetString(0));
            }
        }

        foreach (var index in uniqueIndexes)
        {
            await using var uniqueColumnsCmd = connection.CreateCommand();
            uniqueColumnsCmd.CommandText = "SELECT name, seqno FROM pragma_index_info(@name) ORDER BY seqno";
            _ = uniqueColumnsCmd.Parameters.AddWithValue("@name", index);
            await using var reader = await uniqueColumnsCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                actualKeys.Add(new SchemaVerification.KeyColumn(
                    "outbox_subscriber_queue", index, false,
                    await reader.IsDBNullAsync(0, ct) ? "<expression>" : reader.GetString(0), reader.GetInt32(1) + 1));
            }
        }

        SchemaVerification.VerifyKeys(errors, actualTables, actualKeys, schemaInfoPrimaryKey: "key");

        // Verify required indexes
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
            ("entity_links", "entity_links_right_fk_index", "entity_links_right_fk_trailing"),
            ("outbox_subscriber_queue", "outbox_subscriber_queue_subscriber_index", "outbox_subscriber_queue_subscriber"),
            ("outbox_subscriber_queue", "outbox_subscriber_queue_pool_id_index", "outbox_subscriber_queue_pool_id"),
        };

        var actualIndexNames = new HashSet<(string Table, string Index)>(
            EqualityComparer<(string, string)>.Create(
                (a, b) => string.Equals(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase),
                x => StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item1) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item2)));
        {
            await using var idxCmd = connection.CreateCommand();
            idxCmd.CommandText = "SELECT tbl_name, name FROM sqlite_master WHERE type='index' AND name IS NOT NULL";
            await using var idxReader = await idxCmd.ExecuteReaderAsync(ct);

            while (await idxReader.ReadAsync(ct))
            {
                _ = actualIndexNames.Add((idxReader.GetString(0), idxReader.GetString(1)));
            }
        }

        var actualIndexes = new List<SchemaVerification.IndexColumn>();
        foreach (var (tableName, indexName, _) in expectedIndexes)
        {
            if (actualIndexNames.Contains((tableName, indexName)))
            {
                await using var columnsCmd = connection.CreateCommand();
                columnsCmd.CommandText = "SELECT name, seqno FROM pragma_index_xinfo(@name) WHERE key = 1 ORDER BY seqno";
                _ = columnsCmd.Parameters.AddWithValue("@name", indexName);
                await using var reader = await columnsCmd.ExecuteReaderAsync(ct);
                var hasColumns = false;
                while (await reader.ReadAsync(ct))
                {
                    hasColumns = true;
                    actualIndexes.Add(new SchemaVerification.IndexColumn(
                        tableName, indexName, await reader.IsDBNullAsync(0, ct) ? null : reader.GetString(0), reader.GetInt32(1) + 1));
                }

                if (!hasColumns)
                {
                    actualIndexes.Add(new SchemaVerification.IndexColumn(tableName, indexName, null, 0));
                }
            }
        }

        SchemaVerification.VerifyIndexes(errors, actualTables, expectedIndexes, actualIndexes, (tableName, indexName) =>
            new SchemaVerificationError(
                tableName, null,
                $"Index '{indexName}' is missing from table '{tableName}'.",
                SchemaVerificationErrorKind.MissingIndex));

        // Verify required foreign keys
        var actualForeignKeys = new List<SchemaVerification.ForeignKeyColumn>();
        var entityPrimaryKeyColumns = new List<string>();
        await using (var primaryKeyCmd = connection.CreateCommand())
        {
            primaryKeyCmd.CommandText = "SELECT name FROM pragma_table_info('entities') WHERE pk > 0 ORDER BY pk";
            await using var primaryKeyReader = await primaryKeyCmd.ExecuteReaderAsync(ct);
            while (await primaryKeyReader.ReadAsync(ct))
            {
                entityPrimaryKeyColumns.Add(primaryKeyReader.GetString(0));
            }
        }

        foreach (var tableName in new[] { "entity_keys", "search_values" })
        {
            await using var fkCmd = connection.CreateCommand();
            fkCmd.CommandText = $"PRAGMA foreign_key_list({tableName})";
            await using var fkReader = await fkCmd.ExecuteReaderAsync(ct);

            while (await fkReader.ReadAsync(ct))
            {
                var sequence = fkReader.GetInt32(1);
                var referencedColumn = await fkReader.IsDBNullAsync(4, ct)
                    ? entityPrimaryKeyColumns.ElementAtOrDefault(sequence) ?? ""
                    : fkReader.GetString(4);
                actualForeignKeys.Add(new SchemaVerification.ForeignKeyColumn(
                    tableName, fkReader.GetInt32(0).ToString(CultureInfo.InvariantCulture), fkReader.GetString(2), true,
                    fkReader.GetString(3), referencedColumn, fkReader.GetString(6), sequence + 1));
            }
        }

        SchemaVerification.VerifyForeignKeys(errors, actualTables, actualForeignKeys);

        // Verify required foreign keys for entity_links. SQLite's FKs aren't named: PRAGMA
        // foreign_key_list returns one row per column with a shared "id" for each constraint, so
        // rows must be grouped by id and all three source-to-target column pairs validated
        // together with the referenced table and delete action, otherwise a single matching
        // column (e.g. right_entity_id -> entities) would pass even if another column in the same
        // FK pointed at the wrong column (e.g. left_entity_type_id instead of right_entity_type_id).
        var expectedLinkForeignKeys = new (string ConstraintLabel, (string From, string To)[] Columns, string OnDelete)[]
        {
            ("left", [("pool_id", "pool_id"), ("left_entity_type_id", "entity_type_id"), ("left_entity_id", "entity_id")], "NO ACTION"),
            ("right", [("pool_id", "pool_id"), ("right_entity_type_id", "entity_type_id"), ("right_entity_id", "entity_id")], "NO ACTION"),
        };

        await using (var linkFkCmd = connection.CreateCommand())
        {
            linkFkCmd.CommandText = "PRAGMA foreign_key_list(entity_links)";
            await using var linkFkReader = await linkFkCmd.ExecuteReaderAsync(ct);

            var actualLinkForeignKeys = new List<(int Id, string ReferencedTable, string From, string To, string OnDelete)>();
            while (await linkFkReader.ReadAsync(ct))
            {
                actualLinkForeignKeys.Add((
                    linkFkReader.GetInt32(0),
                    linkFkReader.GetString(2), // "table" (referenced table)
                    linkFkReader.GetString(3), // "from" (referencing column)
                    linkFkReader.GetString(4), // "to" (referenced column)
                    linkFkReader.GetString(6))); // "on_delete"
            }

            var groupedById = actualLinkForeignKeys
                .GroupBy(fk => fk.Id)
                .ToArray();

            foreach (var (constraintLabel, expectedColumns, onDelete) in expectedLinkForeignKeys)
            {
                var found = groupedById.Any(group =>
                    group.Count() == expectedColumns.Length &&
                    group.All(fk => string.Equals(fk.ReferencedTable, "entities", StringComparison.OrdinalIgnoreCase)) &&
                    group.All(fk => string.Equals(fk.OnDelete, onDelete, StringComparison.OrdinalIgnoreCase)) &&
                    expectedColumns.All(expected => group.Any(fk =>
                        string.Equals(fk.From, expected.From, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(fk.To, expected.To, StringComparison.OrdinalIgnoreCase))));

                if (!found)
                {
                    errors.Add(new SchemaVerificationError(
                        "entity_links", null,
                        $"Foreign key from 'entity_links' ({constraintLabel} side) to 'entities' with the expected column mapping and ON DELETE {onDelete} is missing.",
                        SchemaVerificationErrorKind.MissingForeignKey));
                }
            }
        }

        return new SchemaVerificationResult(errors);
    }

    string IStorageInstanceSchema.BuildMigrationScript(DatabaseSchemaVersion fromVersion)
    {
        var sb = new StringBuilder();
        foreach (var (targetVersion, sql) in MigrationScriptLoader.GetScripts(typeof(SqliteStorageEngine).Assembly, fromVersion))
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"-- Migration step: V{targetVersion - 1} → V{targetVersion}");
            _ = sb.AppendLine(sql);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns whether the database error says that a table does not exist: SQLite error 1 with the text 'no such table'.
    /// </summary>
    private static bool IsMissingStorageTable(SqliteException ex) => ex.SqliteErrorCode == 1 && ex.Message.Contains("no such table", StringComparison.Ordinal);

    /// <summary>
    /// Qualified table names <see cref="SqlWhereClauseBuilder"/> needs to compile a link filter, matching the
    /// qualified names used elsewhere in this engine.
    /// </summary>
    private static readonly StorageTableNames LinkFilterTableNames = new("main.entities", "main.entity_links");

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

            await using var cnn = await OpenConnectionAsync(ct);
            await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

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
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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


            await using var cnn = await OpenConnectionAsync(ct);

            await using var cmd = cnn.CreateCommand();
            cmd.CommandText = """
                SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at
                FROM main.entities v
                WHERE v.entity_type_id = @entity_type_id AND v.entity_id = @entity_id AND v.pool_id = @pool_id
                """;

            _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
            _ = cmd.Parameters.AddWithValue("@entity_id", id.Value.ToString());
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            Log.ExecutingSql(logger, cmd.CommandText);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return StoreGetResult.NotFound();
            }

            var entityId = Guid.Parse(reader.GetString(0));
            var jsonValue = reader.GetString(1);
            var dsoTypeVersion = reader.GetInt32(2);
            var valueVersion = reader.GetInt32(3);
            var created = DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind);
            var lastUpdated = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);

            var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
            var dsoType = dataStorageTypeRegistry.Get(version);
            var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

            return StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated);
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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

            await using var cnn = await OpenConnectionAsync(ct);

            await using var cmd = cnn.CreateCommand();
            cmd.CommandText = """
                SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at
                FROM main.entity_keys i
                INNER JOIN main.entities v ON i.entity_type_id = v.entity_type_id AND i.entity_id = v.entity_id
                WHERE i.entity_type_id = @entity_type_id
                    AND i.key_type_id = @key_type_id
                    AND i.key_type_version = @key_type_version
                    AND i.key_value = @key_value
                    AND i.pool_id = @pool_id
                    AND v.pool_id = @pool_id
                """;

            _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
            _ = cmd.Parameters.AddWithValue("@key_type_id", keyTypeId);
            _ = cmd.Parameters.AddWithValue("@key_type_version", keyTypeVersion);
            _ = cmd.Parameters.AddWithValue("@key_value", keyGuid.ToString());
            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

            Log.ExecutingSql(logger, cmd.CommandText);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return StoreGetResult.NotFound();
            }

            var entityId = Guid.Parse(reader.GetString(0));
            var jsonValue = reader.GetString(1);
            var dsoTypeVersion = reader.GetInt32(2);
            var valueVersion = reader.GetInt32(3);
            var created = DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind);
            var lastUpdated = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);

            var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
            var dsoType = dataStorageTypeRegistry.Get(version);
            var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

            return StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated);
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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

            await using var cnn = await OpenConnectionAsync(ct);

            await using var cmd = cnn.CreateCommand();

            // Build IN clause with individual parameters (SQLite doesn't support array parameters)
            var idParams = new List<string>();
            var i = 0;
            foreach (var id in ids)
            {
                var paramName = $"@id{i}";
                idParams.Add(paramName);
                _ = cmd.Parameters.AddWithValue(paramName, id.Value.ToString());
                i++;
            }

            var inClause = string.Join(", ", idParams);
            cmd.CommandText = $"""
                SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at
                FROM main.entities v
                WHERE v.entity_type_id = @entityTypeId AND v.entity_id IN ({inClause}) AND v.pool_id = @poolId
                """;

            _ = cmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
            _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);

            Log.ExecutingSql(logger, cmd.CommandText);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var results = new List<StoreGetResult>();
            while (await reader.ReadAsync(ct))
            {
                var entityId = Guid.Parse(reader.GetString(0));
                var jsonValue = reader.GetString(1);
                var dsoTypeVersion = reader.GetInt32(2);
                var valueVersion = reader.GetInt32(3);
                var created = DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind);
                var lastUpdated = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);

                var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
                var dsoType = dataStorageTypeRegistry.Get(version);
                var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                results.Add(StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated));
            }

            return results;
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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

            await using var cnn = await OpenConnectionAsync(ct);
            await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

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
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<DeleteResult> IPartitionedStorage.DeleteAsync(EntityType entityType, Storage.UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            var deleteOp = DeleteOperation.ById(entityType, id);

            await using var cnn = await OpenConnectionAsync(ct);
            await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

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
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<DeleteResult> IPartitionedStorage.DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            var deleteOp = DeleteOperation.ByKey(entityType, key);

            await using var cnn = await OpenConnectionAsync(ct);
            await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

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
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private static int AddInserts(StringBuilder builder,
        SqliteCommand cmd,
        IReadOnlyCollection<DataStorageKey> keys)
    {
        var idNumber = 0;

        foreach (var key in keys)
        {
            var keyTypeIdParam = $"@key_type_id{idNumber}";
            var keyTypeNameParam = $"@key_type_name{idNumber}";
            var keyTypeVersionParam = $"@key_type_version{idNumber}";
            var keyValueParam = $"@key{idNumber}";
            var keyStringParam = $"@key_json{idNumber}";

            _ = builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"""
                 INSERT INTO main.entity_keys (entity_type_id, key_type_id, key_type_name, key_value, key_json, key_type_version, entity_id, pool_id)
                 VALUES (@entity_type_id, {keyTypeIdParam}, {keyTypeNameParam}, {keyValueParam}, {keyStringParam}, {keyTypeVersionParam}, @entity_id, @pool_id);
                 """);

            _ = cmd.Parameters.AddWithValue(keyTypeIdParam, (int)key.DskVersion.KeyType.Id);
            _ = cmd.Parameters.AddWithValue(keyTypeNameParam, key.DskVersion.KeyType.Name);
            _ = cmd.Parameters.AddWithValue(keyValueParam, key.Value.ToString());
            _ = cmd.Parameters.AddWithValue(keyStringParam, (object?)key.KeyJsonValue ?? DBNull.Value);
            _ = cmd.Parameters.AddWithValue(keyTypeVersionParam, (int)key.DskVersion.SchemaVersion);

            ++idNumber;
        }

        return idNumber;
    }

    private static int AddSearchFieldInserts(
        StringBuilder builder,
        SqliteCommand cmd,
        SearchFieldCollection? searchFields)
    {
        if (searchFields is null || searchFields.Count == 0)
        {
            return 0;
        }

        var fieldNumber = 0;
        foreach (var field in searchFields)
        {
            var fieldPathParam = $"@field_path{fieldNumber}";
            var fieldPathTextParam = $"@field_path_text{fieldNumber}";
            var itemIndexParam = $"@item_index{fieldNumber}";
            var stringValueParam = $"@string_value{fieldNumber}";
            var numberValueParam = $"@number_value{fieldNumber}";
            var datetimeValueParam = $"@datetime_value{fieldNumber}";
            var booleanValueParam = $"@boolean_value{fieldNumber}";
            var guidValueParam = $"@guid_value{fieldNumber}";

            _ = builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"""
                 INSERT INTO main.search_values (entity_type_id, entity_id, field_path, field_path_text, item_index, string_value, number_value, datetime_value, boolean_value, guid_value, pool_id)
                 VALUES (@entity_type_id, @entity_id, {fieldPathParam}, {fieldPathTextParam}, {itemIndexParam}, {stringValueParam}, {numberValueParam}, {datetimeValueParam}, {booleanValueParam}, {guidValueParam}, @pool_id);
                 """);

            _ = cmd.Parameters.AddWithValue(fieldPathParam, field.FieldPathId.ToByteArray());
            _ = cmd.Parameters.AddWithValue(fieldPathTextParam, field.FieldPath);
            _ = cmd.Parameters.AddWithValue(itemIndexParam, field.ItemIndex ?? -1);
            _ = cmd.Parameters.AddWithValue(stringValueParam, (object?)field.StringValue ?? DBNull.Value);
            _ = cmd.Parameters.AddWithValue(numberValueParam,
                field.NumberValue.HasValue ? field.NumberValue.Value : DBNull.Value);

            // Convert DateTimeOffset to ISO 8601 text for SQLite
            if (field.DateTimeValue.HasValue)
            {
                _ = cmd.Parameters.AddWithValue(datetimeValueParam,
                    field.DateTimeValue.Value.UtcDateTime.ToString("O"));
            }
            else
            {
                _ = cmd.Parameters.AddWithValue(datetimeValueParam, DBNull.Value);
            }

            _ = cmd.Parameters.AddWithValue(booleanValueParam,
                field.BooleanValue.HasValue ? field.BooleanValue.Value ? 1 : 0 : DBNull.Value);

            _ = cmd.Parameters.AddWithValue(guidValueParam,
                field.GuidValue.HasValue ? field.GuidValue.Value.ToString() : DBNull.Value);

            ++fieldNumber;
        }

        return fieldNumber;
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
                return await QueryCursorCoreAsync(entityType, filter, sort, dataRange.TokenValue, ct);
            }

            var (skip, take) = ResolveOffsetAndSize(dataRange);
            var entityTypeId = (int)entityType.Id;

            Log.QueryingDsos(logger, entityType, skip, take);

            await using var cnn = await OpenConnectionAsync(ct);

            await using var cmd = cnn.CreateCommand();
            var queryClauses = BuildQueryClauses(cmd, filter, sort, entityType);

            // Build main query using CTEs
            string allMatchesSelect;
            string pagedOrderBy;
            string outerOrderBy;
            if (!sort.IsEmpty)
            {
                var sortColumn = GetSortColumnName(sort.Field!);
                var sortDirection = sort.Direction == SortDirection.Ascending ? "ASC" : "DESC";
                allMatchesSelect = $"SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at, {sortColumn} AS sort_value";
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
                    FROM main.entities v
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
                    LIMIT @limit OFFSET @offset
                )
                SELECT p.entity_id, p.value, p.dso_type_schema_version, p.value_version, p.created_at, p.last_updated_at, t.total_count
                FROM total t
                LEFT JOIN paged p ON 1
                {outerOrderBy}
                """;

            Dialect.AddParameter(cmd, "@entity_type_id", entityTypeId);
            Dialect.AddParameter(cmd, "@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take);

            cmd.CommandText = query;

            Log.ExecutingQuery(logger, query);

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

                    if (await reader.IsDBNullAsync(0, ct))
                    {
                        continue;
                    }

                    var entityId = Guid.Parse(reader.GetString(0));
                    var jsonValue = reader.GetString(1);
                    var dsoTypeVersion = reader.GetInt32(2);
                    var valueVersion = reader.GetInt32(3);
                    var created = DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind);
                    var lastUpdated = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);

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
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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
                return await QueryFieldsCursorCoreAsync(entityType, fields, filter, sort, dataRange.TokenValue, ct);
            }

            var (skip, take) = ResolveOffsetAndSize(dataRange);
            var entityTypeId = (int)entityType.Id;

            Log.QueryingFieldsDsos(logger, entityType, fields.Count, skip, take);

            await using var cnn = await OpenConnectionAsync(ct);

            await using var cmd = cnn.CreateCommand();
            var queryClauses = BuildQueryClauses(cmd, filter, sort, entityType);

            var fieldPaths = fields.Select(f => f.Path).ToList();
            var fieldConditions = new List<string>();
            var paramIndex = 0;
            for (var i = 0; i < fieldPaths.Count; i++)
            {
                if (SystemFields.IsSystemField(fieldPaths[i]))
                {
                    continue;
                }

                _ = cmd.Parameters.AddWithValue($"@select_field_{paramIndex}", DeterministicGuidGenerator.Create(fieldPaths[i].ToUpperInvariant()).ToByteArray());
                fieldConditions.Add($"field_sv.field_path = @select_field_{paramIndex}");
                paramIndex++;
            }
            var fieldConditionsClause = fieldConditions.Count > 0
                ? string.Join(" OR ", fieldConditions)
                : "1=0";

            string cteSelect;
            string cteJoin;

            if (!sort.IsEmpty)
            {
                cteJoin = queryClauses.JoinClause;
                var sortColumn = GetSortColumnName(sort.Field!);
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
                    FROM main.entities v
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
                    LIMIT @limit OFFSET @offset
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
                LEFT JOIN filtered_ids fi ON 1
                LEFT JOIN main.search_values field_sv
                  ON fi.entity_id = field_sv.entity_id
                  AND field_sv.entity_type_id = @entity_type_id
                  AND field_sv.pool_id = @pool_id
                  AND field_sv.item_index = -1
                  AND ({fieldConditionsClause})
                ORDER BY fi.row_num, fi.entity_id
                """;

            Dialect.AddParameter(cmd, "@entity_type_id", entityTypeId);
            Dialect.AddParameter(cmd, "@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take);

            cmd.CommandText = query;

            Log.ExecutingQuery(logger, query);

            var resultsById = new Dictionary<Guid, (Dictionary<string, object?> FieldValues, DateTimeOffset Created, DateTimeOffset LastUpdated, int Version)>();
            var orderedIds = new List<Guid>();
            var totalCount = 0;
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    if (totalCount == 0)
                    {
                        totalCount = Convert.ToInt32(reader.GetInt64(7));
                    }

                    if (await reader.IsDBNullAsync(0, ct))
                    {
                        continue;
                    }

                    var entityId = Guid.Parse(reader.GetString(0));
                    var fieldPath = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);

                    if (!resultsById.TryGetValue(entityId, out var entry))
                    {
                        var fieldValues = new Dictionary<string, object?>();
                        orderedIds.Add(entityId);

                        foreach (var field in fields)
                        {
                            fieldValues[field.Path] = null;
                        }

                        var created = DateTimeOffset.Parse(reader.GetString(8), null, DateTimeStyles.RoundtripKind);
                        var lastUpdated = DateTimeOffset.Parse(reader.GetString(9), null, DateTimeStyles.RoundtripKind);
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
                        var field = fields.First(f => f.Path == fieldPath);
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
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    /// <summary>
    /// Cursor-based query core for full entities.
    /// </summary>
    private async Task<QueryResult<MetadataEnvelope>> QueryCursorCoreAsync(
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

        await using var cnn = await OpenConnectionAsync(ct);

        await using var cmd = cnn.CreateCommand();
        var queryClauses = BuildCursorQueryClauses(cmd, filter, sort, position, traversal, entityType);

        var query = $"""
            SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at, {queryClauses.SortColumnName}
            FROM main.entities v
            {queryClauses.JoinClause}
            WHERE v.entity_type_id = @entity_type_id
              AND v.pool_id = @pool_id
              AND ({queryClauses.WhereClause})
              {queryClauses.SeekClause}
            {queryClauses.OrderByClause}
            LIMIT @limit
            """;

        Dialect.AddParameter(cmd, "@entity_type_id", entityTypeId);
        Dialect.AddParameter(cmd, "@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@limit", pageSize + 1);

        cmd.CommandText = query;

        Log.ExecutingQuery(logger, query);

        var items = new List<(Guid Id, MetadataEnvelope Item, object? SortValue)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = Guid.Parse(reader.GetString(0));
                var jsonValue = reader.GetString(1);
                var dsoTypeVersion = reader.GetInt32(2);
                var valueVersion = reader.GetInt32(3);
                var created = DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind);
                var lastUpdated = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);

                var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
                var dsoType = dataStorageTypeRegistry.Get(version);
                var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

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

    /// <summary>
    /// Cursor-based query core for projected field values.
    /// </summary>
    private async Task<QueryResult<ProjectedResult>> QueryFieldsCursorCoreAsync(
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

        await using var cnn = await OpenConnectionAsync(ct);

        await using var cmd = cnn.CreateCommand();
        var queryClauses = BuildCursorQueryClauses(cmd, filter, sort, position, traversal, entityType);

        var fieldPaths = fields.Select(f => f.Path).ToList();
        var fieldConditions = new List<string>();
        var paramIndex = 0;
        for (var i = 0; i < fieldPaths.Count; i++)
        {
            if (SystemFields.IsSystemField(fieldPaths[i]))
            {
                continue;
            }

            _ = cmd.Parameters.AddWithValue($"@select_field_{paramIndex}", DeterministicGuidGenerator.Create(fieldPaths[i].ToUpperInvariant()).ToByteArray());
            fieldConditions.Add($"field_sv.field_path = @select_field_{paramIndex}");
            paramIndex++;
        }
        var fieldConditionsClause = fieldConditions.Count > 0
            ? string.Join(" OR ", fieldConditions)
            : "1=0";

        var query = $"""
            WITH filtered_ids AS (
                SELECT v.entity_id, v.created_at, v.last_updated_at, v.value_version, {queryClauses.SortColumnName} AS sort_value, ROW_NUMBER() OVER ({queryClauses.OrderByClause}) AS row_num
                FROM main.entities v
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
            LEFT JOIN main.search_values field_sv
              ON fi.entity_id = field_sv.entity_id
              AND field_sv.entity_type_id = @entity_type_id
              AND field_sv.pool_id = @pool_id
              AND field_sv.item_index = -1
              AND ({fieldConditionsClause})
            ORDER BY fi.row_num, fi.entity_id
            """;

        Dialect.AddParameter(cmd, "@entity_type_id", entityTypeId);
        Dialect.AddParameter(cmd, "@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@limit", pageSize + 1);

        cmd.CommandText = query;

        Log.ExecutingQuery(logger, query);

        var resultsByid = new Dictionary<Guid, (Dictionary<string, object?> FieldValues, DateTimeOffset Created, DateTimeOffset LastUpdated, object? SortValue, int Version)>();
        var orderedIds = new List<Guid>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = Guid.Parse(reader.GetString(0));
                var fieldPath = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);

                if (!resultsByid.TryGetValue(entityId, out var entry))
                {
                    var fieldValues = new Dictionary<string, object?>();

                    foreach (var field in fields)
                    {
                        fieldValues[field.Path] = null;
                    }

                    var sortValue = sort.IsEmpty ? null : await ReadSortValueAsync(reader, sort.Field!, 7, ct);
                    var created = DateTimeOffset.Parse(reader.GetString(8), null, DateTimeStyles.RoundtripKind);
                    var lastUpdated = DateTimeOffset.Parse(reader.GetString(9), null, DateTimeStyles.RoundtripKind);
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
                    var field = fields.First(f => f.Path == fieldPath);
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

    /// <summary>
    /// Builds the WHERE clause, ORDER BY clause, and JOIN clause for a query.
    /// </summary>
    private static QueryClauses BuildQueryClauses(
        SqliteCommand cmd,
        IQueryExpression filter,
        SortParameter sort,
        EntityType entityType)
    {
        var whereBuilder = new SqlWhereClauseBuilder(SchemaName, cmd, Dialect, LinkFilterTableNames, entityType);
        var whereClause = whereBuilder.BuildWhereClause(filter);

        string joinClause;
        string orderByClause;
        if (!sort.IsEmpty)
        {
            var sortFieldPath = sort.Field!.Path;
            var sortDirection = sort.Direction == SortDirection.Ascending ? "ASC" : "DESC";
            var sortColumn = GetSortColumnName(sort.Field!);

            if (SystemFields.IsSystemField(sortFieldPath))
            {
                joinClause = "";
            }
            else
            {
                joinClause = $"""
                    LEFT JOIN main.search_values sort_sv
                      ON v.entity_type_id = sort_sv.entity_type_id
                      AND v.entity_id = sort_sv.entity_id
                      AND v.pool_id = sort_sv.pool_id
                      AND sort_sv.field_path = @sort_field_path
                      AND sort_sv.item_index = -1
                    """;

                _ = cmd.Parameters.AddWithValue("@sort_field_path", DeterministicGuidGenerator.Create(sortFieldPath.ToUpperInvariant()).ToByteArray());
            }

            orderByClause = $"""
                ORDER BY
                  CASE WHEN {sortColumn} IS NULL THEN 1 ELSE 0 END,
                  {sortColumn} {sortDirection},
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
    private static CursorQueryClauses BuildCursorQueryClauses(
        SqliteCommand cmd,
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
    private static (string WhereClause, string JoinClause, string? SortColumn) BuildCursorFilterAndJoin(
        SqliteCommand cmd,
        IQueryExpression filter,
        SortParameter sort,
        EntityType entityType)
    {
        var whereBuilder = new SqlWhereClauseBuilder(SchemaName, cmd, Dialect, LinkFilterTableNames, entityType);
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
                LEFT JOIN main.search_values sort_sv
                  ON v.entity_type_id = sort_sv.entity_type_id
                  AND v.entity_id = sort_sv.entity_id
                  AND v.pool_id = sort_sv.pool_id
                  AND sort_sv.field_path = @sort_field_path
                  AND sort_sv.item_index = -1
                """;

            _ = cmd.Parameters.AddWithValue("@sort_field_path", DeterministicGuidGenerator.Create(sortFieldPath.ToUpperInvariant()).ToByteArray());
        }

        return (whereClause, joinClause, sortColumn);
    }

    /// <summary>
    /// Binds <c>@last_id</c> and, when the seek predicate references it, <c>@last_sort_value</c>
    /// for a decoded cursor key position, using the same per-kind conversions as insertion.
    /// </summary>
    private static void BindCursorPositionParameters(SqliteCommand cmd, CursorPosition position, string? sortColumn)
    {
        if (position.Kind != CursorPositionKind.Key)
        {
            return;
        }

        _ = cmd.Parameters.AddWithValue("@last_id", position.Id.ToString());

        if (sortColumn is null || position.ValueKind == CursorValueKind.Null)
        {
            return;
        }

        object boundValue = position.Value switch
        {
            string s => s,
            decimal d => double.Parse(d.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.UtcDateTime.ToString("O"),
            bool b => b ? 1 : 0,
            Guid g => g.ToString(),
            _ => throw new InvalidOperationException($"Unsupported cursor sort value type: {position.Value!.GetType().Name}")
        };

        _ = cmd.Parameters.AddWithValue("@last_sort_value", boundValue);
    }

    /// <summary>
    /// Resolves the skip and take values from a DataRange (offset or page-based).
    /// </summary>
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
    /// </summary>
    private static async Task<object?> ReadFieldValueAsync(SqliteDataReader reader, FieldType fieldType, int stringValueColumnIndex, Ct ct)
    {
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
            FieldType.Number => Convert.ToDecimal(reader.GetValue(columnIndex), CultureInfo.InvariantCulture),
            FieldType.DateTime => DateTimeOffset.Parse(reader.GetString(columnIndex), CultureInfo.InvariantCulture),
            FieldType.Boolean => reader.GetInt64(columnIndex) != 0,
            FieldType.Guid => Guid.Parse(reader.GetString(columnIndex)),
            _ => throw new InvalidOperationException($"Unsupported field type: {fieldType}")
        };
    }

    /// <summary>
    /// Reads a sort value from a database reader for the specified field type.
    /// </summary>
    private static async Task<object?> ReadSortValueAsync(SqliteDataReader reader, Field sortField, int columnIndex, Ct ct)
    {
        if (await reader.IsDBNullAsync(columnIndex, ct))
        {
            return null;
        }

        return sortField switch
        {
            StringField => reader.GetString(columnIndex),
            NumberField => ReadDecimalFromDouble(reader.GetValue(columnIndex)),
            DateTimeField => DateTimeOffset.Parse(reader.GetString(columnIndex), CultureInfo.InvariantCulture),
            BooleanField => reader.GetInt64(columnIndex) != 0,
            GuidField or ExactMatchField => Guid.Parse(reader.GetString(columnIndex)),
            _ => throw new InvalidOperationException($"Unsupported field type for sorting: {sortField.GetType().Name}")
        };
    }

    /// <summary>
    /// Converts a stored REAL value to a decimal that exactly matches the double as read,
    /// instead of rounding through <see cref="Convert.ToDecimal(double)"/> (which rounds to 15
    /// significant digits and can cause cursor seeks to repeat or skip rows). Values at or beyond
    /// <see cref="decimal.MaxValue"/> or <see cref="decimal.MinValue"/> are clamped to that bound:
    /// <c>decimal.Parse</c> of the full round-trippable ("R") representation of a double near those
    /// bounds can throw <see cref="OverflowException"/> because the double's nearest representable
    /// value falls just outside the decimal range.
    /// </summary>
    private static decimal ReadDecimalFromDouble(object value)
    {
        if (value is double d)
        {
            return d >= (double)decimal.MaxValue
                ? decimal.MaxValue
                : d <= (double)decimal.MinValue
                    ? decimal.MinValue
                    : decimal.Parse(d.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    async Task<LinkResult> IPartitionedStorage.LinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            await using var cnn = await OpenConnectionAsync(ct);
            await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);
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
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    /// <inheritdoc/>
    async Task<UnlinkResult> IPartitionedStorage.UnlinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            await using var cnn = await OpenConnectionAsync(ct);
            await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);
            _ = await ExecuteUnlinkCoreAsync(cnn, tx, UnlinkOperation.For(definition, leftEntityId, rightEntityId), ct);
            if (outboxEvents is { Count: > 0 })
            {
                await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
            }
            await tx.CommitAsync(ct);
            return UnlinkResult.Success;
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private async Task<OperationOutcome> ExecuteLinkCoreAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
        LinkOperation op,
        Ct ct)
    {

        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR IGNORE INTO main.entity_links (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id, restrict_delete)
            VALUES (@pool_id, @link_type_id, @left_entity_type_id, @left_entity_id, @right_entity_type_id, @right_entity_id, @restrict_delete)
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@link_type_id", (int)op.Definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_type_id", (int)op.Definition.Left.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", op.LeftEntityId.Value.ToString());
        _ = cmd.Parameters.AddWithValue("@right_entity_type_id", (int)op.Definition.Right.Id);
        _ = cmd.Parameters.AddWithValue("@right_entity_id", op.RightEntityId.Value.ToString());
        _ = cmd.Parameters.AddWithValue("@restrict_delete", op.Definition.OnRightDelete == LinkDeleteBehavior.Restrict ? 1 : 0);

        Log.ExecutingSql(logger, cmd.CommandText);

        int rowsAffected;
        try
        {
            rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // INSERT OR IGNORE suppresses UNIQUE/PRIMARY KEY/CHECK/NOT NULL violations, but not
            // foreign key violations, so any SQLITE_CONSTRAINT (19) reaching here is the left or
            // right entity not existing in this pool with the definition's entity type.
            return OperationOutcome.DoesNotExist;
        }

        return rowsAffected == 0 ? OperationOutcome.AlreadyLinked : OperationOutcome.Success;
    }

    private async Task<OperationOutcome> ExecuteUnlinkCoreAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
        UnlinkOperation op,
        Ct ct)
    {

        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            DELETE FROM main.entity_links
            WHERE pool_id = @pool_id
              AND link_type_id = @link_type_id
              AND left_entity_id = @left_entity_id
              AND right_entity_id = @right_entity_id
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@link_type_id", (int)op.Definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", op.LeftEntityId.Value.ToString());
        _ = cmd.Parameters.AddWithValue("@right_entity_id", op.RightEntityId.Value.ToString());

        Log.ExecutingSql(logger, cmd.CommandText);

        _ = await cmd.ExecuteNonQueryAsync(ct);
        return OperationOutcome.Success;
    }

    private async Task ExecuteOutboxInsertBatchCoreAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
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
            valueRows.Add($"(@message_id{i}, @event_id{i}, @timestamp{i}, @event_name{i}, @subject_id{i}, @entity_type_id{i}, @entity_type_name{i}, @pool_id, @payload{i}, @subscriber_name{i}, @dso_type_schema_version{i})");
            _ = cmd.Parameters.AddWithValue($"@message_id{i}", Guid.CreateVersion7().ToString());
            _ = cmd.Parameters.AddWithValue($"@event_id{i}", evt.Id.Value.ToString());
            _ = cmd.Parameters.AddWithValue($"@timestamp{i}", evt.Timestamp.UtcDateTime.ToString("O"));
            _ = cmd.Parameters.AddWithValue($"@event_name{i}", evt.EventName.ToString());
            _ = cmd.Parameters.AddWithValue($"@subject_id{i}", evt.SubjectId.Value.ToString());
            _ = cmd.Parameters.AddWithValue($"@entity_type_id{i}", evt.EntityTypeId);
            _ = cmd.Parameters.AddWithValue($"@entity_type_name{i}", evt.EntityTypeName);
            _ = cmd.Parameters.AddWithValue($"@payload{i}", evt.Payload);
            _ = cmd.Parameters.AddWithValue($"@subscriber_name{i}", subscription.SubscriberName.ToString());
            _ = cmd.Parameters.AddWithValue($"@dso_type_schema_version{i}", evt.DsoTypeSchemaVersion.HasValue ? evt.DsoTypeSchemaVersion.Value : DBNull.Value);
        }
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        cmd.CommandText = $"""
            INSERT INTO main.outbox_subscriber_queue
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

            await using var connection = await OpenConnectionAsync(ct);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

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
                    return new BatchResult(false, results);
                }
            }

            if (outboxEvents is { Count: > 0 })
            {
                await ExecuteOutboxInsertBatchCoreAsync(connection, transaction, outboxEvents, ct);
            }

            await transaction.CommitAsync(ct);
            return new BatchResult(true, results);
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    async Task<OutboxEventsPage> ICrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, Ct ct)
    {
        try
        {
            await using var cnn = await OpenConnectionAsync(ct);

            await using var cmd = cnn.CreateCommand();
            cmd.CommandText = """
                SELECT sequence_number, message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version
                FROM main.outbox_subscriber_queue
                WHERE subscriber_name = @subscriber_name
                ORDER BY sequence_number ASC
                LIMIT @limit
                """;
            _ = cmd.Parameters.AddWithValue("@limit", count + 1);
            _ = cmd.Parameters.AddWithValue("@subscriber_name", subscriptionName.ToString());

            Log.ExecutingSql(logger, cmd.CommandText);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var events = new List<PersistedOutboxEvent>();
            while (await reader.ReadAsync(ct))
            {
                var timestamp = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture);
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
                    MessageId = Guid.Parse(reader.GetString(1)),
                    EventId = Guid.Parse(reader.GetString(2)),
                    Timestamp = timestamp,
                    EventName = OutboxEventName.Create(reader.GetString(4)),
                    SubjectId = Storage.UuidV7.From(Guid.Parse(reader.GetString(5))),
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
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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

            await using var cnn = await OpenConnectionAsync(ct);

            const int MaxBatchSize = 1000;
            for (var offset = 0; offset < ids.Count; offset += MaxBatchSize)
            {
                var chunk = ids.Skip(offset).Take(MaxBatchSize).ToArray();

                await using var cmd = cnn.CreateCommand();

                var idParams = new List<string>();
                for (var i = 0; i < chunk.Length; i++)
                {
                    var paramName = $"@id{i}";
                    idParams.Add(paramName);
                    _ = cmd.Parameters.AddWithValue(paramName, chunk[i].Value.ToString());
                }

                cmd.CommandText = $"""
                    DELETE FROM main.outbox_subscriber_queue
                    WHERE message_id IN ({string.Join(", ", idParams)})
                    """;

                Log.ExecutingSql(logger, cmd.CommandText);
                _ = await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private async Task<OperationOutcome> ExecuteCreateCoreAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
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

        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
             INSERT INTO main.entities (entity_type_id, entity_type_name, entity_id, value, dso_type_schema_version, value_version, pool_id, expires_at, created_at, last_updated_at)
             VALUES (@entity_type_id, @entity_type_name, @entity_id, @value, @dso_type_schema_version, 1, @pool_id, @expires_at, @now, @now);
             """);

        await using var createCmd = cnn.CreateCommand();
        createCmd.Transaction = tx;
        _ = createCmd.Parameters.AddWithValue("@entity_type_id", dsoTypeId);
        _ = createCmd.Parameters.AddWithValue("@entity_type_name", entityType.Name);
        _ = createCmd.Parameters.AddWithValue("@entity_id", op.Id.Value.ToString());
        _ = createCmd.Parameters.AddWithValue("@value", jsonDso);
        _ = createCmd.Parameters.AddWithValue("@dso_type_schema_version", (int)dsoVersion.SchemaVersion);
        _ = createCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = createCmd.Parameters.AddWithValue("@now", timeProvider.GetUtcNow().UtcDateTime.ToString("O"));
        if (expiresAt.HasValue)
        {
            _ = createCmd.Parameters.AddWithValue("@expires_at", expiresAt.Value.UtcDateTime.ToString("O"));
        }
        else
        {
            _ = createCmd.Parameters.AddWithValue("@expires_at", DBNull.Value);
        }

        // Add insert statements for keys
        _ = AddInserts(builder, createCmd, op.Keys);

        // Add insert statements for search fields
        _ = AddSearchFieldInserts(builder, createCmd, op.SearchFieldCollection);

        createCmd.CommandText = builder.ToString();

        Log.ExecutingSql(logger, createCmd.CommandText);

        try
        {
            _ = await createCmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            // Distinguish between entity already exists (constraint on entities table)
            // and key conflict (constraint on entity_keys table) by checking the error message.
            // SQLite constraint messages include the table name, e.g.:
            //   "UNIQUE constraint failed: entities.pool_id, entities.entity_type_id, entities.entity_id"
            //   "UNIQUE constraint failed: entity_keys.pool_id, entity_keys.entity_type_id, ..."
            if (ex.Message.Contains("entities.", StringComparison.OrdinalIgnoreCase))
            {
                return OperationOutcome.AlreadyExists;
            }

            return OperationOutcome.KeyConflict;
        }

        return OperationOutcome.Success;
    }

    private async Task<OperationOutcome> ExecuteUpdateCoreAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
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

        // Read the current version of the entity
        await using var readVersionCmd = cnn.CreateCommand();
        readVersionCmd.Transaction = tx;
        readVersionCmd.CommandText =
            "SELECT value_version FROM main.entities WHERE entity_type_id = @entity_type_id AND entity_id = @entity_id AND pool_id = @pool_id";

        _ = readVersionCmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = readVersionCmd.Parameters.AddWithValue("@entity_id", op.Id.Value.ToString());
        _ = readVersionCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        Log.ExecutingSql(logger, readVersionCmd.CommandText);

        var actualEntityVersion = (long?)await readVersionCmd.ExecuteScalarAsync(ct);

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
            : "";

        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"""
             UPDATE main.entities
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

        await using var updateCmd = cnn.CreateCommand();
        updateCmd.Transaction = tx;
        _ = updateCmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = updateCmd.Parameters.AddWithValue("@entity_type_name", entityType.Name);
        _ = updateCmd.Parameters.AddWithValue("@entity_id", op.Id.Value.ToString());
        _ = updateCmd.Parameters.AddWithValue("@value", jsonDso);
        _ = updateCmd.Parameters.AddWithValue("@dso_type_schema_version", (int)dsoVersion.SchemaVersion);
        _ = updateCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = updateCmd.Parameters.AddWithValue("@now", timeProvider.GetUtcNow().UtcDateTime.ToString("O"));
        if (hasExpirationChange)
        {
            if (expiresAt.HasValue)
            {
                _ = updateCmd.Parameters.AddWithValue("@expires_at", expiresAt.Value.UtcDateTime.ToString("O"));
            }
            else
            {
                _ = updateCmd.Parameters.AddWithValue("@expires_at", DBNull.Value);
            }
        }

        // Delete existing keys
        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"DELETE FROM main.entity_keys WHERE entity_type_id = @entity_type_id AND entity_id = @entity_id AND pool_id = @pool_id;");

        // Delete existing search fields
        _ = builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"DELETE FROM main.search_values WHERE entity_type_id = @entity_type_id AND entity_id = @entity_id AND pool_id = @pool_id;");

        // Re-insert new keys
        _ = AddInserts(builder, updateCmd, op.Keys);

        // Re-insert new search fields
        _ = AddSearchFieldInserts(builder, updateCmd, op.SearchFieldCollection);

        updateCmd.CommandText = builder.ToString();

        Log.ExecutingSql(logger, updateCmd.CommandText);

        try
        {
            _ = await updateCmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            return OperationOutcome.KeyConflict;
        }

        return OperationOutcome.Success;
    }

    private async Task<(OperationOutcome Outcome, bool EntityDeleted)> ExecuteDeleteCoreAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
        DeleteOperation op,
        Ct ct)
    {
        var entityType = op.EntityType;

        // Resolve the id up front (by-key deletes look it up via entity_keys) so the same id is
        // used both to delete links where this entity is an endpoint and to delete the entity
        // itself. entity_links has no cascading FK (both FKs are NO ACTION): (1) deletes links
        // where the entity is the left side, and right-side links that are not restricted; (2)
        // deletes the entity. If a restricted right-side link remains, step 2's FK check fails and
        // this returns StillLinked. If step 2 fails but no restricted link is found on re-check (a
        // cascade link was inserted concurrently between the two statements), steps (1) and (2)
        // are retried exactly once; if the retry (the second and last attempt) hits the same
        // failure with still no restricted link, the exception is surfaced instead of trying a
        // third time (matching PostgreSQL/SQL Server/Oracle).
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

            await using var deleteCmd = cnn.CreateCommand();
            deleteCmd.Transaction = tx;
            deleteCmd.CommandText = """
                DELETE FROM main.entities
                WHERE entity_type_id = @entity_type_id AND pool_id = @pool_id AND entity_id = @entity_id
                RETURNING entity_id
                """;
            _ = deleteCmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
            _ = deleteCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = deleteCmd.Parameters.AddWithValue("@entity_id", entityId.Value.ToString());

            Log.ExecutingSql(logger, deleteCmd.CommandText);

            object? result;
            try
            {
                result = await deleteCmd.ExecuteScalarAsync(ct);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
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

            var deletedEntityId = result is string s ? Guid.Parse(s) : (Guid?)null;
            return (OperationOutcome.Success, deletedEntityId.HasValue);
        }

        throw new UnreachableException("The entity delete loop always returns or throws within its attempts.");
    }

    /// <summary>
    /// Resolves the entity id for a by-key delete, or <see langword="null"/> if no such entity exists.
    /// </summary>
    private async Task<Guid?> ResolveEntityIdByKeyAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
    {
        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT entity_id FROM main.entity_keys
            WHERE entity_type_id = @entity_type_id
              AND key_type_id = @key_type_id
              AND key_type_version = @key_type_version
              AND key_value = @key_value
              AND pool_id = @pool_id
            """;
        _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@key_type_id", (int)key.DskVersion.KeyType.Id);
        _ = cmd.Parameters.AddWithValue("@key_type_version", (int)key.DskVersion.SchemaVersion);
        _ = cmd.Parameters.AddWithValue("@key_value", key.Value.ToString());
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string s ? Guid.Parse(s) : null;
    }

    /// <summary>
    /// Deletes links where the given entity is the left side, and right-side links that are not
    /// restricted (<c>restrict_delete = 0</c>). Restricted right-side links are left in place so
    /// the entity delete's foreign key check can refuse the delete.
    /// </summary>
    private async Task DeleteCascadingLinksCoreAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
        EntityType entityType,
        Guid entityId,
        Ct ct)
    {
        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            DELETE FROM main.entity_links
            WHERE pool_id = @pool_id
              AND (
                (left_entity_type_id = @entity_type_id AND left_entity_id = @entity_id)
                OR (right_entity_type_id = @entity_type_id AND right_entity_id = @entity_id AND restrict_delete = 0)
              )
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@entity_id", entityId.ToString());

        Log.ExecutingSql(logger, cmd.CommandText);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Returns whether a restricted (<c>restrict_delete = 1</c>) link still exists with the given
    /// entity as the right side.
    /// </summary>
    private async Task<bool> HasRestrictedRightLinkAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
        EntityType entityType,
        Guid entityId,
        Ct ct)
    {
        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM main.entity_links
                WHERE pool_id = @pool_id
                  AND right_entity_type_id = @entity_type_id
                  AND right_entity_id = @entity_id
                  AND restrict_delete = 1
            )
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@entity_id", entityId.ToString());

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture) != 0;
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

            await using var cnn = await OpenConnectionAsync(ct);

            await using var cmd = cnn.CreateCommand();

            var (joinSql, whereLastJoin) = BuildLinkJoins(query, cmd);

            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@source_entity_type_id", sourceEntityTypeId);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take);

            string whereClause;
            if (query.WhereEntityId is not null)
            {
                _ = cmd.Parameters.AddWithValue("@where_entity_id", query.WhereEntityId.Value.ToString());
                whereClause = $"{whereLastJoin} = @where_entity_id";
            }
            else
            {
                whereClause = "1";
            }

            var mainQuery = $"""
                SELECT DISTINCT e.entity_id, e.value, e.dso_type_schema_version, e.value_version, e.created_at, e.last_updated_at
                FROM main.entities e
                {joinSql}
                WHERE e.entity_type_id = @source_entity_type_id
                  AND e.pool_id = @pool_id
                  AND {whereClause}
                ORDER BY e.entity_id
                LIMIT @limit OFFSET @offset
                """;

            cmd.CommandText = mainQuery;
            Log.ExecutingQuery(logger, mainQuery);

            var items = new List<MetadataEnvelope>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var entityId = Guid.Parse(reader.GetString(0));
                    var jsonValue = reader.GetString(1);
                    var dsoTypeVersion = reader.GetInt32(2);
                    var valueVersion = reader.GetInt32(3);
                    var created = DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind);
                    var lastUpdated = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);

                    var version = new DataStorageObjectVersion(query.SourceEntityType, (uint)dsoTypeVersion);
                    var dsoType = dataStorageTypeRegistry.Get(version);
                    var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                    items.Add(new MetadataEnvelope(item, entityId, valueVersion, created, lastUpdated));
                }
            }

            // Count query
            var countQuery = $"""
                SELECT COUNT(DISTINCT e.entity_id)
                FROM main.entities e
                {joinSql}
                WHERE e.entity_type_id = @source_entity_type_id
                  AND e.pool_id = @pool_id
                  AND {whereClause}
                """;

            await using var countCmd = cnn.CreateCommand();
            _ = countCmd.Parameters.AddWithValue("@source_entity_type_id", sourceEntityTypeId);
            _ = countCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            if (query.WhereEntityId is not null)
            {
                _ = countCmd.Parameters.AddWithValue("@where_entity_id", query.WhereEntityId.Value.ToString());
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
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private static (StringBuilder JoinSql, string LastFilterColumn) BuildLinkJoins(
        LinkQueryDescriptor query,
        SqliteCommand cmd)
    {
        var joinSql = new StringBuilder();
        var whereLastJoin = "";

        for (var i = 0; i < query.Joins.Count; i++)
        {
            var join = query.Joins[i];
            var linkTypeParam = $"@lt{i}";
            _ = cmd.Parameters.AddWithValue(linkTypeParam, (int)join.Definition.Link.Id);

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
                _ = joinSql.AppendLine(CultureInfo.InvariantCulture,
                    $"JOIN main.entity_links l0 ON l0.{sourceSide} = e.entity_id AND l0.link_type_id = {linkTypeParam} AND l0.pool_id = @pool_id");
            }
            else
            {
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
                    $"JOIN main.entity_links l{i} ON l{i}.{sourceSide} = l{i - 1}.{prevFilterSide} AND l{i}.link_type_id = {linkTypeParam} AND l{i}.pool_id = @pool_id");
            }

            if (i == query.Joins.Count - 1)
            {
                whereLastJoin = $"l{i}.{filterSide}";
            }
        }

        return (joinSql, whereLastJoin);
    }

    private static string BuildAnchorInClause(SqliteCommand cmd, IReadOnlySet<Storage.UuidV7> ids)
    {
        var idParams = new List<string>(ids.Count);
        var i = 0;
        foreach (var id in ids)
        {
            var paramName = $"@anchor{i}";
            idParams.Add(paramName);
            _ = cmd.Parameters.AddWithValue(paramName, id.Value.ToString());
            i++;
        }

        return string.Join(", ", idParams);
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

            await using var cnn = await OpenConnectionAsync(ct);
            await using var cmd = cnn.CreateCommand();

            var (joinSql, anchorColumn) = BuildLinkJoins(query, cmd);
            var inClause = BuildAnchorInClause(cmd, query.WhereEntityIds);

            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@source_entity_type_id", (int)query.SourceEntityType.Id);
            _ = cmd.Parameters.AddWithValue("@offset", skip);
            _ = cmd.Parameters.AddWithValue("@limit", take + 1);

            // Dedupe and page on ids only, then fetch the values for the page, so the sort doesn't carry `value` for every skipped row.
            cmd.CommandText = $"""
                SELECT p.anchor_id, ent.entity_id, ent.value, ent.dso_type_schema_version, ent.value_version, ent.created_at, ent.last_updated_at
                FROM (
                    SELECT DISTINCT {anchorColumn} AS anchor_id, e.entity_id
                    FROM main.entities e
                    {joinSql}
                    WHERE e.entity_type_id = @source_entity_type_id
                      AND e.pool_id = @pool_id
                      AND {anchorColumn} IN ({inClause})
                    ORDER BY anchor_id, e.entity_id
                    LIMIT @limit OFFSET @offset
                ) p
                JOIN main.entities ent ON ent.entity_id = p.entity_id AND ent.pool_id = @pool_id AND ent.entity_type_id = @source_entity_type_id
                ORDER BY p.anchor_id, ent.entity_id
                """;
            Log.ExecutingQuery(logger, cmd.CommandText);

            var items = new List<LinkedEnvelope>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var anchorId = Storage.UuidV7.From(Guid.Parse(reader.GetString(0)));
                    var entityId = Guid.Parse(reader.GetString(1));
                    var jsonValue = reader.GetString(2);
                    var dsoTypeVersion = reader.GetInt32(3);
                    var valueVersion = reader.GetInt32(4);
                    var created = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);
                    var lastUpdated = DateTimeOffset.Parse(reader.GetString(6), null, DateTimeStyles.RoundtripKind);

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
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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

            await using var cnn = await OpenConnectionAsync(ct);
            await using var cmd = cnn.CreateCommand();

            var (joinSql, anchorColumn) = BuildLinkJoins(query, cmd);
            var inClause = BuildAnchorInClause(cmd, query.WhereEntityIds);

            _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = cmd.Parameters.AddWithValue("@source_entity_type_id", (int)query.SourceEntityType.Id);

            cmd.CommandText = $"""
                SELECT {anchorColumn} AS anchor_id, COUNT(DISTINCT e.entity_id)
                FROM main.entities e
                {joinSql}
                WHERE e.entity_type_id = @source_entity_type_id
                  AND e.pool_id = @pool_id
                  AND {anchorColumn} IN ({inClause})
                GROUP BY {anchorColumn}
                """;
            Log.ExecutingSql(logger, cmd.CommandText);

            var counts = query.WhereEntityIds.ToDictionary(id => id, _ => 0L);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                counts[Storage.UuidV7.From(Guid.Parse(reader.GetString(0)))] = reader.GetInt64(1);
            }

            return counts;
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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
            await using var cnn = await OpenConnectionAsync(ct);
            await using var cmd = cnn.CreateCommand();

            string whereClause;
            if (filter is null or AllExpression)
            {
                whereClause = "1=1";
            }
            else
            {
                var whereBuilder = new SqlWhereClauseBuilder(SchemaName, cmd, Dialect, LinkFilterTableNames, entityType);
                whereClause = whereBuilder.BuildWhereClause(filter);
            }

            var query = $"""
                SELECT COUNT(*)
                FROM {SchemaName}.entities v
                WHERE v.entity_type_id = @entity_type_id
                  AND v.pool_id = @pool_id
                  AND ({whereClause})
                """;
            var entityTypeId = (int)entityType.Id;
            _ = cmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
            Dialect.AddParameter(cmd, "@pool_id", PoolId.Value);

            cmd.CommandText = query;

            Log.ExecutingSql(logger, query);

            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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

            var now = timeProvider.GetUtcNow().UtcDateTime.ToString("O");

            await using var cnn = await OpenConnectionAsync(ct);
            await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

            // Step 1: Select expired rows (SQLite doesn't have FOR UPDATE SKIP LOCKED, but
            // the transaction provides database-level locking)
            var expired = new List<(string PoolId, int EntityTypeId, string EntityId, string EntityTypeName, string Value, int? DsoTypeSchemaVersion, string EventId)>();
            await using (var selectCmd = cnn.CreateCommand())
            {
                selectCmd.Transaction = tx;
                selectCmd.CommandText = """
                    SELECT pool_id, entity_type_id, entity_id, entity_type_name, value, dso_type_schema_version
                    FROM main.entities
                    WHERE expires_at IS NOT NULL AND expires_at <= @now
                    LIMIT @batchSize
                    """;
                _ = selectCmd.Parameters.AddWithValue("@now", now);
                _ = selectCmd.Parameters.AddWithValue("@batchSize", batchSize);
                Log.ExecutingSql(logger, selectCmd.CommandText);
                await using var reader = await selectCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var dsoTypeSchemaVersion = await reader.IsDBNullAsync(5, ct) ? null : (int?)reader.GetInt32(5);
                    expired.Add((
                        reader.GetString(0),
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        dsoTypeSchemaVersion,
                        Guid.CreateVersion7().ToString()));
                }
            }

            if (expired.Count == 0)
            {
                return 0;
            }

            // Step 2: Insert outbox events per matching subscription
            if (!outboxSubscriptions.IsEmpty)
            {
                var eventName = OutboxEventName.EntityExpired;
                foreach (var subscription in outboxSubscriptions.Subscriptions)
                {
                    if (subscription.EventNames.Count > 0 && !subscription.EventNames.Contains(eventName))
                    {
                        continue;
                    }

                    var matchingExpired = subscription.EntityTypeIds.Count > 0
                        ? expired.Where(e => subscription.EntityTypeIds.Contains(e.EntityTypeId))
                        : expired;

                    foreach (var (poolId, entityTypeId, entityId, entityTypeName, value, dsoTypeSchemaVersion, eventId) in matchingExpired)
                    {
                        await using var outboxCmd = cnn.CreateCommand();
                        outboxCmd.Transaction = tx;
                        outboxCmd.CommandText = """
                            INSERT INTO main.outbox_subscriber_queue
                            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version)
                            VALUES (@message_id, @event_id, @timestamp, @event_name, @subject_id, @entity_type_id, @entity_type_name, @pool_id, @payload, @subscriber_name, @dso_type_schema_version)
                            """;
                        _ = outboxCmd.Parameters.AddWithValue("@message_id", Guid.CreateVersion7().ToString());
                        _ = outboxCmd.Parameters.AddWithValue("@event_id", eventId);
                        _ = outboxCmd.Parameters.AddWithValue("@timestamp", now);
                        _ = outboxCmd.Parameters.AddWithValue("@event_name", eventName.ToString());
                        _ = outboxCmd.Parameters.AddWithValue("@subject_id", entityId);
                        _ = outboxCmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
                        _ = outboxCmd.Parameters.AddWithValue("@entity_type_name", entityTypeName);
                        _ = outboxCmd.Parameters.AddWithValue("@pool_id", poolId);
                        _ = outboxCmd.Parameters.AddWithValue("@payload", value);
                        _ = outboxCmd.Parameters.AddWithValue("@subscriber_name", subscription.SubscriberName.ToString());
                        _ = outboxCmd.Parameters.AddWithValue("@dso_type_schema_version", dsoTypeSchemaVersion.HasValue ? dsoTypeSchemaVersion.Value : DBNull.Value);
                        Log.ExecutingSql(logger, outboxCmd.CommandText);
                        _ = await outboxCmd.ExecuteNonQueryAsync(ct);
                    }
                }
            }

            // Step 3: Delete all links touching an expired entity, on either side. entity_links no
            // longer cascades in the database (both FKs are NO ACTION), so purge must remove these
            // explicitly; restricted links are removed too — purging an expired entity overrides
            // Restrict.
            foreach (var (poolId, entityTypeId, entityId, _, _, _, _) in expired)
            {
                await using var linkCmd = cnn.CreateCommand();
                linkCmd.Transaction = tx;
                linkCmd.CommandText = """
                    DELETE FROM main.entity_links
                    WHERE pool_id = @pool_id
                      AND (
                        (left_entity_type_id = @entity_type_id AND left_entity_id = @entity_id)
                        OR (right_entity_type_id = @entity_type_id AND right_entity_id = @entity_id)
                      )
                    """;
                _ = linkCmd.Parameters.AddWithValue("@pool_id", poolId);
                _ = linkCmd.Parameters.AddWithValue("@entity_id", entityId);
                _ = linkCmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
                Log.ExecutingSql(logger, linkCmd.CommandText);
                _ = await linkCmd.ExecuteNonQueryAsync(ct);
            }

            // Step 4: Delete entities (entity_keys and search_values cascade)
            var deleted = 0;
            foreach (var (poolId, entityTypeId, entityId, _, _, _, _) in expired)
            {
                await using var deleteCmd = cnn.CreateCommand();
                deleteCmd.Transaction = tx;
                deleteCmd.CommandText = """
                    DELETE FROM main.entities
                    WHERE pool_id = @pool_id AND entity_type_id = @entity_type_id AND entity_id = @entity_id
                      AND expires_at <= @now
                    """;
                _ = deleteCmd.Parameters.AddWithValue("@pool_id", poolId);
                _ = deleteCmd.Parameters.AddWithValue("@entity_type_id", entityTypeId);
                _ = deleteCmd.Parameters.AddWithValue("@entity_id", entityId);
                _ = deleteCmd.Parameters.AddWithValue("@now", now);
                Log.ExecutingSql(logger, deleteCmd.CommandText);
                deleted += await deleteCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return deleted;
        }
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
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

            await using var cnn = await OpenConnectionAsync(ct);

            // Drain entity_links completely before any entity batch: entity_links no longer
            // cascades in the database (both FKs are NO ACTION), so purging it explicitly first
            // avoids a foreign key violation on the later entity deletes, and it must be fully
            // drained (not just one batch per entity batch) because an independent entity batch
            // could otherwise select an entity that still has links left over from a batch size
            // smaller than its in-degree.
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

                int batchLinks;
                await using (var linksCmd = cnn.CreateCommand())
                {
                    linksCmd.Transaction = tx;
                    linksCmd.CommandText = """
                        DELETE FROM main.entity_links WHERE rowid IN (
                            SELECT rowid FROM main.entity_links WHERE pool_id = @pool_id LIMIT @batch_size
                        )
                        """;
                    _ = linksCmd.Parameters.AddWithValue("@pool_id", poolId.Value);
                    _ = linksCmd.Parameters.AddWithValue("@batch_size", batchSize);
                    Log.ExecutingSql(logger, linksCmd.CommandText);
                    batchLinks = await linksCmd.ExecuteNonQueryAsync(ct);
                }

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

                await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

                int batchOutbox;
                int batchEntities;

                await using (var outboxCmd = cnn.CreateCommand())
                {
                    outboxCmd.Transaction = tx;
                    outboxCmd.CommandText = """
                        DELETE FROM main.outbox_subscriber_queue WHERE rowid IN (
                            SELECT rowid FROM main.outbox_subscriber_queue WHERE pool_id = @pool_id LIMIT @batch_size
                        )
                        """;
                    _ = outboxCmd.Parameters.AddWithValue("@pool_id", poolId.Value);
                    _ = outboxCmd.Parameters.AddWithValue("@batch_size", batchSize);
                    Log.ExecutingSql(logger, outboxCmd.CommandText);
                    batchOutbox = await outboxCmd.ExecuteNonQueryAsync(ct);
                }

                await using (var entitiesCmd = cnn.CreateCommand())
                {
                    entitiesCmd.Transaction = tx;
                    entitiesCmd.CommandText = """
                        DELETE FROM main.entities WHERE rowid IN (
                            SELECT rowid FROM main.entities WHERE pool_id = @pool_id LIMIT @batch_size
                        )
                        """;
                    _ = entitiesCmd.Parameters.AddWithValue("@pool_id", poolId.Value);
                    _ = entitiesCmd.Parameters.AddWithValue("@batch_size", batchSize);
                    Log.ExecutingSql(logger, entitiesCmd.CommandText);
                    batchEntities = await entitiesCmd.ExecuteNonQueryAsync(ct);
                }

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
        catch (SqliteException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    private sealed record QueryClauses(string WhereClause, string JoinClause, string OrderByClause);

    private sealed record CursorQueryClauses(
        string WhereClause,
        string JoinClause,
        string OrderByClause,
        string SeekClause,
        string SortColumnName);

    private sealed record SchemaComment(int Version);
}
