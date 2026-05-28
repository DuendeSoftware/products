// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.Sqlite.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
internal sealed class SqliteStore(
    SqliteStoreOptions options,
    DataStorageTypeRegistry dataStorageTypeRegistry,
    TimeProvider timeProvider,
    OutboxSubscribers outboxSubscribers,
    ILogger<SqliteStore> logger) : StoreBase, IStore, IDatabaseSchema
{
    private const int RequiredSchemaVersion = 1;
    private static readonly SqliteDialect Dialect = new();

    // SQLite's default schema is "main", used for SqlWhereClauseBuilder schema-qualified table names
    private const string SchemaName = "main";

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

    async Task<CheckSchemaVersionResult> IDatabaseSchema.CheckVersionAsync(Ct ct)
    {
        Log.CheckingSchemaVersion(logger);

        await using var connection = await OpenConnectionAsync(ct);

        // Check if __schema_info table exists
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='__schema_info'";
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

    async Task IDatabaseSchema.MigrateAsync(Ct ct)
    {
        Log.MigratingSchema(logger, "sqlite");

        var versionResult = await ((IDatabaseSchema)this).CheckVersionAsync(ct);
        var currentVersion = new DatabaseSchemaVersion((int)versionResult.CurrentVersion);

        await using var connection = await OpenConnectionAsync(ct);

        foreach (var (_, sql) in MigrationScriptLoader.GetScripts(typeof(SqliteStore).Assembly, currentVersion))
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                Log.ExecutingSql(logger, cmd.CommandText);
                _ = await cmd.ExecuteNonQueryAsync(ct);

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        var verifyResult = await ((IDatabaseSchema)this).VerifySchemaAsync(ct);
        if (!verifyResult.IsValid)
        {
            var errors = string.Join("; ", verifyResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Schema verification failed after migration: {errors}");
        }
    }

    async Task<SchemaVerificationResult> IDatabaseSchema.VerifySchemaAsync(Ct ct)
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
                ("subscriber_name", "TEXT")
            ]
        };

        // Check each expected table
        foreach (var (tableName, expectedColumns) in expectedTables)
        {
            await using var tableCmd = connection.CreateCommand();
            tableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@name";
            _ = tableCmd.Parameters.AddWithValue("@name", tableName);
            var tableExists = await tableCmd.ExecuteScalarAsync(ct);

            if (tableExists is null)
            {
                errors.Add(new SchemaVerificationError(tableName, null, $"Table '{tableName}' is missing.", SchemaVerificationErrorKind.MissingTable));
                continue;
            }

            // Check columns via PRAGMA table_info
            await using var colCmd = connection.CreateCommand();
            colCmd.CommandText = $"PRAGMA table_info({tableName})";
            await using var colReader = await colCmd.ExecuteReaderAsync(ct);

            var actualColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (await colReader.ReadAsync(ct))
            {
                var colName = colReader.GetString(1);
                var colType = colReader.GetString(2).ToUpperInvariant();
                actualColumns[colName] = colType;
            }

            foreach (var (colName, typeHint) in expectedColumns)
            {
                if (!actualColumns.TryGetValue(colName, out var actualType))
                {
                    errors.Add(new SchemaVerificationError(tableName, colName, $"Column '{tableName}.{colName}' is missing.", SchemaVerificationErrorKind.MissingColumn));
                }
                else if (!actualType.Contains(typeHint, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new SchemaVerificationError(tableName, colName, $"Column '{tableName}.{colName}' has type '{actualType}', expected '{typeHint}'.", SchemaVerificationErrorKind.WrongType));
                }
            }
        }

        // Verify required indexes
        var expectedIndexes = new (string Table, string Index)[]
        {
            ("entities", "entities_expires_at_index"),
            ("entities", "entities_created_at_index"),
            ("entities", "entities_last_updated_at_index"),
            ("entity_keys", "entity_keys_entity_type_id_entity_id_index"),
            ("search_values", "search_values_string_value_index"),
            ("search_values", "search_values_number_value_index"),
            ("search_values", "search_values_datetime_value_index"),
            ("search_values", "search_values_boolean_value_index"),
            ("search_values", "search_values_array_string_value_index"),
            ("search_values", "search_values_array_number_value_index"),
            ("search_values", "search_values_array_datetime_value_index"),
            ("search_values", "search_values_array_boolean_value_index"),
            ("search_values", "search_values_guid_value_index"),
            ("search_values", "search_values_array_guid_value_index"),
            ("entity_links", "entity_links_left_entity_index"),
            ("entity_links", "entity_links_right_entity_index"),
            ("entity_links", "entity_links_left_cascade_index"),
            ("entity_links", "entity_links_right_cascade_index"),
            ("outbox_subscriber_queue", "outbox_subscriber_queue_subscriber_index"),
        };

        {
            await using var idxCmd = connection.CreateCommand();
            idxCmd.CommandText = "SELECT tbl_name, name FROM sqlite_master WHERE type='index' AND name IS NOT NULL";
            await using var idxReader = await idxCmd.ExecuteReaderAsync(ct);

            var actualIndexes = new HashSet<(string Table, string Index)>(
                EqualityComparer<(string, string)>.Create(
                    (a, b) => string.Equals(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase),
                    x => StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item1) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item2)));

            while (await idxReader.ReadAsync(ct))
            {
                _ = actualIndexes.Add((idxReader.GetString(0), idxReader.GetString(1)));
            }

            foreach (var (tableName, indexName) in expectedIndexes)
            {
                if (!actualIndexes.Contains((tableName, indexName)))
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, null,
                        $"Index '{indexName}' is missing from table '{tableName}'.",
                        SchemaVerificationErrorKind.MissingIndex));
                }
            }
        }

        // Verify required foreign keys
        var expectedForeignKeys = new (string Table, string ReferencedTable)[]
        {
            ("entity_keys", "entities"),
            ("search_values", "entities"),
        };

        foreach (var (tableName, referencedTable) in expectedForeignKeys)
        {
            await using var fkCmd = connection.CreateCommand();
            fkCmd.CommandText = $"PRAGMA foreign_key_list({tableName})";
            await using var fkReader = await fkCmd.ExecuteReaderAsync(ct);

            var referencedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await fkReader.ReadAsync(ct))
            {
                _ = referencedTables.Add(fkReader.GetString(2)); // column 2 is "table" (referenced table)
            }

            if (!referencedTables.Contains(referencedTable))
            {
                errors.Add(new SchemaVerificationError(
                    tableName, null,
                    $"Foreign key from '{tableName}' to '{referencedTable}' is missing.",
                    SchemaVerificationErrorKind.MissingForeignKey));
            }
        }

        return new SchemaVerificationResult(errors);
    }

    string IDatabaseSchema.BuildMigrationScript(DatabaseSchemaVersion fromVersion)
    {
        var sb = new StringBuilder();
        foreach (var (targetVersion, sql) in MigrationScriptLoader.GetScripts(typeof(SqliteStore).Assembly, fromVersion))
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"-- Migration step: V{targetVersion - 1} → V{targetVersion}");
            _ = sb.AppendLine(sql);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a new entity in the store.
    /// </summary>
    async Task<CreateResult> IStore.CreateAsync<TDso>(
        Storage.UuidV7 id,
        TDso dso,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
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

    async Task<StoreGetResult> IStore.TryReadAsync(
        EntityType entityType,
        Storage.UuidV7 id,
        Ct ct)
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

    async Task<StoreGetResult> IStore.TryReadAsync(
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
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

    async Task<IReadOnlyList<StoreGetResult>> IStore.TryReadManyAsync(
        EntityType entityType,
        IReadOnlySet<Storage.UuidV7> ids,
        int maximum,
        Ct ct)
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

    /// <summary>
    /// Updates an existing entity in the store.
    /// </summary>
    async Task<UpdateResult> IStore.UpdateAsync<TDso>(
        Storage.UuidV7 id,
        TDso dso,
        int expectedEntityVersion,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration? expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
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

    async Task<DeleteResult> IStore.DeleteAsync(EntityType entityType, Storage.UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        var deleteOp = DeleteOperation.ById(entityType, id);

        await using var cnn = await OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

        var (_, entityDeleted) = await ExecuteDeleteCoreAsync(cnn, tx, deleteOp, ct);

        if (entityDeleted && outboxEvents is { Count: > 0 })
        {
            await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
        }

        await tx.CommitAsync(ct);
        return DeleteResult.Success;
    }

    async Task<DeleteResult> IStore.DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        var deleteOp = DeleteOperation.ByKey(entityType, key);

        await using var cnn = await OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

        var (_, entityDeleted) = await ExecuteDeleteCoreAsync(cnn, tx, deleteOp, ct);

        if (entityDeleted && outboxEvents is { Count: > 0 })
        {
            await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
        }

        await tx.CommitAsync(ct);
        return DeleteResult.Success;
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
    async Task<QueryResult<MetadataEnvelope<TDso>>> IStore.QueryAsync<TDso>(
        EntityType entityType,
        IQueryExpression filter,
        SortParameter sort,
        DataRange dataRange,
        Ct ct)
    {
        if (dataRange.TokenValue is not null)
        {
            return await QueryCursorCoreAsync<TDso>(entityType, filter, sort, dataRange.TokenValue, ct);
        }

        var (skip, take) = ResolveOffsetAndSize(dataRange);
        var dsoVersion = TDso.DsoVersion;
        var entityTypeId = (int)entityType.Id;

        Log.QueryingDsos(logger, entityType, skip, take);

        await using var cnn = await OpenConnectionAsync(ct);

        await using var cmd = cnn.CreateCommand();
        var queryClauses = BuildQueryClauses(cmd, filter, sort);

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

        var items = new List<MetadataEnvelope<TDso>>();
        var totalCount = 0;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
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
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                var valueVersion = reader.GetInt32(3);
                var created = DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind);
                var lastUpdated = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);
                items.Add(new MetadataEnvelope<TDso>(item, entityId, valueVersion, created, lastUpdated));
            }
        }

        return new QueryResult<MetadataEnvelope<TDso>>
        {
            Items = items,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / take),
            HasMoreData = skip + take < totalCount
        };
    }

    /// <summary>
    /// Queries for specific field values with the specified pagination strategy.
    /// </summary>
    async Task<QueryResult<ProjectedResult>> IStore.QueryFieldsAsync(
        EntityType entityType,
        IReadOnlyCollection<Field> fields,
        IQueryExpression filter,
        SortParameter sort,
        DataRange dataRange,
        Ct ct)
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
        var queryClauses = BuildQueryClauses(cmd, filter, sort);

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
            HasMoreData = skip + take < totalCount
        };
    }

    /// <summary>
    /// Cursor-based query core for full entities.
    /// </summary>
    private async Task<QueryResult<MetadataEnvelope<TDso>>> QueryCursorCoreAsync<TDso>(
        EntityType entityType,
        IQueryExpression filter,
        SortParameter sort,
        ContinuationTokenDataRange tokenRange,
        Ct ct) where TDso : IDataStorageObject
    {
        ArgumentNullException.ThrowIfNull(sort);
        if (sort.IsEmpty)
        {
            throw new ArgumentException("Sort parameter is required for cursor-based pagination.", nameof(sort));
        }

        var pageSize = tokenRange.Size.Value;
        var dsoVersion = TDso.DsoVersion;
        var entityTypeId = (int)entityType.Id;

        Log.QueryingDsos(logger, entityType, 0, pageSize);

        await using var cnn = await OpenConnectionAsync(ct);

        await using var cmd = cnn.CreateCommand();
        var queryClauses = BuildCursorQueryClauses(cmd, entityTypeId, filter, sort, tokenRange);

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

        var items = new List<(Guid Id, TDso Item, int Version, DateTimeOffset Created, DateTimeOffset LastUpdated, object? SortValue)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
            while (await reader.ReadAsync(ct))
            {
                var entityId = Guid.Parse(reader.GetString(0));
                var jsonValue = reader.GetString(1);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                var valueVersion = reader.GetInt32(3);
                var created = DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind);
                var lastUpdated = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);
                var sortValue = await ReadSortValueAsync(reader, sort.Field!, 6, ct);
                items.Add((entityId, item, valueVersion, created, lastUpdated, sortValue));
            }
        }

        var hasMore = items.Count > pageSize;
        var pageItems = items.Take(pageSize).ToList();

        ContinuationToken? nextToken = null;
        if (pageItems.Count > 0)
        {
            var lastItem = pageItems[^1];
            var token = CreateCursorToken(lastItem.Id, lastItem.SortValue);
            nextToken = (ContinuationToken)token.Encode();
        }

        var resultItems = pageItems.Select(x => new MetadataEnvelope<TDso>(x.Item, x.Id, x.Version, x.Created, x.LastUpdated)).ToList();
        return new QueryResult<MetadataEnvelope<TDso>>
        {
            Items = resultItems,
            NextToken = nextToken,
            HasMoreData = hasMore
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
        ArgumentNullException.ThrowIfNull(sort);
        if (sort.IsEmpty)
        {
            throw new ArgumentException("Sort parameter is required for cursor-based pagination.", nameof(sort));
        }

        var pageSize = tokenRange.Size.Value;
        var entityTypeId = (int)entityType.Id;

        Log.QueryingFieldsDsos(logger, entityType, fields.Count, 0, pageSize);

        await using var cnn = await OpenConnectionAsync(ct);

        await using var cmd = cnn.CreateCommand();
        var queryClauses = BuildCursorQueryClauses(cmd, entityTypeId, filter, sort, tokenRange);

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

                    var sortValue = await ReadSortValueAsync(reader, sort.Field!, 7, ct);
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

        var itemsList = orderedIds.Select(id => (Id: id, resultsByid[id])).ToList();
        var hasMore = itemsList.Count > pageSize;
        var pageItems = itemsList.Take(pageSize).ToList();

        ContinuationToken? nextToken = null;
        if (pageItems.Count > 0)
        {
            var lastItem = pageItems[^1];
            var token = CreateCursorToken(lastItem.Id, lastItem.Item2.SortValue);
            nextToken = (ContinuationToken)token.Encode();
        }

        var items = pageItems
            .Select(item => new ProjectedResult(item.Id, item.Item2.FieldValues))
            .ToList();

        return new QueryResult<ProjectedResult>
        {
            Items = items,
            NextToken = nextToken,
            HasMoreData = hasMore
        };
    }

    /// <summary>
    /// Builds the WHERE clause, ORDER BY clause, and JOIN clause for a query.
    /// </summary>
    private static QueryClauses BuildQueryClauses(
        SqliteCommand cmd,
        IQueryExpression filter,
        SortParameter sort)
    {
        var whereBuilder = new SqlWhereClauseBuilder(SchemaName, cmd, Dialect);
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
        int entityTypeId,
        IQueryExpression filter,
        SortParameter sort,
        ContinuationTokenDataRange tokenRange)
    {
        var whereBuilder = new SqlWhereClauseBuilder(SchemaName, cmd, Dialect);
        var whereClause = whereBuilder.BuildWhereClause(filter);

        var sortFieldPath = sort.Field!.Path;
        var sortDirection = sort.Direction == SortDirection.Ascending ? "ASC" : "DESC";
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

        var orderByClause = $"""
            ORDER BY
              CASE WHEN {sortColumn} IS NULL THEN 1 ELSE 0 END,
              {sortColumn} {sortDirection},
              v.entity_id ASC
            """;

        // Build seek clause for cursor position
        var seekClause = "";
        var tokenValue = tokenRange.Start.Value;
        if (tokenValue != ContinuationToken.Beginning)
        {
            var decodedToken = CursorToken.Decode(tokenValue);
            if (decodedToken != null)
            {
                var lastSortParam = "@last_sort_value";
                var lastIdParam = "@last_id";

                if (decodedToken.GuidValue.HasValue)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.GuidValue.Value.ToString());
                }
                else if (decodedToken.StringValue != null)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.StringValue);
                }
                else if (decodedToken.NumberValue.HasValue)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, (double)decodedToken.NumberValue.Value);
                }
                else if (decodedToken.DateTimeValue.HasValue)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.DateTimeValue.Value.UtcDateTime.ToString("O"));
                }
                else if (decodedToken.BooleanValue.HasValue)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.BooleanValue.Value ? 1 : 0);
                }
                else
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, DBNull.Value);
                }

                _ = cmd.Parameters.AddWithValue(lastIdParam, decodedToken.Id.ToString());

                if (sort.Direction == SortDirection.Ascending)
                {
                    // With NULLS LAST in ascending: non-NULL values first, then NULLs
                    // If last value was non-NULL, include rows with greater value OR NULL values
                    // If last value was NULL, only include NULLs with greater ID
                    seekClause = $"""
                        AND (
                          ({sortColumn} > {lastSortParam} OR ({sortColumn} = {lastSortParam} AND v.entity_id > {lastIdParam}))
                          OR ({sortColumn} IS NULL AND {lastSortParam} IS NOT NULL)
                          OR ({sortColumn} IS NULL AND {lastSortParam} IS NULL AND v.entity_id > {lastIdParam})
                        )
                        """;
                }
                else
                {
                    // With NULLS LAST in descending: non-NULL values (descending), then NULLs
                    // If last value was non-NULL, include rows with lesser value OR NULL values
                    // If last value was NULL, only include NULLs with greater ID
                    seekClause = $"""
                        AND (
                          ({sortColumn} < {lastSortParam} OR ({sortColumn} = {lastSortParam} AND v.entity_id > {lastIdParam}))
                          OR ({sortColumn} IS NULL AND {lastSortParam} IS NOT NULL)
                          OR ({sortColumn} IS NULL AND {lastSortParam} IS NULL AND v.entity_id > {lastIdParam})
                        )
                        """;
                }
            }
        }

        return new CursorQueryClauses(whereClause, joinClause, orderByClause, seekClause, sortColumn);
    }

    /// <summary>
    /// Creates a cursor token from a sort value and entity ID.
    /// </summary>
    private static CursorToken CreateCursorToken(Guid id, object? sortValue) =>
        sortValue switch
        {
            string s => CursorToken.Create(id, s, null, null, null, null),
            decimal d => CursorToken.Create(id, null, d, null, null, null),
            DateTime dt => CursorToken.Create(id, null, null, new DateTimeOffset(dt, TimeSpan.Zero), null, null),
            DateTimeOffset dto => CursorToken.Create(id, null, null, dto, null, null),
            bool b => CursorToken.Create(id, null, null, null, b, null),
            Guid g => CursorToken.Create(id, null, null, null, null, g),
            null => CursorToken.Create(id, null, null, null, null, null),
            _ => throw new InvalidOperationException($"Unsupported sort value type: {sortValue.GetType().Name}")
        };

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
            NumberField => Convert.ToDecimal(reader.GetValue(columnIndex), CultureInfo.InvariantCulture),
            DateTimeField => DateTimeOffset.Parse(reader.GetString(columnIndex), CultureInfo.InvariantCulture),
            BooleanField => reader.GetInt64(columnIndex) != 0,
            GuidField or ExactMatchField => Guid.Parse(reader.GetString(columnIndex)),
            _ => throw new InvalidOperationException($"Unsupported field type for sorting: {sortField.GetType().Name}")
        };
    }

    /// <inheritdoc/>
    async Task<LinkResult> IStore.LinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
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
        return outcome == OperationOutcome.AlreadyLinked ? LinkResult.AlreadyLinked : LinkResult.Success;
    }

    /// <inheritdoc/>
    async Task<UnlinkResult> IStore.UnlinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
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

    private async Task<OperationOutcome> ExecuteLinkCoreAsync(
        SqliteConnection cnn,
        SqliteTransaction tx,
        LinkOperation op,
        Ct ct)
    {

        await using var cmd = cnn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR IGNORE INTO main.entity_links (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id)
            VALUES (@pool_id, @link_type_id, @left_entity_type_id, @left_entity_id, @right_entity_type_id, @right_entity_id)
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@link_type_id", (int)op.Definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_type_id", (int)op.Definition.Left.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", op.LeftEntityId.Value.ToString());
        _ = cmd.Parameters.AddWithValue("@right_entity_type_id", (int)op.Definition.Right.Id);
        _ = cmd.Parameters.AddWithValue("@right_entity_id", op.RightEntityId.Value.ToString());

        Log.ExecutingSql(logger, cmd.CommandText);

        var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
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
        var rows = new List<(OutboxEvent Evt, IOutboxSubscriber Subscriber)>();
        foreach (var evt in outboxEvents)
        {
            foreach (var subscriber in outboxSubscribers.GetMatchingSubscribers(evt.EventName, evt.EntityTypeId))
            {
                rows.Add((evt, subscriber));
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
            var (evt, subscriber) = rows[i];
            valueRows.Add($"(@message_id{i}, @event_id{i}, @timestamp{i}, @event_name{i}, @subject_id{i}, @entity_type_id{i}, @entity_type_name{i}, @pool_id, @payload{i}, @subscriber_name{i})");
            _ = cmd.Parameters.AddWithValue($"@message_id{i}", Guid.CreateVersion7().ToString());
            _ = cmd.Parameters.AddWithValue($"@event_id{i}", evt.Id.Value.ToString());
            _ = cmd.Parameters.AddWithValue($"@timestamp{i}", evt.Timestamp.UtcDateTime.ToString("O"));
            _ = cmd.Parameters.AddWithValue($"@event_name{i}", evt.EventName.ToString());
            _ = cmd.Parameters.AddWithValue($"@subject_id{i}", evt.SubjectId.Value.ToString());
            _ = cmd.Parameters.AddWithValue($"@entity_type_id{i}", evt.EntityTypeId);
            _ = cmd.Parameters.AddWithValue($"@entity_type_name{i}", evt.EntityTypeName);
            _ = cmd.Parameters.AddWithValue($"@payload{i}", evt.Payload);
            _ = cmd.Parameters.AddWithValue($"@subscriber_name{i}", subscriber.SubscriberName.ToString());
        }
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        cmd.CommandText = $"""
            INSERT INTO main.outbox_subscriber_queue
            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name)
            VALUES
            {string.Join(",\n            ", valueRows)}
            """;

        Log.ExecutingSql(logger, cmd.CommandText);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Executes multiple operations atomically in a single transaction.
    /// </summary>
    async Task<BatchResult> IStore.ExecuteBatchAsync(
        IReadOnlyList<IStoreOperation> operations,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
    {
        if (operations.Count == 0)
        {
            return new BatchResult(true, []);
        }

        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var results = new List<OperationResult>();

        try
        {
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
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    async Task<OutboxEventsPage> IStore.GetOutboxEventsForSubscriberAsync(SubscriberName subscriberName, int count, Ct ct)
    {
        await using var cnn = await OpenConnectionAsync(ct);

        await using var cmd = cnn.CreateCommand();
        cmd.CommandText = """
            SELECT sequence_number, message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name
            FROM main.outbox_subscriber_queue
            WHERE subscriber_name = @subscriber_name
            ORDER BY sequence_number ASC
            LIMIT @limit
            """;
        _ = cmd.Parameters.AddWithValue("@limit", count + 1);
        _ = cmd.Parameters.AddWithValue("@subscriber_name", subscriberName.ToString());

        Log.ExecutingSql(logger, cmd.CommandText);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var events = new List<PersistedOutboxEvent>();
        while (await reader.ReadAsync(ct))
        {
            var timestamp = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture);
            events.Add(new PersistedOutboxEvent
            {
                SequenceNumber = reader.GetInt64(0),
                MessageId = Guid.Parse(reader.GetString(1)),
                EventId = Guid.Parse(reader.GetString(2)),
                Timestamp = timestamp,
                EventName = OutboxEventName.Create(reader.GetString(4)),
                SubjectId = Storage.UuidV7.From(Guid.Parse(reader.GetString(5))),
                EntityTypeId = reader.GetInt32(6),
                EntityTypeName = reader.GetString(7),
                PoolId = PoolId.Load(reader.GetInt32(8)),
                Payload = reader.GetString(9),
                SubscriberName = SubscriberName.Create(reader.GetString(10)),
            });
        }

        var hasMore = events.Count > count;
        if (hasMore)
        {
            events.RemoveAt(events.Count - 1);
        }

        return new OutboxEventsPage(events, hasMore);
    }

    async Task IStore.DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct)
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

        await using var deleteCmd = cnn.CreateCommand();
        deleteCmd.Transaction = tx;
        _ = deleteCmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = deleteCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        deleteCmd.CommandText = "DELETE FROM main.entities WHERE entity_type_id = @entity_type_id AND pool_id = @pool_id";

        if (op.Id is not null)
        {
            Log.DeletingDso(logger, entityType, op.Id.Value);
            _ = deleteCmd.Parameters.AddWithValue("@entity_id", op.Id.Value.ToString());

            deleteCmd.CommandText += " AND entity_id = @entity_id";
        }
        else if (op.Key is not null)
        {
            var key = op.Key;
            _ = deleteCmd.Parameters.AddWithValue("@key_type_id", (int)key.DskVersion.KeyType.Id);
            _ = deleteCmd.Parameters.AddWithValue("@key_type_version", (int)key.DskVersion.SchemaVersion);
            _ = deleteCmd.Parameters.AddWithValue("@key_value", key.Value.ToString());
            deleteCmd.CommandText += """
                  AND entity_id = (
                    SELECT entity_id FROM main.entity_keys
                    WHERE entity_type_id = @entity_type_id
                      AND key_type_id = @key_type_id
                      AND key_type_version = @key_type_version
                      AND key_value = @key_value
                      AND pool_id = @pool_id
                  )
                """;
        }
        else
        {
            return (OperationOutcome.Success, false);
        }

        deleteCmd.CommandText += " RETURNING entity_id";

        Log.ExecutingSql(logger, deleteCmd.CommandText);

        // Use RETURNING to get the deleted entity_id in a single round-trip
        var result = await deleteCmd.ExecuteScalarAsync(ct);
        var deletedEntityId = result is string s ? Guid.Parse(s) : (Guid?)null;

        // Delete entity links (no FK to entities, must be done manually)
        if (deletedEntityId.HasValue)
        {
            await using var linkDeleteCmd = cnn.CreateCommand();
            linkDeleteCmd.Transaction = tx;
            linkDeleteCmd.CommandText = """
                DELETE FROM main.entity_links
                WHERE pool_id = @pool_id
                  AND (left_entity_id = @entity_id OR right_entity_id = @entity_id)
                """;
            _ = linkDeleteCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = linkDeleteCmd.Parameters.AddWithValue("@entity_id", deletedEntityId.Value.ToString());
            Log.ExecutingSql(logger, linkDeleteCmd.CommandText);
            _ = await linkDeleteCmd.ExecuteNonQueryAsync(ct);
        }

        return (OperationOutcome.Success, deletedEntityId.HasValue);
    }



    /// <inheritdoc/>
    async Task<QueryResult<MetadataEnvelope<TDso>>> IStore.QueryLinksAsync<TDso>(
        LinkQueryDescriptor query,
        DataRange dataRange,
        Ct ct)
    {
        if (dataRange.TokenValue is not null)
        {
            throw new NotSupportedException("Cursor-based pagination is not supported for link queries.");
        }

        var (skip, take) = ResolveOffsetAndSize(dataRange);
        var dsoVersion = TDso.DsoVersion;
        var sourceEntityTypeId = (int)query.SourceEntityType.Id;

        await using var cnn = await OpenConnectionAsync(ct);

        await using var cmd = cnn.CreateCommand();

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

        var items = new List<MetadataEnvelope<TDso>>();
        var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = Guid.Parse(reader.GetString(0));
                var jsonValue = reader.GetString(1);
                var valueVersion = reader.GetInt32(3);
                var created = DateTimeOffset.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind);
                var lastUpdated = DateTimeOffset.Parse(reader.GetString(5), null, DateTimeStyles.RoundtripKind);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                items.Add(new MetadataEnvelope<TDso>(item, entityId, valueVersion, created, lastUpdated));
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

        return new QueryResult<MetadataEnvelope<TDso>>
        {
            Items = items,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / take),
            HasMoreData = skip + take < totalCount
        };
    }

    async Task<long> IStore.CountAsync(
        EntityType entityType,
        IQueryExpression? filter,
        Ct ct)
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
            var whereBuilder = new SqlWhereClauseBuilder(SchemaName, cmd, Dialect);
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

    async Task<int> IStore.PurgeExpiredAsync(int batchSize, Ct ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, StorageConstants.TtlCleanupMaxBatchSize);

        var now = timeProvider.GetUtcNow().UtcDateTime.ToString("O");

        await using var cnn = await OpenConnectionAsync(ct);
        await using var tx = (SqliteTransaction)await cnn.BeginTransactionAsync(ct);

        try
        {
            // Step 1: Select expired rows (SQLite doesn't have FOR UPDATE SKIP LOCKED, but
            // the transaction provides database-level locking)
            var expired = new List<(string PoolId, int EntityTypeId, string EntityId, string EntityTypeName, string Value, string EventId)>();
            await using (var selectCmd = cnn.CreateCommand())
            {
                selectCmd.Transaction = tx;
                selectCmd.CommandText = """
                    SELECT pool_id, entity_type_id, entity_id, entity_type_name, value
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
                    expired.Add((
                        reader.GetString(0),
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        Guid.CreateVersion7().ToString()));
                }
            }

            if (expired.Count == 0)
            {
                await tx.RollbackAsync(ct);
                return 0;
            }

            // Step 2: Insert outbox events per matching subscriber
            if (!outboxSubscribers.IsEmpty)
            {
                var eventName = OutboxEventName.EntityExpired;
                foreach (var subscriber in outboxSubscribers.Subscribers)
                {
                    if (subscriber.EventNames.Count > 0 && !subscriber.EventNames.Contains(eventName))
                    {
                        continue;
                    }

                    var matchingExpired = subscriber.EntityTypeIds.Count > 0
                        ? expired.Where(e => subscriber.EntityTypeIds.Contains(e.EntityTypeId))
                        : expired;

                    foreach (var (poolId, entityTypeId, entityId, entityTypeName, value, eventId) in matchingExpired)
                    {
                        await using var outboxCmd = cnn.CreateCommand();
                        outboxCmd.Transaction = tx;
                        outboxCmd.CommandText = """
                            INSERT INTO main.outbox_subscriber_queue
                            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name)
                            VALUES (@message_id, @event_id, @timestamp, @event_name, @subject_id, @entity_type_id, @entity_type_name, @pool_id, @payload, @subscriber_name)
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
                        _ = outboxCmd.Parameters.AddWithValue("@subscriber_name", subscriber.SubscriberName.ToString());
                        Log.ExecutingSql(logger, outboxCmd.CommandText);
                        _ = await outboxCmd.ExecuteNonQueryAsync(ct);
                    }
                }
            }

            // Step 3: Delete entity links for expired entities
            foreach (var (poolId, entityTypeId, entityId, _, _, _) in expired)
            {
                await using var linkCmd = cnn.CreateCommand();
                linkCmd.Transaction = tx;
                linkCmd.CommandText = """
                    DELETE FROM main.entity_links
                    WHERE pool_id = @pool_id
                      AND (
                            (left_entity_id = @entity_id AND left_entity_type_id = @entity_type_id)
                         OR (right_entity_id = @entity_id AND right_entity_type_id = @entity_type_id)
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
            foreach (var (poolId, entityTypeId, entityId, _, _, _) in expired)
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
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
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
