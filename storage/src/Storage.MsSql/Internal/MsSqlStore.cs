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
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using CursorToken = Duende.Storage.Internal.Querying.CursorToken;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.MsSql.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
internal sealed class MsSqlStore(
    CreateSqlConnection createConnection,
    MsSqlStoreOptions options,
    DataStorageTypeRegistry dataStorageTypeRegistry,
    TimeProvider timeProvider,
    OutboxSubscribers outboxSubscribers,
    ILogger<MsSqlStore> logger) : StoreBase, IStore, IDatabaseSchema
{
    private const int RequiredSchemaVersion = 1;
    private readonly string _schemaName = options.SchemaName;

    private SqlConnection OpenConnection() => createConnection();

    async Task<CheckSchemaVersionResult> IDatabaseSchema.CheckVersionAsync(Ct ct)
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

    async Task IDatabaseSchema.MigrateAsync(Ct ct)
    {
        Log.MigratingSchema(logger, _schemaName);

        var versionResult = await ((IDatabaseSchema)this).CheckVersionAsync(ct);
        var currentVersion = new DatabaseSchemaVersion((int)versionResult.CurrentVersion);

        var scripts = MigrationScriptLoader.GetScripts(typeof(MsSqlStore).Assembly, currentVersion, _schemaName).ToList();

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        foreach (var (_, sql) in scripts)
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
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
            catch (SqlException e)
            {
                Log.ErrorWhileCreatingSchema(logger, e);
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
            },
        };

        // Query actual columns from INFORMATION_SCHEMA
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $"""
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = '{_schemaName}'
                ORDER BY TABLE_NAME, COLUMN_NAME
                """;
            Log.ExecutingSql(logger, cmd.CommandText);

            var actualColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var actualColumnTypes = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var tableName = reader.GetString(0);
                var columnName = reader.GetString(1);
                var dataType = reader.GetString(2);

                if (!actualColumns.TryGetValue(tableName, out var cols))
                {
                    cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    actualColumns[tableName] = cols;
                    actualColumnTypes[tableName] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                _ = cols.Add(columnName);
                actualColumnTypes[tableName][columnName] = dataType;
            }

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
                    else if (!string.Equals(actualColumnTypes[tableName][columnName], expectedType, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new SchemaVerificationError(
                            tableName, columnName,
                            $"Column '{columnName}' in table '{_schemaName}.{tableName}' has type '{actualColumnTypes[tableName][columnName]}' but expected '{expectedType}'.",
                            SchemaVerificationErrorKind.WrongType));
                    }
                }
            }
        }

        // Verify required indexes
        var expectedIndexes = new[]
        {
            ("entities", $"IX_{_schemaName}_entities_expires_at"),
            ("entities", $"IX_{_schemaName}_entities_entity_type_name"),
            ("entities", $"IX_{_schemaName}_entities_created_at"),
            ("entities", $"IX_{_schemaName}_entities_last_updated_at"),
            ("entity_keys", $"IX_{_schemaName}_entity_keys_entity_type_id_entity_id"),
            ("search_values", $"IX_{_schemaName}_search_values_string_value"),
            ("search_values", $"IX_{_schemaName}_search_values_number_value"),
            ("search_values", $"IX_{_schemaName}_search_values_datetime_value"),
            ("search_values", $"IX_{_schemaName}_search_values_boolean_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_string_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_number_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_datetime_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_boolean_value"),
            ("search_values", $"IX_{_schemaName}_search_values_guid_value"),
            ("search_values", $"IX_{_schemaName}_search_values_array_guid_value"),
            ("entity_links", $"IX_{_schemaName}_entity_links_left_entity"),
            ("entity_links", $"IX_{_schemaName}_entity_links_right_entity"),
            ("entity_links", $"IX_{_schemaName}_entity_links_left_cascade"),
            ("entity_links", $"IX_{_schemaName}_entity_links_right_cascade"),
            ("outbox_subscriber_queue", $"IX_{_schemaName}_outbox_subscriber_queue_subscriber"),
        };

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $"""
                SELECT t.name AS table_name, i.name AS index_name
                FROM sys.indexes i
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'{_schemaName}'
                  AND i.name IS NOT NULL
                """;
            Log.ExecutingSql(logger, cmd.CommandText);

            var actualIndexes = new HashSet<(string Table, string Index)>(
                EqualityComparer<(string, string)>.Create(
                    (a, b) => string.Equals(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase),
                    x => StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item1) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item2)));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                _ = actualIndexes.Add((reader.GetString(0), reader.GetString(1)));
            }

            foreach (var (tableName, indexName) in expectedIndexes)
            {
                if (!actualIndexes.Contains((tableName, indexName)))
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, null,
                        $"Index '{indexName}' is missing from table '{_schemaName}.{tableName}'.",
                        SchemaVerificationErrorKind.MissingIndex));
                }
            }
        }

        // Verify required foreign keys
        var expectedForeignKeys = new[]
        {
            ("entity_keys", $"FK_{_schemaName}_entity_keys_entities"),
            ("search_values", $"FK_{_schemaName}_search_values_entities"),
        };

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $"""
                SELECT t.name AS table_name, fk.name AS fk_name
                FROM sys.foreign_keys fk
                INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'{_schemaName}'
                """;
            Log.ExecutingSql(logger, cmd.CommandText);

            var actualForeignKeys = new HashSet<(string Table, string Fk)>(
                EqualityComparer<(string, string)>.Create(
                    (a, b) => string.Equals(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase),
                    x => StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item1) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item2)));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                _ = actualForeignKeys.Add((reader.GetString(0), reader.GetString(1)));
            }

            foreach (var (tableName, fkName) in expectedForeignKeys)
            {
                if (!actualForeignKeys.Contains((tableName, fkName)))
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, null,
                        $"Foreign key '{fkName}' is missing from table '{_schemaName}.{tableName}'.",
                        SchemaVerificationErrorKind.MissingForeignKey));
                }
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

    string IDatabaseSchema.BuildMigrationScript(DatabaseSchemaVersion fromVersion)
    {
        var scripts = MigrationScriptLoader.GetScripts(typeof(MsSqlStore).Assembly, fromVersion, _schemaName);
        return string.Concat(scripts.Select(s => s.Sql + Environment.NewLine));
    }

    async Task<CreateResult> IStore.CreateAsync<TDso>(
        Storage.UuidV7 id,
        TDso value,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
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

    async Task<StoreGetResult> IStore.TryReadAsync(
        EntityType entityType,
        Storage.UuidV7 id,
        Ct ct)
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

    async Task<StoreGetResult> IStore.TryReadAsync(
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
    {
        Log.ReadingDso(logger, entityType, key.Value);

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at
            FROM [{_schemaName}].entity_keys i
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

    async Task<DeleteResult> IStore.DeleteAsync(EntityType entityType, Storage.UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        var deleteOp = DeleteOperation.ById(entityType, id);

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var (_, entityDeleted) = await ExecuteDeleteCoreAsync(connection, (SqlTransaction)transaction, deleteOp, ct);

        if (entityDeleted && outboxEvents is { Count: > 0 })
        {
            await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
        }
        await transaction.CommitAsync(ct);

        return DeleteResult.Success;
    }

    async Task<DeleteResult> IStore.DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        var deleteOp = DeleteOperation.ByKey(entityType, key);

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var (_, entityDeleted) = await ExecuteDeleteCoreAsync(connection, (SqlTransaction)transaction, deleteOp, ct);

        if (entityDeleted && outboxEvents is { Count: > 0 })
        {
            await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
        }
        await transaction.CommitAsync(ct);

        return DeleteResult.Success;
    }

    /// <inheritdoc/>
    async Task<LinkResult> IStore.LinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            var outcome = await ExecuteLinkCoreAsync(connection, (SqlTransaction)transaction, LinkOperation.For(definition, leftEntityId, rightEntityId), ct);
            if (outcome == OperationOutcome.Success)
            {
                if (outboxEvents is { Count: > 0 })
                {
                    await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
                }
                await transaction.CommitAsync(ct);
            }
            return outcome == OperationOutcome.AlreadyLinked ? LinkResult.AlreadyLinked : LinkResult.Success;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc/>
    async Task<UnlinkResult> IStore.UnlinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            _ = await ExecuteUnlinkCoreAsync(connection, (SqlTransaction)transaction, UnlinkOperation.For(definition, leftEntityId, rightEntityId), ct);
            if (outboxEvents is { Count: > 0 })
            {
                await ExecuteOutboxInsertBatchCoreAsync(connection, (SqlTransaction)transaction, outboxEvents, ct);
            }
            await transaction.CommitAsync(ct);
            return UnlinkResult.Success;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
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
            INSERT INTO [{_schemaName}].[entity_links] (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id)
            VALUES (@poolId, @linkTypeId, @leftEntityTypeId, @leftEntityId, @rightEntityTypeId, @rightEntityId)
            """;
        _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);
        _ = cmd.Parameters.AddWithValue("@linkTypeId", (int)op.Definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@leftEntityTypeId", (int)op.Definition.Left.Id);
        _ = cmd.Parameters.AddWithValue("@leftEntityId", SqlServerGuidConverter.ToSqlServer(op.LeftEntityId.Value));
        _ = cmd.Parameters.AddWithValue("@rightEntityTypeId", (int)op.Definition.Right.Id);
        _ = cmd.Parameters.AddWithValue("@rightEntityId", SqlServerGuidConverter.ToSqlServer(op.RightEntityId.Value));

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

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;

        var valueRows = new List<string>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var (evt, subscriber) = rows[i];
            valueRows.Add($"(@messageId{i}, @eventId{i}, @timestamp{i}, @eventName{i}, @subjectId{i}, @entityTypeId{i}, @entityTypeName{i}, @poolId, @payload{i}, @subscriberName{i})");
            _ = cmd.Parameters.AddWithValue($"@messageId{i}", Guid.CreateVersion7());
            _ = cmd.Parameters.AddWithValue($"@eventId{i}", evt.Id.Value);
            _ = cmd.Parameters.AddWithValue($"@timestamp{i}", evt.Timestamp);
            _ = cmd.Parameters.AddWithValue($"@eventName{i}", evt.EventName.ToString());
            _ = cmd.Parameters.AddWithValue($"@subjectId{i}", SqlServerGuidConverter.ToSqlServer(evt.SubjectId.Value));
            _ = cmd.Parameters.AddWithValue($"@entityTypeId{i}", evt.EntityTypeId);
            _ = cmd.Parameters.AddWithValue($"@entityTypeName{i}", evt.EntityTypeName);
            _ = cmd.Parameters.AddWithValue($"@payload{i}", evt.Payload);
            _ = cmd.Parameters.AddWithValue($"@subscriberName{i}", subscriber.SubscriberName.ToString());
        }
        _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);

        cmd.CommandText = $"""
            INSERT INTO [{_schemaName}].[outbox_subscriber_queue]
            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name)
            VALUES
            {string.Join(",\n            ", valueRows)}
            """;

        Log.ExecutingSql(logger, cmd.CommandText);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    async Task<BatchResult> IStore.ExecuteBatchAsync(
        IReadOnlyList<IStoreOperation> operations,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct)
    {
        if (operations.Count == 0)
        {
            return new BatchResult(true, []);
        }

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var results = new List<OperationResult>();

        try
        {
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
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    async Task<OutboxEventsPage> IStore.GetOutboxEventsForSubscriberAsync(SubscriberName subscriberName, int count, Ct ct)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT TOP (@count) sequence_number, message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name
            FROM [{_schemaName}].[outbox_subscriber_queue]
            WHERE subscriber_name = @subscriber_name
            ORDER BY sequence_number ASC
            """;

        _ = cmd.Parameters.AddWithValue("@count", count + 1);
        _ = cmd.Parameters.AddWithValue("@subscriber_name", subscriberName.ToString());

        Log.ExecutingSql(logger, cmd.CommandText);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var events = new List<PersistedOutboxEvent>();
        while (await reader.ReadAsync(ct))
        {
            events.Add(new PersistedOutboxEvent
            {
                SequenceNumber = reader.GetInt64(0),
                MessageId = reader.GetGuid(1),
                EventId = reader.GetGuid(2),
                Timestamp = reader.GetDateTimeOffset(3),
                EventName = OutboxEventName.Create(reader.GetString(4)),
                SubjectId = Storage.UuidV7.From(SqlServerGuidConverter.ToUuidV7(reader.GetGuid(5))),
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

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        // Build WHERE clause and ORDER BY clause
        var queryClauses = BuildQueryClauses(cmd, filter, sort, skip);

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
        var items = new List<MetadataEnvelope<TDso>>();
        var totalCount = 0;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
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
                var valueVersion = reader.GetInt32(3);
                var created = reader.GetDateTimeOffset(4);
                var lastUpdated = reader.GetDateTimeOffset(5);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
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

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        // Build WHERE clause and ORDER BY clause for cursor-based pagination
        var queryClauses = BuildCursorQueryClauses(cmd, filter, sort, tokenRange);

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
        var items = new List<(Guid Id, TDso Item, int Version, DateTimeOffset Created, DateTimeOffset LastUpdated, object? SortValue)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
            while (await reader.ReadAsync(ct))
            {
                var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
                var jsonValue = reader.GetString(1);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                var valueVersion = reader.GetInt32(3);
                var created = reader.GetDateTimeOffset(4);
                var lastUpdated = reader.GetDateTimeOffset(5);
                // Get the sort value from the last column (index 6)
                var sortValue = await ReadSortValueAsync(reader, sort.Field!, 6, ct);
                items.Add((entityId, item, valueVersion, created, lastUpdated, sortValue));
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

        var resultItems = pageItems.Select(x => new MetadataEnvelope<TDso>(x.Item, x.Id, x.Version, x.Created, x.LastUpdated)).ToList();
        return new QueryResult<MetadataEnvelope<TDso>>
        {
            Items = resultItems,
            NextToken = nextToken,
            HasMoreData = hasMore
        };
    }

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

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        // Build WHERE clause and ORDER BY clause
        var queryClauses = BuildQueryClauses(cmd, filter, sort, skip);

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
            LEFT JOIN [{_schemaName}].search_values field_sv
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
            HasMoreData = skip + take < totalCount
        };
    }

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

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        // Build WHERE clause and ORDER BY clause for cursor-based pagination
        var queryClauses = BuildCursorQueryClauses(cmd, filter, sort, tokenRange);

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
            LEFT JOIN [{_schemaName}].search_values field_sv
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
                    var sortValue = await ReadSortValueAsync(reader, sort.Field!, 7, ct);
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

        var itemsList = orderedIds.Select(id => (Id: id, resultsByid[id])).ToList();
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
                    INSERT INTO [{_schemaName}].entity_keys (
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
                    INSERT INTO [{_schemaName}].search_values (
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
                    DELETE FROM [{_schemaName}].entity_keys
                    WHERE entity_type_id = @entityTypeId AND entity_id = @entityId AND pool_id = @poolId;

                    DELETE FROM [{_schemaName}].search_values
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
                    INSERT INTO [{_schemaName}].entity_keys (
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
                    INSERT INTO [{_schemaName}].search_values (
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

        await using var deleteCmd = connection.CreateCommand();
        deleteCmd.Transaction = transaction;
        deleteCmd.CommandType = CommandType.Text;

        _ = deleteCmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
        _ = deleteCmd.Parameters.AddWithValue("@poolId", PoolId.Value);

        if (op.Id is not null)
        {
            Log.DeletingDso(logger, entityType, op.Id.Value);

            deleteCmd.CommandText = $"""
                DELETE FROM [{_schemaName}].[entities]
                WHERE entity_type_id = @entityTypeId AND entity_id = @entityId AND pool_id = @poolId
                """;

            _ = deleteCmd.Parameters.AddWithValue("@entityId", SqlServerGuidConverter.ToSqlServer(op.Id.Value));
        }
        else if (op.Key is not null)
        {
            var key = op.Key;

            deleteCmd.CommandText = $"""
                DELETE FROM [{_schemaName}].[entities]
                WHERE entity_type_id = @entityTypeId
                  AND pool_id = @poolId
                  AND entity_id = (
                    SELECT entity_id
                    FROM [{_schemaName}].entity_keys
                    WHERE entity_type_id = @entityTypeId
                      AND key_type_id = @keyTypeId
                      AND key_type_version = @keyTypeVersion
                      AND key_value = @keyValue
                      AND pool_id = @poolId
                  )
                """;

            _ = deleteCmd.Parameters.AddWithValue("@keyTypeId", (int)key.DskVersion.KeyType.Id);
            _ = deleteCmd.Parameters.AddWithValue("@keyTypeVersion", (int)key.DskVersion.SchemaVersion);
            _ = deleteCmd.Parameters.AddWithValue("@keyValue", key.Value);
        }
        else
        {
            return (OperationOutcome.Success, false);
        }

        Log.ExecutingSql(logger, deleteCmd.CommandText);

        // Resolve entity_id for link cleanup BEFORE deleting the entity,
        // because entity_keys has ON DELETE CASCADE and will be gone after delete.
        Guid? entityIdForLinks = null;
        if (op.Id is not null)
        {
            entityIdForLinks = SqlServerGuidConverter.ToSqlServer(op.Id.Value);
        }
        else if (op.Key is not null)
        {
            entityIdForLinks = await ResolveKeyToEntityIdAsync(connection, transaction, op.EntityType, op.Key, ct);
        }

        var rowsAffected = await deleteCmd.ExecuteNonQueryAsync(ct);

        // Delete entity links (no FK to entities, must be done manually)
        if (entityIdForLinks.HasValue)
        {
            await using var linkDeleteCmd = connection.CreateCommand();
            linkDeleteCmd.Transaction = transaction;
            linkDeleteCmd.CommandType = CommandType.Text;
            linkDeleteCmd.CommandText = $"""
                DELETE FROM [{_schemaName}].[entity_links]
                WHERE pool_id = @poolId
                  AND (left_entity_id = @entityId OR right_entity_id = @entityId)
                """;
            _ = linkDeleteCmd.Parameters.AddWithValue("@poolId", PoolId.Value);
            _ = linkDeleteCmd.Parameters.AddWithValue("@entityId", entityIdForLinks.Value);
            Log.ExecutingSql(logger, linkDeleteCmd.CommandText);
            _ = await linkDeleteCmd.ExecuteNonQueryAsync(ct);
        }

        return (OperationOutcome.Success, rowsAffected > 0);
    }

    private async Task<Guid?> ResolveKeyToEntityIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT entity_id FROM [{_schemaName}].[entity_keys]
            WHERE entity_type_id = @entityTypeId
              AND key_type_id = @keyTypeId
              AND key_type_version = @keyTypeVersion
              AND key_value = @keyValue
              AND pool_id = @poolId
            """;
        _ = cmd.Parameters.AddWithValue("@entityTypeId", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@keyTypeId", (int)key.DskVersion.KeyType.Id);
        _ = cmd.Parameters.AddWithValue("@keyTypeVersion", (int)key.DskVersion.SchemaVersion);
        _ = cmd.Parameters.AddWithValue("@keyValue", key.Value);
        _ = cmd.Parameters.AddWithValue("@poolId", PoolId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid guid ? guid : null;
    }

    /// <summary>
    /// Builds the WHERE clause, JOIN clause, ORDER BY clause, and calculates the offset for a query.
    /// </summary>
    private QueryClauses BuildQueryClauses(
        SqlCommand cmd,
        IQueryExpression filter,
        SortParameter sort,
        int offset)
    {
        // Build WHERE clause using shared SqlWhereClauseBuilder
        var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect);
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
                    LEFT JOIN [{_schemaName}].search_values sort_sv
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
        ContinuationTokenDataRange tokenRange)
    {
        // Build WHERE clause using shared SqlWhereClauseBuilder
        var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect);
        var whereClause = whereBuilder.BuildWhereClause(filter);

        var sortFieldPath = sort.Field!.Path;

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
                LEFT JOIN [{_schemaName}].search_values sort_sv
                  ON v.entity_type_id = sort_sv.entity_type_id
                  AND v.entity_id = sort_sv.entity_id
                  AND v.pool_id = sort_sv.pool_id
                  AND sort_sv.field_path = @sort_field_path
                  AND sort_sv.item_index = -1
                """;

            _ = cmd.Parameters.AddWithValue("@sort_field_path", DeterministicGuidGenerator.Create(sortFieldPath.ToUpperInvariant()));
        }

        // Build ORDER BY clause with NULLS LAST behavior (using CASE expression)
        string orderByClause;
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

        // Build seek clause for cursor position (WHERE clause addition)
        var seekClause = "";
        var tokenValue = tokenRange.Start.Value;
        if (tokenValue != ContinuationToken.Beginning)
        {
            var decodedToken = CursorToken.Decode(tokenValue);
            if (decodedToken != null)
            {
                // Use seek conditions for efficient pagination
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
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.DateTimeValue.Value);
                }
                else if (decodedToken.BooleanValue.HasValue)
                {
                    _ = cmd.Parameters.AddWithValue(lastSortParam, decodedToken.BooleanValue.Value);
                }
                else
                {
                    // NULL sort value - use DBNull
                    _ = cmd.Parameters.AddWithValue(lastSortParam, DBNull.Value);
                }

                _ = cmd.Parameters.AddWithValue(lastIdParam, SqlServerGuidConverter.ToSqlServer(decodedToken.Id));

                // Build the seek condition based on sort direction
                // For ascending: seek rows where (sort_value > last_sort) OR (sort_value = last_sort AND id > last_id)
                // For descending: seek rows where (sort_value < last_sort) OR (sort_value = last_sort AND id > last_id)
                // Handle NULL values according to NULLS LAST behavior
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
    /// Creates a cursor token from a sort value and entity ID.
    /// </summary>
    private static CursorToken CreateCursorToken(Guid id, object? sortValue) =>
        sortValue switch
        {
            string s => CursorToken.Create(id, s, null, null, null, null),
            decimal d => CursorToken.Create(id, null, d, null, null, null),
            DateTimeOffset dto => CursorToken.Create(id, null, null, dto, null, null),
            bool b => CursorToken.Create(id, null, null, null, b, null),
            Guid g => CursorToken.Create(id, null, null, null, null, g),
            null => CursorToken.Create(id, null, null, null, null, null),
            _ => throw new InvalidOperationException($"Unsupported sort value type: {sortValue.GetType().Name}")
        };

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

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

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

        var items = new List<MetadataEnvelope<TDso>>();
        var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = SqlServerGuidConverter.ToUuidV7(reader.GetGuid(0));
                var jsonValue = reader.GetString(1);
                var valueVersion = reader.GetInt32(3);
                var created = reader.GetDateTimeOffset(4);
                var lastUpdated = reader.GetDateTimeOffset(5);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                items.Add(new MetadataEnvelope<TDso>(item, entityId, valueVersion, created, lastUpdated));
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
            var whereBuilder = new SqlWhereClauseBuilder(_schemaName, cmd, Dialect);
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

    async Task<int> IStore.PurgeExpiredAsync(int batchSize, Ct ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, StorageConstants.TtlCleanupMaxBatchSize);

        var now = timeProvider.GetUtcNow();

        await using var connection = OpenConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = (SqlTransaction)transaction;
            cmd.CommandType = CommandType.Text;

            var sql = new StringBuilder();

            // Step 1: Lock expired rows into a temp table
            _ = sql.AppendLine(CultureInfo.InvariantCulture, $"""
                SELECT TOP (@batchSize) pool_id, entity_id, entity_type_id, entity_type_name, value, NEWID() AS event_id
                INTO #_expired
                FROM [{_schemaName}].[entities] WITH (UPDLOCK, ROWLOCK, READPAST)
                WHERE expires_at IS NOT NULL AND expires_at <= @now;
                """);
            _ = cmd.Parameters.AddWithValue("@now", now);
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
                        INSERT INTO [{_schemaName}].[outbox_subscriber_queue]
                        (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name)
                        SELECT NEWID(), event_id, @now, @eventName, entity_id, entity_type_id, entity_type_name, pool_id, value, {subParam}
                        FROM #_expired
                        """);

                    if (subscriber.EntityTypeIds.Count > 0)
                    {
                        var typeIds = string.Join(", ", subscriber.EntityTypeIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
                        _ = sql.Append(CultureInfo.InvariantCulture, $" WHERE entity_type_id IN ({typeIds})");
                    }

                    _ = sql.AppendLine(";");
                    subscriberIndex++;
                }
            }

            // Step 3: Delete entity links
            _ = sql.AppendLine(CultureInfo.InvariantCulture, $"""
                DELETE el FROM [{_schemaName}].[entity_links] el
                INNER JOIN #_expired e ON el.pool_id = e.pool_id
                    AND (
                        (el.left_entity_id = e.entity_id AND el.left_entity_type_id = e.entity_type_id)
                        OR
                        (el.right_entity_id = e.entity_id AND el.right_entity_type_id = e.entity_type_id)
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
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
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
