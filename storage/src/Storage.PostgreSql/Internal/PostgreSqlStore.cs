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
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.Storage.Schema;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using CursorToken = Duende.Storage.Internal.Querying.CursorToken;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.PostgreSql.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
internal sealed class PostgreSqlStore(
    NpgsqlDataSource dataSource,
    PostgreSqlStoreOptions options,
    DataStorageTypeRegistry dataStorageTypeRegistry,
    TimeProvider timeProvider,
    OutboxSubscribers outboxSubscribers,
    ILogger<PostgreSqlStore> logger) : StoreBase, IStore, IDatabaseSchema
{
    private const int RequiredSchemaVersion = 1;
    private static readonly ISqlDialect Dialect = new PostgreSqlDialect();

#pragma warning disable CA1308 // PostgreSQL folds unquoted identifiers to lowercase; we must match migration behavior
    private readonly string _schemaName = options.SchemaName.ToLowerInvariant();
    private readonly string _entities = FormatQualifiedTable(options.SchemaName, "entities");
    private readonly string _entityKeys = FormatQualifiedTable(options.SchemaName, "entity_keys");
    private readonly string _searchValues = FormatQualifiedTable(options.SchemaName, "search_values");
    private readonly string _entityLinks = FormatQualifiedTable(options.SchemaName, "entity_links");
    private readonly string _outboxSubscriberQueue = FormatQualifiedTable(options.SchemaName, "outbox_subscriber_queue");

    private static string FormatQualifiedTable(string schema, string table) =>
        $"{Dialect.QuoteIdentifier(schema.ToLowerInvariant())}.{Dialect.QuoteIdentifier(table)}";
#pragma warning restore CA1308

    async Task<CheckSchemaVersionResult> IDatabaseSchema.CheckVersionAsync(Ct ct)
    {
        Log.CheckingSchemaVersion(logger);

        await using var cmd = dataSource.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"SELECT CASE WHEN to_regnamespace('{_schemaName}') IS NOT NULL THEN obj_description(to_regnamespace('{_schemaName}')) END";
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

    async Task IDatabaseSchema.MigrateAsync(Ct ct)
    {
        Log.MigratingSchema(logger, _schemaName);

        var versionResult = await ((IDatabaseSchema)this).CheckVersionAsync(ct);
        var currentVersion = new DatabaseSchemaVersion((int)versionResult.CurrentVersion);

        var scripts = MigrationScriptLoader.GetScripts(typeof(PostgreSqlStore).Assembly, currentVersion, _schemaName);
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

        var verifyResult = await ((IDatabaseSchema)this).VerifySchemaAsync(ct);
        if (!verifyResult.IsValid)
        {
            var errors = string.Join("; ", verifyResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Schema verification failed after migration: {errors}");
        }
    }

    async Task<SchemaVerificationResult> IDatabaseSchema.VerifySchemaAsync(Ct ct)
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
            },
        };

        // Query actual columns from information_schema
        await using var colCmd = dataSource.CreateCommand(
            """
            SELECT table_name, column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = $1
            ORDER BY table_name, column_name
            """);
        _ = colCmd.Parameters.AddWithValue(_schemaName);
        Log.ExecutingSql(logger, colCmd.CommandText);

        var actualColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        // Key: "table|column" → data_type
        var actualColumnTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await using (var reader = await colCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var tableName = reader.GetString(0);
                var columnName = reader.GetString(1);
                var dataType = reader.GetString(2);

                if (!actualColumns.TryGetValue(tableName, out var cols))
                {
                    cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    actualColumns[tableName] = cols;
                }

                _ = cols.Add(columnName);
                actualColumnTypes[$"{tableName}|{columnName}"] = dataType;
            }
        }

        // Check each expected table and column
        foreach (var (tableName, columns) in expectedColumns)
        {
            if (!actualColumns.ContainsKey(tableName))
            {
                errors.Add(new SchemaVerificationError(
                    tableName, null,
                    $"Table '{_schemaName}.{tableName}' is missing.",
                    SchemaVerificationErrorKind.MissingTable));
                continue;
            }

            foreach (var (columnName, expectedType) in columns)
            {
                if (!actualColumns[tableName].Contains(columnName))
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, columnName,
                        $"Column '{columnName}' is missing from table '{_schemaName}.{tableName}'.",
                        SchemaVerificationErrorKind.MissingColumn));
                }
                else
                {
                    var actualType = actualColumnTypes[$"{tableName}|{columnName}"];
                    if (!string.Equals(actualType, expectedType, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new SchemaVerificationError(
                            tableName, columnName,
                            $"Column '{columnName}' in '{_schemaName}.{tableName}' has type '{actualType}', expected '{expectedType}'.",
                            SchemaVerificationErrorKind.WrongType));
                    }
                }
            }
        }

        // Check expected indexes
        var expectedIndexes = new[]
        {
            "entities_expires_at_index",
            "entities_created_at_index",
            "entities_last_updated_at_index",
            "entity_keys_entity_type_id_entity_id_index",
            "search_values_string_value_index",
            "search_values_number_value_index",
            "search_values_datetime_value_index",
            "search_values_boolean_value_index",
            "search_values_array_string_value_index",
            "search_values_array_number_value_index",
            "search_values_array_datetime_value_index",
            "search_values_array_boolean_value_index",
            "search_values_guid_value_index",
            "search_values_array_guid_value_index",
            "entity_links_left_entity_index",
            "entity_links_right_entity_index",
            "entity_links_left_cascade_index",
            "entity_links_right_cascade_index",
            "outbox_subscriber_queue_subscriber_index",
        };

        await using var idxCmd = dataSource.CreateCommand(
            """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = $1
            """);
        _ = idxCmd.Parameters.AddWithValue(_schemaName);
        Log.ExecutingSql(logger, idxCmd.CommandText);

        var actualIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await idxCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                _ = actualIndexes.Add(reader.GetString(0));
            }
        }

        foreach (var indexName in expectedIndexes)
        {
            if (!actualIndexes.Contains(indexName))
            {
                errors.Add(new SchemaVerificationError(
                    indexName, null,
                    $"Index '{indexName}' is missing from schema '{_schemaName}'.",
                    SchemaVerificationErrorKind.MissingIndex));
            }
        }

        // Check foreign keys
        var expectedForeignKeys = new[]
        {
            ("entity_keys", "entities"),
            ("search_values", "entities"),
        };

        await using var fkCmd = dataSource.CreateCommand(
            """
            SELECT tc.table_name, ccu.table_name AS foreign_table_name
            FROM information_schema.table_constraints AS tc
            JOIN information_schema.referential_constraints AS rc
                ON tc.constraint_name = rc.constraint_name AND tc.constraint_schema = rc.constraint_schema
            JOIN information_schema.constraint_column_usage AS ccu
                ON ccu.constraint_name = rc.unique_constraint_name AND ccu.constraint_schema = rc.unique_constraint_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.constraint_schema = $1
            """);
        _ = fkCmd.Parameters.AddWithValue(_schemaName);
        Log.ExecutingSql(logger, fkCmd.CommandText);

        var actualForeignKeys = new HashSet<(string, string)>();
        await using (var reader = await fkCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                _ = actualForeignKeys.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        foreach (var (fromTable, toTable) in expectedForeignKeys)
        {
            if (!actualForeignKeys.Contains((fromTable, toTable)))
            {
                errors.Add(new SchemaVerificationError(
                    fromTable, null,
                    $"Foreign key from '{_schemaName}.{fromTable}' to '{_schemaName}.{toTable}' is missing.",
                    SchemaVerificationErrorKind.MissingForeignKey));
            }
        }

        return new SchemaVerificationResult(errors);
    }

    string IDatabaseSchema.BuildMigrationScript(DatabaseSchemaVersion fromVersion)
    {
        var scripts = MigrationScriptLoader.GetScripts(typeof(PostgreSqlStore).Assembly, fromVersion, _schemaName);
        var sb = new StringBuilder();
        foreach (var (_, sql) in scripts)
        {
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

    async Task<StoreGetResult> IStore.TryReadAsync(
        EntityType entityType,
        Storage.UuidV7 id,
        Ct ct)
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

    async Task<StoreGetResult> IStore.TryReadAsync(
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
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

    async Task<DeleteResult> IStore.DeleteAsync(EntityType entityType, Storage.UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        var deleteOp = DeleteOperation.ById(entityType, id);

        await using var cnn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await cnn.BeginTransactionAsync(ct);

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

        await using var cnn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await cnn.BeginTransactionAsync(ct);

        var (_, entityDeleted) = await ExecuteDeleteCoreAsync(cnn, tx, deleteOp, ct);

        if (entityDeleted && outboxEvents is { Count: > 0 })
        {
            await ExecuteOutboxInsertBatchCoreAsync(cnn, tx, outboxEvents, ct);
        }

        await tx.CommitAsync(ct);
        return DeleteResult.Success;
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
    async Task<QueryResult<MetadataEnvelope<TDso>>> IStore.QueryAsync<TDso>(
        EntityType entityType,
        IQueryExpression filter,
        SortParameter sort,
        DataRange dataRange,
        Ct ct)
    {
        if (dataRange.TokenValue is not null)
        {
            return await QueryCursorAsync<TDso>(entityType, filter, sort, dataRange.TokenValue, ct);
        }

        var (skip, take) = ResolveOffsetAndSize(dataRange);
        var dsoVersion = TDso.DsoVersion;
        var entityTypeId = (int)entityType.Id;

        Log.QueryingDsos(logger, entityType, skip, take);

        // Build WHERE clause and ORDER BY clause
        await using var cmd = dataSource.CreateCommand();
        var queryClauses = BuildQueryClauses(cmd, filter, sort);

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

                // When page is beyond range, p.entity_id is NULL — skip deserializing
                if (await reader.IsDBNullAsync(0, ct))
                {
                    continue;
                }

                var entityId = reader.GetGuid(0);
                var jsonValue = reader.GetString(1);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                var created = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
                var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);
                items.Add(new MetadataEnvelope<TDso>(item, entityId, reader.GetInt32(3), created, lastUpdated));
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
            return await QueryFieldsCursorAsync(entityType, fields, filter, sort, dataRange.TokenValue, ct);
        }

        var (skip, take) = ResolveOffsetAndSize(dataRange);
        var entityTypeId = (int)entityType.Id;

        Log.QueryingFieldsDsos(logger, entityType, fields.Count, skip, take);

        // Build WHERE clause and ORDER BY clause
        await using var cmd = dataSource.CreateCommand();
        var queryClauses = BuildQueryClauses(cmd, filter, sort);

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
            HasMoreData = skip + take < totalCount
        };
    }

    /// <summary>
    /// Queries entities with cursor-based pagination.
    /// </summary>
    private async Task<QueryResult<MetadataEnvelope<TDso>>> QueryCursorAsync<TDso>(
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

        var dsoVersion = TDso.DsoVersion;
        var entityTypeId = (int)entityType.Id;
        var pageSize = tokenRange.Size.Value;

        Log.QueryingDsos(logger, entityType, 0, pageSize);

        // Build WHERE clause and ORDER BY clause for cursor-based pagination
        await using var cmd = dataSource.CreateCommand();
        var queryClauses = BuildCursorQueryClauses(cmd, entityTypeId, filter, sort, tokenRange);

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
        var items = new List<(Guid Id, MetadataEnvelope<TDso> Item, object? SortValue)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
            while (await reader.ReadAsync(ct))
            {
                var entityId = reader.GetGuid(0);
                var jsonValue = reader.GetString(1);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                var created = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
                var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);
                var envelope = new MetadataEnvelope<TDso>(item, entityId, reader.GetInt32(3), created, lastUpdated);

                // Get the sort value from column 6
                var sortValue = await ReadSortValueAsync(reader, sort.Field!, 6, ct);
                items.Add((entityId, envelope, sortValue));
            }
        }

        // Check if there are more pages
        var hasMore = items.Count > pageSize;
        var pageItems = items.Take(pageSize).ToList();

        // Generate next token if there are more pages
        ContinuationToken? nextToken = null;
        if (pageItems.Count > 0)
        {
            var lastItem = pageItems[^1];
            var token = CreateCursorToken(lastItem.Id, lastItem.SortValue);
            nextToken = (ContinuationToken)token.Encode();
        }

        var resultItems = pageItems.Select(x => x.Item).ToList();
        return new QueryResult<MetadataEnvelope<TDso>>
        {
            Items = resultItems,
            NextToken = nextToken,
            HasMoreData = hasMore
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
        ArgumentNullException.ThrowIfNull(sort);
        if (sort.IsEmpty)
        {
            throw new ArgumentException("Sort parameter is required for cursor-based pagination.", nameof(sort));
        }

        var entityTypeId = (int)entityType.Id;
        var pageSize = tokenRange.Size.Value;

        Log.QueryingFieldsDsos(logger, entityType, fields.Count, 0, pageSize);

        // Build WHERE clause and ORDER BY clause for cursor-based pagination
        await using var cmd = dataSource.CreateCommand();
        var queryClauses = BuildCursorQueryClauses(cmd, entityTypeId, filter, sort, tokenRange);

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

                    var sortValue = await ReadSortValueAsync(reader, sort.Field!, 7, ct);
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
        var hasMore = itemsList.Count > pageSize;
        var pageItems = itemsList.Take(pageSize).ToList();

        // Generate next token if there are more pages
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
    /// Builds the WHERE clause, ORDER BY clause, and calculates the offset for a query.
    /// </summary>
    private QueryClauses BuildQueryClauses(
        NpgsqlCommand cmd,
        IQueryExpression filter,
        SortParameter sort)
    {
        // Build WHERE clause
        var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect);
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
        int entityTypeId,
        IQueryExpression filter,
        SortParameter sort,
        ContinuationTokenDataRange tokenRange)
    {
        // Build WHERE clause
        var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect);
        var whereClause = whereBuilder.BuildWhereClause(filter);

        var sortFieldPath = sort.Field!.Path;
        var sortDirection = sort.Direction == SortDirection.Ascending ? "ASC" : "DESC";

        // Determine which column to sort on based on field type
        var sortColumn = GetSortColumnName(sort.Field!);

        // Build JOIN clause — timestamp fields are columns on entities table, no JOIN needed
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

        // Build ORDER BY clause
        var orderByClause = $"""
            ORDER BY
              {sortColumn} {sortDirection} NULLS LAST,
              v.entity_id ASC
            """;

        // Build seek clause for cursor position (WHERE clause addition)
        var seekClause = "";
        var tokenValue = tokenRange.Start.Value;
        if (tokenValue != ContinuationToken.Beginning)
        {
            var decodedToken = CursorToken.Decode(tokenValue);
            if (decodedToken != null)
            {
                // Use row value comparison for efficient seeking
                // Format: (sort_value, id) > (@last_sort, @last_id)
                // This ensures we continue from the exact position
                var lastSortParam = "@last_sort_value";
                var lastIdParam = "@last_id";

                // Add parameters based on the field type
                if (decodedToken.GuidValue.HasValue)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.GuidValue.Value);
                }
                else if (decodedToken.StringValue != null)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.StringValue);
                }
                else if (decodedToken.NumberValue.HasValue)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.NumberValue.Value);
                }
                else if (decodedToken.DateTimeValue.HasValue)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, NpgsqlDbType.TimestampTz, decodedToken.DateTimeValue.Value.UtcDateTime);
                }
                else if (decodedToken.BooleanValue.HasValue)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.BooleanValue.Value);
                }
                else
                {
                    // NULL sort value - use a sentinel value
                    _ = cmd.Parameters.AddWithValue(lastSortParam, DBNull.Value);
                }

                _ = cmd.Parameters.AddWithValue(lastIdParam, decodedToken.Id);

                // Build the seek condition based on sort direction
                // For ascending: (sort_value, id) > (last_sort, last_id)
                // For descending: (sort_value, id) < (last_sort, last_id) OR (sort_value IS NULL AND id > last_id)
                if (sort.Direction == SortDirection.Ascending)
                {
                    // Handle NULL values in sort column
                    seekClause = $"""
                        AND (
                          ({sortColumn} > {lastSortParam} OR ({sortColumn} = {lastSortParam} AND v.entity_id > {lastIdParam}))
                          OR ({sortColumn} IS NULL AND {lastSortParam} IS NOT NULL)
                        )
                        """;
                }
                else
                {
                    seekClause = $"""
                        AND (
                          ({sortColumn} < {lastSortParam} OR ({sortColumn} = {lastSortParam} AND v.entity_id > {lastIdParam}))
                          OR ({sortColumn} IS NOT NULL AND {lastSortParam} IS NULL)
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
    async Task<LinkResult> IStore.LinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
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
        return outcome == OperationOutcome.AlreadyLinked ? LinkResult.AlreadyLinked : LinkResult.Success;
    }

    /// <inheritdoc/>
    async Task<UnlinkResult> IStore.UnlinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
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
            INSERT INTO {_entityLinks} (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id)
            VALUES (@pool_id, @link_type_id, @left_entity_type_id, @left_entity_id, @right_entity_type_id, @right_entity_id)
            ON CONFLICT (pool_id, link_type_id, left_entity_id, right_entity_id) DO NOTHING
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@link_type_id", (int)op.Definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_type_id", (int)op.Definition.Left.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", op.LeftEntityId.Value);
        _ = cmd.Parameters.AddWithValue("@right_entity_type_id", (int)op.Definition.Right.Id);
        _ = cmd.Parameters.AddWithValue("@right_entity_id", op.RightEntityId.Value);

        Log.ExecutingSql(logger, cmd.CommandText);

        var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
        return rowsAffected == 0 ? OperationOutcome.AlreadyLinked : OperationOutcome.Success;
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
            valueRows.Add($"(@message_id{i}, @event_id{i}, @timestamp{i}, @event_name{i}, @subject_id{i}, @entity_type_id{i}, @entity_type_name{i}, @pool_id, @payload{i}::jsonb, @subscriber_name{i})");
            _ = cmd.Parameters.AddWithValue($"@message_id{i}", Guid.CreateVersion7());
            _ = cmd.Parameters.AddWithValue($"@event_id{i}", evt.Id.Value);
            _ = cmd.Parameters.Add(new NpgsqlParameter($"@timestamp{i}", NpgsqlDbType.TimestampTz) { Value = evt.Timestamp });
            _ = cmd.Parameters.AddWithValue($"@event_name{i}", evt.EventName.ToString());
            _ = cmd.Parameters.AddWithValue($"@subject_id{i}", evt.SubjectId.Value);
            _ = cmd.Parameters.AddWithValue($"@entity_type_id{i}", evt.EntityTypeId);
            _ = cmd.Parameters.AddWithValue($"@entity_type_name{i}", evt.EntityTypeName);
            _ = cmd.Parameters.Add(new NpgsqlParameter($"@payload{i}", NpgsqlDbType.Jsonb) { Value = evt.Payload });
            _ = cmd.Parameters.AddWithValue($"@subscriber_name{i}", subscriber.SubscriberName.ToString());
        }
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        cmd.CommandText = $"""
            INSERT INTO {_outboxSubscriberQueue}
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

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

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
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    async Task<OutboxEventsPage> IStore.GetOutboxEventsForSubscriberAsync(SubscriberName subscriberName, int count, Ct ct)
    {
        await using var cnn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = cnn.CreateCommand();
        cmd.CommandText = $"""
            SELECT sequence_number, message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name
            FROM {_outboxSubscriberQueue}
            WHERE subscriber_name = @subscriber_name
            ORDER BY sequence_number ASC
            LIMIT @limit
            """;
        _ = cmd.Parameters.AddWithValue("@subscriber_name", subscriberName.ToString());
        _ = cmd.Parameters.AddWithValue("@limit", count + 1);

        Log.ExecutingSql(logger, cmd.CommandText);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var events = new List<PersistedOutboxEvent>();
        while (await reader.ReadAsync(ct))
        {
            var timestamp = await reader.GetFieldValueAsync<DateTimeOffset>(3, ct);
            events.Add(new PersistedOutboxEvent
            {
                SequenceNumber = reader.GetInt64(0),
                MessageId = reader.GetGuid(1),
                EventId = reader.GetGuid(2),
                Timestamp = timestamp,
                EventName = OutboxEventName.Create(reader.GetString(4)),
                SubjectId = Storage.UuidV7.From(reader.GetGuid(5)),
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

        await using var cnn = await dataSource.OpenConnectionAsync(ct);

        const int MaxBatchSize = 1000;
        for (var offset = 0; offset < ids.Count; offset += MaxBatchSize)
        {
            var chunk = ids.Skip(offset).Take(MaxBatchSize).ToArray();

            await using var cmd = cnn.CreateCommand();
            cmd.CommandText = $"""
                DELETE FROM {_outboxSubscriberQueue}
                WHERE message_id = ANY(@ids)
                """;
            var guidIds = chunk.Select(id => id.Value).ToArray();
            _ = cmd.Parameters.Add(new NpgsqlParameter("@ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = guidIds });

            Log.ExecutingSql(logger, cmd.CommandText);
            _ = await cmd.ExecuteNonQueryAsync(ct);
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

        await using var deleteCmd = cnn.CreateCommand();
        deleteCmd.Transaction = tx;
        _ = deleteCmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = deleteCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);

        deleteCmd.CommandText = $"DELETE FROM {_entities} WHERE entity_type_id = @entity_type_id AND pool_id = @pool_id";

        // Both entity_keys and search_values have ON DELETE CASCADE, so we only need to delete from entities
        if (op.Id is not null)
        {
            Log.DeletingDso(logger, entityType, op.Id.Value);
            _ = deleteCmd.Parameters.AddWithValue("@entity_id", op.Id.Value);

            deleteCmd.CommandText += " AND entity_id = @entity_id";
        }
        else if (op.Key is not null)
        {
            var key = op.Key;
            _ = deleteCmd.Parameters.AddWithValue("@key_type_id", (int)key.DskVersion.KeyType.Id);
            _ = deleteCmd.Parameters.AddWithValue("@key_type_version", (int)key.DskVersion.SchemaVersion);
            _ = deleteCmd.Parameters.AddWithValue("@key_value", key.Value);
            deleteCmd.CommandText += $"""
                  AND entity_id = (
                    SELECT entity_id FROM {_entityKeys}
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
        var deletedEntityId = result is Guid guid ? guid : (Guid?)null;

        // Delete entity links (no FK to entities, must be done manually)
        if (deletedEntityId.HasValue)
        {
            await using var linkDeleteCmd = cnn.CreateCommand();
            linkDeleteCmd.Transaction = tx;
            linkDeleteCmd.CommandText = $"""
                DELETE FROM {_entityLinks}
                WHERE pool_id = @pool_id
                  AND (left_entity_id = @entity_id OR right_entity_id = @entity_id)
                """;
            _ = linkDeleteCmd.Parameters.AddWithValue("@pool_id", PoolId.Value);
            _ = linkDeleteCmd.Parameters.AddWithValue("@entity_id", deletedEntityId.Value);
            Log.ExecutingSql(logger, linkDeleteCmd.CommandText);
            _ = await linkDeleteCmd.ExecuteNonQueryAsync(ct);
        }

        return (OperationOutcome.Success, deletedEntityId.HasValue);
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

        await using var cmd = dataSource.CreateCommand();

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

        var items = new List<MetadataEnvelope<TDso>>();
        var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = reader.GetGuid(0);
                var jsonValue = reader.GetString(1);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                var valueVersion = reader.GetInt32(3);
                var created = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
                var lastUpdated = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);
                items.Add(new MetadataEnvelope<TDso>(item, entityId, valueVersion, created, lastUpdated));
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
        var entityTypeId = (int)entityType.Id;

        await using var cmd = dataSource.CreateCommand();

        string whereClause;
        if (filter is null or AllExpression)
        {
            whereClause = "1=1";
        }
        else
        {
            var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect);
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

    async Task<int> IStore.PurgeExpiredAsync(int batchSize, Ct ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, StorageConstants.TtlCleanupMaxBatchSize);

        var now = timeProvider.GetUtcNow();

        await using var cnn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await cnn.BeginTransactionAsync(ct);

        try
        {
            await using var cmd = cnn.CreateCommand();
            cmd.Transaction = tx;

            var sql = new StringBuilder();

            // Step 1: Lock expired rows into a temp table
            _ = sql.AppendLine(CultureInfo.InvariantCulture, $"""
                CREATE TEMP TABLE _expired ON COMMIT DROP AS
                SELECT pool_id, entity_id, entity_type_id, entity_type_name, value, gen_random_uuid() AS event_id
                FROM {_entities}
                WHERE expires_at IS NOT NULL AND expires_at <= @now
                LIMIT @batchSize
                FOR UPDATE SKIP LOCKED;
                """);
            _ = cmd.Parameters.Add(new NpgsqlParameter("@now", NpgsqlDbType.TimestampTz) { Value = now.UtcDateTime });
            _ = cmd.Parameters.AddWithValue("@batchSize", batchSize);

            // Step 2: Insert outbox events per matching subscriber
            if (!outboxSubscribers.IsEmpty)
            {
                var eventName = OutboxEventName.EntityExpired;
                _ = cmd.Parameters.AddWithValue("@eventName", eventName.ToString());

                var subscriberIndex = 0;
                foreach (var subscriber in outboxSubscribers.Subscribers)
                {
                    if (subscriber.EventNames.Count > 0 && !subscriber.EventNames.Contains(eventName))
                    {
                        continue;
                    }

                    var subParam = $"@sub{subscriberIndex}";
                    _ = cmd.Parameters.AddWithValue(subParam, subscriber.SubscriberName.ToString());

                    _ = sql.Append(CultureInfo.InvariantCulture, $"""
                        INSERT INTO {_outboxSubscriberQueue}
                        (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name)
                        SELECT gen_random_uuid(), event_id, @now, @eventName, entity_id, entity_type_id, entity_type_name, pool_id, value, {subParam}
                        FROM _expired
                        """);

                    if (subscriber.EntityTypeIds.Count > 0)
                    {
                        var typesParam = $"@subTypes{subscriberIndex}";
                        _ = cmd.Parameters.Add(new NpgsqlParameter(typesParam, NpgsqlDbType.Array | NpgsqlDbType.Integer)
                        {
                            Value = subscriber.EntityTypeIds.ToArray()
                        });
                        _ = sql.Append(CultureInfo.InvariantCulture, $" WHERE entity_type_id = ANY({typesParam})");
                    }

                    _ = sql.AppendLine(";");
                    subscriberIndex++;
                }
            }

            // Step 3: Delete entity links
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
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

}
