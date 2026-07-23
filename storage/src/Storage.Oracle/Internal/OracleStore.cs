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
using Oracle.ManagedDataAccess.Client;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.Oracle.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
internal sealed class OracleStore(
    CreateOracleConnection createConnection,
    OracleStoreOptions options,
    DataStorageTypeRegistry dataStorageTypeRegistry,
    TimeProvider timeProvider,
    OutboxSubscribers outboxSubscribers,
    ILogger<OracleStore> logger) : StoreBase, IStore, IDatabaseSchema
{
    private const int RequiredSchemaVersion = 2;
    private static readonly ISqlDialect Dialect = new OracleDialect();
    private string? _resolvedSchema;

    // ───────────────────────── helpers ─────────────────────────

    private async Task<OracleConnection> OpenConnectionAsync(Ct ct)
    {
        var conn = createConnection();
        await conn.OpenAsync(ct);

        await using var sessionCmd = conn.CreateCommand();
        sessionCmd.CommandText = "ALTER SESSION SET NLS_COMP=LINGUISTIC NLS_SORT=BINARY_CI";
        _ = await sessionCmd.ExecuteNonQueryAsync(ct);

        return conn;
    }

    private async ValueTask<string> ResolveSchemaAsync(OracleConnection conn, Ct ct)
    {
        if (_resolvedSchema is not null)
        {
            return _resolvedSchema;
        }

        var configured = options.SchemaName?.ToUpperInvariant();
        if (!string.IsNullOrEmpty(configured))
        {
            _resolvedSchema = configured;
            return configured;
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL";
        var result = (string)(await cmd.ExecuteScalarAsync(ct))!;
        _resolvedSchema = result;
        return result;
    }

    private static string SchemaPrefix(string schema) => $"{Dialect.QuoteIdentifier(schema)}.";

    private static Guid ReadGuid(OracleDataReader reader, int ordinal) =>
        OracleGuidConverter.FromRaw((byte[])reader.GetValue(ordinal));

    private static DateTimeOffset ReadDateTimeOffset(OracleDataReader reader, int ordinal)
    {
        var ts = reader.GetOracleTimeStampTZ(ordinal);
        return new DateTimeOffset(ts.Value, ts.GetTimeZoneOffset());
    }

    private static DateTimeOffset? ReadNullableDateTimeOffset(OracleDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ReadDateTimeOffset(reader, ordinal);

    private static void BindClob(OracleCommand cmd, string name, string? value)
    {
        cmd.BindByName = true;
        var p = new OracleParameter
        {
            ParameterName = name.TrimStart('@', ':'),
            OracleDbType = OracleDbType.Clob,
            Value = (object?)value ?? DBNull.Value
        };
        _ = cmd.Parameters.Add(p);
    }

    // ───────────────────────── IDatabaseSchema ─────────────────────────

    async Task<CheckSchemaVersionResult> IDatabaseSchema.CheckVersionAsync(Ct ct)
    {
        Log.CheckingSchemaVersion(logger);

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT MAX(version) FROM {prefix}"__SCHEMA_INFO"
            """;

        Log.ExecutingSql(logger, cmd.CommandText);

        try
        {
            var scalar = await cmd.ExecuteScalarAsync(ct);
            if (scalar is null or DBNull)
            {
                return new CheckSchemaVersionResult(0, RequiredSchemaVersion);
            }

            var version = (uint)Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            return new CheckSchemaVersionResult(version, RequiredSchemaVersion);
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            // ORA-00942: table or view does not exist
            return new CheckSchemaVersionResult(0, RequiredSchemaVersion);
        }
    }

    async Task IDatabaseSchema.MigrateAsync(Ct ct)
    {
        var schemaConfigured = options.SchemaName?.ToUpperInvariant() ?? "";
        Log.MigratingSchema(logger, schemaConfigured);

        var versionResult = await ((IDatabaseSchema)this).CheckVersionAsync(ct);
        var currentVersion = new DatabaseSchemaVersion((int)versionResult.CurrentVersion);

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        var scripts = MigrationScriptLoader.GetScripts(typeof(OracleStore).Assembly, currentVersion, prefix).ToList();

        foreach (var (targetVersion, statements) in scripts)
        {
            Log.ExecutingMigrationStep(logger, currentVersion.Value, targetVersion);

            try
            {
                foreach (var statement in statements)
                {
                    await using var stepCmd = connection.CreateCommand();
                    stepCmd.CommandType = CommandType.Text;
                    stepCmd.CommandText = statement;
                    Log.ExecutingSql(logger, stepCmd.CommandText);
                    _ = await stepCmd.ExecuteNonQueryAsync(ct);
                }

                // Explicit commit after each migration script (DDL auto-commits but DML does not)
                await using var commitCmd = connection.CreateCommand();
                commitCmd.CommandText = "COMMIT";
                _ = await commitCmd.ExecuteNonQueryAsync(ct);
            }
            catch (OracleException e)
            {
                Log.ErrorWhileCreatingSchema(logger, e);
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
        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);

        Log.VerifyingSchema(logger, schema);

        var errors = new List<SchemaVerificationError>();

        // Expected tables and their required columns (table -> column -> data_type prefix)
        var expectedColumns = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ENTITIES"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["POOL_ID"] = "NUMBER",
                ["ENTITY_TYPE_ID"] = "NUMBER",
                ["ENTITY_ID"] = "RAW",
                ["ORIGINAL_ENTITY_ID"] = "RAW",
                ["ENTITY_TYPE_NAME"] = "NVARCHAR2",
                ["VALUE"] = "CLOB",
                ["DSO_TYPE_SCHEMA_VERSION"] = "NUMBER",
                ["VALUE_VERSION"] = "NUMBER",
                ["CREATED_AT"] = "TIMESTAMP",
                ["LAST_UPDATED_AT"] = "TIMESTAMP",
                ["EXPIRES_AT"] = "TIMESTAMP",
            },
            ["ENTITY_KEYS"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["POOL_ID"] = "NUMBER",
                ["ENTITY_TYPE_ID"] = "NUMBER",
                ["KEY_TYPE_ID"] = "NUMBER",
                ["ENTITY_ID"] = "RAW",
                ["KEY_TYPE_NAME"] = "NVARCHAR2",
                ["KEY_TYPE_VERSION"] = "NUMBER",
                ["KEY_VALUE"] = "RAW",
                ["KEY_JSON"] = "CLOB",
                ["TIMESTAMP"] = "TIMESTAMP",
            },
            ["SEARCH_VALUES"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["POOL_ID"] = "NUMBER",
                ["ENTITY_TYPE_ID"] = "NUMBER",
                ["ENTITY_ID"] = "RAW",
                ["FIELD_PATH"] = "RAW",
                ["FIELD_PATH_TEXT"] = "NVARCHAR2",
                ["ITEM_INDEX"] = "NUMBER",
                ["STRING_VALUE"] = "NVARCHAR2",
                ["NUMBER_VALUE"] = "NUMBER",
                ["DATETIME_VALUE"] = "TIMESTAMP",
                ["BOOLEAN_VALUE"] = "NUMBER",
                ["GUID_VALUE"] = "RAW",
            },
            ["ENTITY_LINKS"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["POOL_ID"] = "NUMBER",
                ["LINK_TYPE_ID"] = "NUMBER",
                ["LEFT_ENTITY_TYPE_ID"] = "NUMBER",
                ["LEFT_ENTITY_ID"] = "RAW",
                ["RIGHT_ENTITY_TYPE_ID"] = "NUMBER",
                ["RIGHT_ENTITY_ID"] = "RAW",
                ["CREATED_AT"] = "TIMESTAMP",
            },
            ["OUTBOX_SUBSCRIBER_QUEUE"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SEQUENCE_NUMBER"] = "NUMBER",
                ["MESSAGE_ID"] = "RAW",
                ["EVENT_ID"] = "RAW",
                ["TIMESTAMP"] = "TIMESTAMP",
                ["EVENT_NAME"] = "NVARCHAR2",
                ["SUBJECT_ID"] = "RAW",
                ["ENTITY_TYPE_ID"] = "NUMBER",
                ["ENTITY_TYPE_NAME"] = "NVARCHAR2",
                ["POOL_ID"] = "NUMBER",
                ["PAYLOAD"] = "CLOB",
                ["SUBSCRIBER_NAME"] = "NVARCHAR2",
                ["DSO_TYPE_SCHEMA_VERSION"] = "NUMBER",
            },
        };

        // Query actual columns from ALL_TAB_COLUMNS
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = """
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
                FROM ALL_TAB_COLUMNS
                WHERE OWNER = :owner
                ORDER BY TABLE_NAME, COLUMN_NAME
                """;
            Dialect.AddParameter(cmd, ":owner", schema);
            Log.ExecutingSql(logger, cmd.CommandText);

            var actualColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var actualColumnTypes = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            await using (reader)
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
                        actualColumnTypes[tableName] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    _ = cols.Add(columnName);
                    actualColumnTypes[tableName][columnName] = dataType;
                }
            }

            foreach (var (tableName, columns) in expectedColumns)
            {
                if (!actualColumns.ContainsKey(tableName))
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, null,
                        $"Table '{schema}.{tableName}' is missing.",
                        SchemaVerificationErrorKind.MissingTable));
                    continue;
                }

                foreach (var (columnName, expectedTypePrefix) in columns)
                {
                    if (!actualColumns[tableName].Contains(columnName))
                    {
                        errors.Add(new SchemaVerificationError(
                            tableName, columnName,
                            $"Column '{columnName}' is missing from table '{schema}.{tableName}'.",
                            SchemaVerificationErrorKind.MissingColumn));
                    }
                    else if (!actualColumnTypes[tableName][columnName]
                                 .StartsWith(expectedTypePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new SchemaVerificationError(
                            tableName, columnName,
                            $"Column '{columnName}' in table '{schema}.{tableName}' has type '{actualColumnTypes[tableName][columnName]}' but expected prefix '{expectedTypePrefix}'.",
                            SchemaVerificationErrorKind.WrongType));
                    }
                }
            }
        }

        // Verify required indexes
        var expectedIndexes = new (string Table, string Index)[]
        {
            ("ENTITIES", "IX_ENTITIES_EXPIRES_AT"),
            ("ENTITIES", "IX_ENTITIES_ENTITY_TYPE_NAME"),
            ("ENTITIES", "IX_ENTITIES_CREATED_AT"),
            ("ENTITIES", "IX_ENTITIES_LAST_UPDATED_AT"),
            ("ENTITY_KEYS", "IX_ENTITY_KEYS_ENTITY"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_STRING"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_NUMBER"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_DATETIME"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_BOOLEAN"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_GUID"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_STRING"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_NUMBER"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_DATETIME"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_BOOLEAN"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_GUID"),
            ("ENTITY_LINKS", "IX_ENTITY_LINKS_LEFT"),
            ("ENTITY_LINKS", "IX_ENTITY_LINKS_RIGHT"),
            ("ENTITY_LINKS", "IX_ENTITY_LINKS_LEFT_CASCADE"),
            ("ENTITY_LINKS", "IX_ENTITY_LINKS_RIGHT_CASCADE"),
            ("OUTBOX_SUBSCRIBER_QUEUE", "IX_OUTBOX_SUBSCRIBER"),
            ("OUTBOX_SUBSCRIBER_QUEUE", "IX_OUTBOX_POOL_ID"),
        };

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = """
                SELECT TABLE_NAME, INDEX_NAME
                FROM ALL_INDEXES
                WHERE OWNER = :owner
                  AND INDEX_NAME IS NOT NULL
                """;
            Dialect.AddParameter(cmd, ":owner", schema);
            Log.ExecutingSql(logger, cmd.CommandText);

            var actualIndexes = new HashSet<(string Table, string Index)>(
                EqualityComparer<(string, string)>.Create(
                    (a, b) => string.Equals(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase),
                    x => StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item1) ^
                         StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item2)));

            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            await using (reader)
            {
                while (await reader.ReadAsync(ct))
                {
                    _ = actualIndexes.Add((reader.GetString(0), reader.GetString(1)));
                }
            }

            foreach (var (tableName, indexName) in expectedIndexes)
            {
                if (!actualIndexes.Contains((tableName, indexName)))
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, null,
                        $"Index '{indexName}' is missing from table '{schema}.{tableName}'.",
                        SchemaVerificationErrorKind.MissingIndex));
                }
            }
        }

        // Verify required foreign keys
        var expectedForeignKeys = new (string Table, string Fk)[]
        {
            ("ENTITY_KEYS", "FK_ENTITY_KEYS_ENTITIES"),
            ("SEARCH_VALUES", "FK_SEARCH_VALUES_ENTITIES"),
        };

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = """
                SELECT TABLE_NAME, CONSTRAINT_NAME
                FROM ALL_CONSTRAINTS
                WHERE OWNER = :owner
                  AND CONSTRAINT_TYPE = 'R'
                """;
            Dialect.AddParameter(cmd, ":owner", schema);
            Log.ExecutingSql(logger, cmd.CommandText);

            var actualForeignKeys = new HashSet<(string Table, string Fk)>(
                EqualityComparer<(string, string)>.Create(
                    (a, b) => string.Equals(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase),
                    x => StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item1) ^
                         StringComparer.OrdinalIgnoreCase.GetHashCode(x.Item2)));

            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            await using (reader)
            {
                while (await reader.ReadAsync(ct))
                {
                    _ = actualForeignKeys.Add((reader.GetString(0), reader.GetString(1)));
                }
            }

            foreach (var (tableName, fkName) in expectedForeignKeys)
            {
                if (!actualForeignKeys.Contains((tableName, fkName)))
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, null,
                        $"Foreign key '{fkName}' is missing from table '{schema}.{tableName}'.",
                        SchemaVerificationErrorKind.MissingForeignKey));
                }
            }
        }

        // Oracle has no user-defined table types (TVPs) — skip that verification.

        return new SchemaVerificationResult(errors);
    }

    string IDatabaseSchema.BuildMigrationScript(DatabaseSchemaVersion fromVersion)
    {
        var schemaUpper = options.SchemaName?.ToUpperInvariant();
        var prefix = string.IsNullOrEmpty(schemaUpper) ? "" : SchemaPrefix(schemaUpper);

        var scripts = MigrationScriptLoader.GetScripts(typeof(OracleStore).Assembly, fromVersion, prefix);
        var sb = new StringBuilder();
        foreach (var (_, statements) in scripts)
        {
            foreach (var statement in statements)
            {
                _ = sb.AppendLine(statement);
                _ = sb.AppendLine("/");
            }
        }
        return sb.ToString();
    }

    // ───────────────────────── IStore — Create ─────────────────────────

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

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var outcome = await ExecuteCreateCoreAsync(connection, (OracleTransaction)transaction, prefix, createOp, ct);

        if (outcome == OperationOutcome.Success)
        {
            if (outboxEvents is { Count: > 0 })
            {
                await ExecuteOutboxInsertBatchCoreAsync(connection, (OracleTransaction)transaction, prefix, outboxEvents, ct);
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

    // ───────────────────────── IStore — TryRead (by id) ─────────────────────────

    async Task<StoreGetResult> IStore.TryReadAsync(
        EntityType entityType,
        Storage.UuidV7 id,
        Ct ct)
    {
        Log.ReadingDso(logger, entityType, id.Value);

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT value, dso_type_schema_version, value_version, created_at, last_updated_at
            FROM {prefix}ENTITIES
            WHERE entity_type_id = :entityTypeId AND entity_id = :entityId AND pool_id = :poolId
            """;

        Dialect.AddParameter(cmd, ":entityTypeId", (int)entityType.Id);
        Dialect.AddParameter(cmd, ":entityId", id.Value);
        Dialect.AddParameter(cmd, ":poolId", PoolId.Value);

        Log.ExecutingSql(logger, cmd.CommandText);
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
        await using (reader)
        {
            if (!await reader.ReadAsync(ct))
            {
                return StoreGetResult.NotFound();
            }

            var jsonValue = reader.GetString(0);
            var dsoTypeVersion = reader.GetInt32(1);
            var valueVersion = reader.GetInt32(2);
            var created = ReadDateTimeOffset(reader, 3);
            var lastUpdated = ReadDateTimeOffset(reader, 4);

            var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
            var dsoType = dataStorageTypeRegistry.Get(version);
            var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

            return StoreGetResult.IsFound(item, id.Value, valueVersion, created, lastUpdated);
        }
    }

    // ───────────────────────── IStore — TryRead (by key) ─────────────────────────

    async Task<StoreGetResult> IStore.TryReadAsync(
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
    {
        Log.ReadingDso(logger, entityType, key.Value);

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at
            FROM {prefix}ENTITY_KEYS i
            INNER JOIN {prefix}ENTITIES v
                ON i.entity_type_id = v.entity_type_id
                AND i.entity_id = v.entity_id
                AND i.pool_id = v.pool_id
            WHERE i.entity_type_id = :entityTypeId
              AND i.key_type_id = :keyTypeId
              AND i.key_type_version = :keyTypeVersion
              AND i.key_value = :keyValue
              AND i.pool_id = :poolId
            """;

        Dialect.AddParameter(cmd, ":entityTypeId", (int)entityType.Id);
        Dialect.AddParameter(cmd, ":keyTypeId", (int)key.DskVersion.KeyType.Id);
        Dialect.AddParameter(cmd, ":keyTypeVersion", (int)key.DskVersion.SchemaVersion);
        Dialect.AddParameter(cmd, ":keyValue", key.Value);
        Dialect.AddParameter(cmd, ":poolId", PoolId.Value);

        Log.ExecutingSql(logger, cmd.CommandText);
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
        await using (reader)
        {
            if (!await reader.ReadAsync(ct))
            {
                return StoreGetResult.NotFound();
            }

            var entityId = ReadGuid(reader, 0);
            var jsonValue = reader.GetString(1);
            var dsoTypeVersion = reader.GetInt32(2);
            var valueVersion = reader.GetInt32(3);
            var created = ReadDateTimeOffset(reader, 4);
            var lastUpdated = ReadDateTimeOffset(reader, 5);

            var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
            var dsoType = dataStorageTypeRegistry.Get(version);
            var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

            return StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated);
        }
    }

    // ───────────────────────── IStore — TryReadMany ─────────────────────────

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

        if (ids.Count == 0)
        {
            return [];
        }

        Log.ReadingDsos(logger, entityType, ids.Count);

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        // Oracle limits IN (...) to 1000 expressions (ORA-01795), so chunk when needed.
        const int maxInSize = 1000;
        var idList = ids.ToList();
        var results = new List<StoreGetResult>();

        for (var offset = 0; offset < idList.Count; offset += maxInSize)
        {
            var chunk = idList.GetRange(offset, Math.Min(maxInSize, idList.Count - offset));

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var paramNames = new List<string>(chunk.Count);
            for (var i = 0; i < chunk.Count; i++)
            {
                var pName = $":eid{i}";
                paramNames.Add(pName);
                Dialect.AddParameter(cmd, pName, chunk[i].Value);
            }

            cmd.CommandText = $"""
                SELECT e.entity_id, e.value, e.dso_type_schema_version, e.value_version, e.created_at, e.last_updated_at
                FROM {prefix}ENTITIES e
                WHERE e.entity_type_id = :entityTypeId
                  AND e.pool_id = :poolId
                  AND e.entity_id IN ({string.Join(", ", paramNames)})
                """;

            Dialect.AddParameter(cmd, ":entityTypeId", (int)entityType.Id);
            Dialect.AddParameter(cmd, ":poolId", PoolId.Value);

            Log.ExecutingSql(logger, cmd.CommandText);
            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            await using (reader)
            {
                while (await reader.ReadAsync(ct))
                {
                    var entityId = ReadGuid(reader, 0);
                    var jsonValue = reader.GetString(1);
                    var dsoTypeVersion = reader.GetInt32(2);
                    var valueVersion = reader.GetInt32(3);
                    var created = ReadDateTimeOffset(reader, 4);
                    var lastUpdated = ReadDateTimeOffset(reader, 5);

                    var version = new DataStorageObjectVersion(entityType, (uint)dsoTypeVersion);
                    var dsoType = dataStorageTypeRegistry.Get(version);
                    var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                    results.Add(StoreGetResult.IsFound(item, entityId, valueVersion, created, lastUpdated));
                }
            }
        }

        return results;
    }

    // ───────────────────────── IStore — Update ─────────────────────────

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

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var outcome = await ExecuteUpdateCoreAsync(connection, (OracleTransaction)transaction, prefix, updateOp, ct);

        if (outcome == OperationOutcome.Success)
        {
            if (outboxEvents is { Count: > 0 })
            {
                await ExecuteOutboxInsertBatchCoreAsync(connection, (OracleTransaction)transaction, prefix, outboxEvents, ct);
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

    // ───────────────────────── IStore — Delete (by id) ─────────────────────────

    async Task<DeleteResult> IStore.DeleteAsync(EntityType entityType, Storage.UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        var deleteOp = DeleteOperation.ById(entityType, id);

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var (_, entityDeleted) = await ExecuteDeleteCoreAsync(connection, (OracleTransaction)transaction, prefix, deleteOp, ct);

        if (entityDeleted && outboxEvents is { Count: > 0 })
        {
            await ExecuteOutboxInsertBatchCoreAsync(connection, (OracleTransaction)transaction, prefix, outboxEvents, ct);
        }
        await transaction.CommitAsync(ct);

        return DeleteResult.Success;
    }

    // ───────────────────────── IStore — Delete (by key) ─────────────────────────

    async Task<DeleteResult> IStore.DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        var deleteOp = DeleteOperation.ByKey(entityType, key);

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var (_, entityDeleted) = await ExecuteDeleteCoreAsync(connection, (OracleTransaction)transaction, prefix, deleteOp, ct);

        if (entityDeleted && outboxEvents is { Count: > 0 })
        {
            await ExecuteOutboxInsertBatchCoreAsync(connection, (OracleTransaction)transaction, prefix, outboxEvents, ct);
        }
        await transaction.CommitAsync(ct);

        return DeleteResult.Success;
    }

    // ───────────────────────── IStore — Link ─────────────────────────

    /// <inheritdoc/>
    async Task<LinkResult> IStore.LinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var outcome = await ExecuteLinkCoreAsync(connection, (OracleTransaction)transaction, prefix, LinkOperation.For(definition, leftEntityId, rightEntityId), ct);
        if (outcome == OperationOutcome.Success)
        {
            if (outboxEvents is { Count: > 0 })
            {
                await ExecuteOutboxInsertBatchCoreAsync(connection, (OracleTransaction)transaction, prefix, outboxEvents, ct);
            }
            await transaction.CommitAsync(ct);
        }
        return outcome == OperationOutcome.AlreadyLinked ? LinkResult.AlreadyLinked : LinkResult.Success;
    }

    // ───────────────────────── IStore — Unlink ─────────────────────────

    /// <inheritdoc/>
    async Task<UnlinkResult> IStore.UnlinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        _ = await ExecuteUnlinkCoreAsync(connection, (OracleTransaction)transaction, prefix, UnlinkOperation.For(definition, leftEntityId, rightEntityId), ct);
        if (outboxEvents is { Count: > 0 })
        {
            await ExecuteOutboxInsertBatchCoreAsync(connection, (OracleTransaction)transaction, prefix, outboxEvents, ct);
        }
        await transaction.CommitAsync(ct);
        return UnlinkResult.Success;
    }

    // ───────────────────────── ExecuteLinkCoreAsync ─────────────────────────

    private async Task<OperationOutcome> ExecuteLinkCoreAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
        LinkOperation op,
        Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            INSERT INTO {prefix}ENTITY_LINKS (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id)
            VALUES (:poolId, :linkTypeId, :leftEntityTypeId, :leftEntityId, :rightEntityTypeId, :rightEntityId)
            """;
        Dialect.AddParameter(cmd, ":poolId", PoolId.Value);
        Dialect.AddParameter(cmd, ":linkTypeId", (int)op.Definition.Link.Id);
        Dialect.AddParameter(cmd, ":leftEntityTypeId", (int)op.Definition.Left.Id);
        Dialect.AddParameter(cmd, ":leftEntityId", op.LeftEntityId.Value);
        Dialect.AddParameter(cmd, ":rightEntityTypeId", (int)op.Definition.Right.Id);
        Dialect.AddParameter(cmd, ":rightEntityId", op.RightEntityId.Value);

        Log.ExecutingSql(logger, cmd.CommandText);

        try
        {
            _ = await cmd.ExecuteNonQueryAsync(ct);
            return OperationOutcome.Success;
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            // ORA-00001: unique constraint violation — link already exists
            return OperationOutcome.AlreadyLinked;
        }
    }

    // ───────────────────────── ExecuteUnlinkCoreAsync ─────────────────────────

    private async Task<OperationOutcome> ExecuteUnlinkCoreAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
        UnlinkOperation op,
        Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            DELETE FROM {prefix}ENTITY_LINKS
            WHERE pool_id = :poolId
              AND link_type_id = :linkTypeId
              AND left_entity_id = :leftEntityId
              AND right_entity_id = :rightEntityId
            """;
        Dialect.AddParameter(cmd, ":poolId", PoolId.Value);
        Dialect.AddParameter(cmd, ":linkTypeId", (int)op.Definition.Link.Id);
        Dialect.AddParameter(cmd, ":leftEntityId", op.LeftEntityId.Value);
        Dialect.AddParameter(cmd, ":rightEntityId", op.RightEntityId.Value);

        Log.ExecutingSql(logger, cmd.CommandText);

        _ = await cmd.ExecuteNonQueryAsync(ct);
        return OperationOutcome.Success;
    }

    // ───────────────────────── ExecuteOutboxInsertBatchCoreAsync ─────────────────────────

    private async Task ExecuteOutboxInsertBatchCoreAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
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

        // Per-row INSERT (sequence_number is GENERATED ALWAYS AS IDENTITY)
        var sql = $"""
            INSERT INTO {prefix}OUTBOX_SUBSCRIBER_QUEUE
            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version)
            VALUES (:messageId, :eventId, :ts, :eventName, :subjectId, :entityTypeId, :entityTypeName, :poolId, :payload, :subscriberName, :dsoTypeSchemaVersion)
            """;

        foreach (var (evt, subscriber) in rows)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;

            Dialect.AddParameter(cmd, ":messageId", Guid.CreateVersion7());
            Dialect.AddParameter(cmd, ":eventId", evt.Id.Value);
            Dialect.AddParameter(cmd, ":ts", evt.Timestamp);
            Dialect.AddParameter(cmd, ":eventName", evt.EventName.ToString());
            Dialect.AddParameter(cmd, ":subjectId", evt.SubjectId.Value);
            Dialect.AddParameter(cmd, ":entityTypeId", evt.EntityTypeId);
            Dialect.AddParameter(cmd, ":entityTypeName", evt.EntityTypeName);
            Dialect.AddParameter(cmd, ":poolId", PoolId.Value);
            BindClob(cmd, ":payload", evt.Payload);
            Dialect.AddParameter(cmd, ":subscriberName", subscriber.SubscriberName.ToString());
            Dialect.AddParameter(cmd, ":dsoTypeSchemaVersion", evt.DsoTypeSchemaVersion.HasValue ? evt.DsoTypeSchemaVersion.Value : DBNull.Value);

            Log.ExecutingSql(logger, cmd.CommandText);
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    // ───────────────────────── IStore — ExecuteBatch ─────────────────────────

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
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var results = new List<OperationResult>();

        for (var i = 0; i < operations.Count; i++)
        {
            var outcome = operations[i] switch
            {
                CreateOperation create => await ExecuteCreateCoreAsync(connection, (OracleTransaction)transaction, prefix, create, ct),
                UpdateOperation update => await ExecuteUpdateCoreAsync(connection, (OracleTransaction)transaction, prefix, update, ct),
                DeleteOperation delete => (await ExecuteDeleteCoreAsync(connection, (OracleTransaction)transaction, prefix, delete, ct)).Outcome,
                LinkOperation link => await ExecuteLinkCoreAsync(connection, (OracleTransaction)transaction, prefix, link, ct),
                UnlinkOperation unlink => await ExecuteUnlinkCoreAsync(connection, (OracleTransaction)transaction, prefix, unlink, ct),
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
            await ExecuteOutboxInsertBatchCoreAsync(connection, (OracleTransaction)transaction, prefix, outboxEvents, ct);
        }

        await transaction.CommitAsync(ct);
        return new BatchResult(true, results);
    }

    // ───────────────────────── IStore — GetOutboxEventsForSubscriber ─────────────────────────

    async Task<OutboxEventsPage> IStore.GetOutboxEventsForSubscriberAsync(SubscriberName subscriberName, int count, Ct ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT sequence_number, message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version
            FROM {prefix}OUTBOX_SUBSCRIBER_QUEUE
            WHERE subscriber_name = :subscriber_name
            ORDER BY sequence_number ASC
            FETCH FIRST :count ROWS ONLY
            """;

        Dialect.AddParameter(cmd, ":count", count + 1);
        Dialect.AddParameter(cmd, ":subscriber_name", subscriberName.ToString());

        Log.ExecutingSql(logger, cmd.CommandText);
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
        await using (reader)
        {
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
                    MessageId = ReadGuid(reader, 1),
                    EventId = ReadGuid(reader, 2),
                    Timestamp = ReadDateTimeOffset(reader, 3),
                    EventName = OutboxEventName.Create(reader.GetString(4)),
                    SubjectId = Storage.UuidV7.From(ReadGuid(reader, 5)),
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
    }

    // ───────────────────────── IStore — DeleteOutboxEvents ─────────────────────────

    async Task IStore.DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        const int MaxBatchSize = 1000;
        for (var offset = 0; offset < ids.Count; offset += MaxBatchSize)
        {
            var chunk = ids.Skip(offset).Take(MaxBatchSize).ToList();

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var paramNames = chunk.Select((_, i) => $":id{i}").ToList();
            cmd.CommandText = $"""
                DELETE FROM {prefix}OUTBOX_SUBSCRIBER_QUEUE
                WHERE message_id IN ({string.Join(", ", paramNames)})
                """;

            for (var i = 0; i < chunk.Count; i++)
            {
                Dialect.AddParameter(cmd, paramNames[i], chunk[i].Value);
            }

            Log.ExecutingSql(logger, cmd.CommandText);
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    // ───────────────────────── IStore — QueryAsync ─────────────────────────

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

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        var queryClauses = BuildQueryClauses(cmd, schema, prefix, filter, sort, skip);

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
                FROM {prefix}ENTITIES v
                {queryClauses.JoinClause}
                WHERE v.entity_type_id = :entity_type_id
                  AND v.pool_id = :pool_id
                  AND ({queryClauses.WhereClause})
            ),
            total AS (
                SELECT COUNT(*) AS total_count FROM all_matches
            ),
            paged AS (
                SELECT * FROM all_matches
                {pagedOrderBy}
                OFFSET :offset ROWS
                FETCH NEXT :limit ROWS ONLY
            )
            SELECT p.entity_id, p.value, p.dso_type_schema_version, p.value_version, p.created_at, p.last_updated_at, t.total_count
            FROM total t
            LEFT JOIN paged p ON 1=1
            {outerOrderBy}
            """;

        Dialect.AddParameter(cmd, ":entity_type_id", entityTypeId);
        Dialect.AddParameter(cmd, ":pool_id", PoolId.Value);
        Dialect.AddParameter(cmd, ":offset", queryClauses.Offset);
        Dialect.AddParameter(cmd, ":limit", take);

        cmd.CommandText = query;

        Log.ExecutingQuery(logger, query);

        var items = new List<MetadataEnvelope<TDso>>();
        var totalCount = 0;
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
        await using (reader)
        {
            var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
            while (await reader.ReadAsync(ct))
            {
                if (totalCount == 0)
                {
                    totalCount = reader.GetInt32(6);
                }

                if (await reader.IsDBNullAsync(0, ct))
                {
                    continue;
                }

                var entityId = ReadGuid(reader, 0);
                var jsonValue = reader.GetString(1);
                var valueVersion = reader.GetInt32(3);
                var created = ReadDateTimeOffset(reader, 4);
                var lastUpdated = ReadDateTimeOffset(reader, 5);
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

    // ───────────────────────── QueryCursorAsync ─────────────────────────

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

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        var queryClauses = BuildCursorQueryClauses(cmd, schema, prefix, filter, sort, tokenRange);

        var query = $"""
            SELECT v.entity_id, v.value, v.dso_type_schema_version, v.value_version, v.created_at, v.last_updated_at, {queryClauses.SortColumnName}
            FROM {prefix}ENTITIES v
            {queryClauses.JoinClause}
            WHERE v.entity_type_id = :entity_type_id
              AND v.pool_id = :pool_id
              AND ({queryClauses.WhereClause})
              {queryClauses.SeekClause}
            {queryClauses.OrderByClause}
            OFFSET 0 ROWS
            FETCH NEXT :limit ROWS ONLY
            """;

        Dialect.AddParameter(cmd, ":entity_type_id", entityTypeId);
        Dialect.AddParameter(cmd, ":pool_id", PoolId.Value);
        Dialect.AddParameter(cmd, ":limit", pageSize + 1);

        cmd.CommandText = query;

        Log.ExecutingQuery(logger, query);

        var items = new List<(Guid Id, TDso Item, int Version, DateTimeOffset Created, DateTimeOffset LastUpdated, object? SortValue)>();
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
        await using (reader)
        {
            var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
            while (await reader.ReadAsync(ct))
            {
                var entityId = ReadGuid(reader, 0);
                var jsonValue = reader.GetString(1);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                var valueVersion = reader.GetInt32(3);
                var created = ReadDateTimeOffset(reader, 4);
                var lastUpdated = ReadDateTimeOffset(reader, 5);
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

        ContinuationToken? previousToken = null;
        if (pageItems.Count > 0)
        {
            var firstItem = pageItems[0];
            var token = CreateCursorToken(firstItem.Id, firstItem.SortValue);
            previousToken = (ContinuationToken)token.Encode();
        }

        var resultItems = pageItems.Select(x => new MetadataEnvelope<TDso>(x.Item, x.Id, x.Version, x.Created, x.LastUpdated)).ToList();
        return new QueryResult<MetadataEnvelope<TDso>>
        {
            Items = resultItems,
            NextToken = nextToken,
            PreviousToken = previousToken,
            HasMoreData = hasMore
        };
    }

    // ───────────────────────── IStore — QueryFieldsAsync ─────────────────────────

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

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        var queryClauses = BuildQueryClauses(cmd, schema, prefix, filter, sort, skip);

        var fieldPaths = fields.Select(f => f.Path).ToList();
        var fieldConditions = new List<string>();
        var paramIndex = 0;
        for (var i = 0; i < fieldPaths.Count; i++)
        {
            if (SystemFields.IsSystemField(fieldPaths[i]))
            {
                continue;
            }

            Dialect.AddParameter(cmd, $":select_field_{paramIndex}", DeterministicGuidGenerator.Create(fieldPaths[i].ToUpperInvariant()));
            fieldConditions.Add($"field_sv.field_path = :select_field_{paramIndex}");
            paramIndex++;
        }
        var fieldConditionsClause = fieldConditions.Count > 0
            ? string.Join(" OR ", fieldConditions)
            : "1=0";

        string cteSelect;
        string cteJoin;

        if (!sort.IsEmpty)
        {
            var sortColumn = GetSortColumnName(sort.Field!);
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
                FROM {prefix}ENTITIES v
                {cteJoin}
                WHERE v.entity_type_id = :entity_type_id
                  AND v.pool_id = :pool_id
                  AND ({queryClauses.WhereClause})
            ),
            total AS (
                SELECT COUNT(*) AS total_count FROM all_matches
            ),
            filtered_ids AS (
                SELECT * FROM all_matches
                ORDER BY row_num ASC
                OFFSET :offset ROWS
                FETCH NEXT :limit ROWS ONLY
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
            LEFT JOIN {prefix}SEARCH_VALUES field_sv
              ON fi.entity_id = field_sv.entity_id
              AND field_sv.entity_type_id = :entity_type_id
              AND field_sv.pool_id = :pool_id
              AND field_sv.item_index = -1
              AND ({fieldConditionsClause})
            ORDER BY fi.row_num, fi.entity_id
            """;

        Dialect.AddParameter(cmd, ":entity_type_id", entityTypeId);
        Dialect.AddParameter(cmd, ":pool_id", PoolId.Value);
        Dialect.AddParameter(cmd, ":offset", queryClauses.Offset);
        Dialect.AddParameter(cmd, ":limit", take);

        cmd.CommandText = query;

        Log.ExecutingQuery(logger, query);

        var resultsByid = new Dictionary<Guid, (Dictionary<string, object?> FieldValues, DateTimeOffset Created, DateTimeOffset LastUpdated, int Version)>();
        var orderedIds = new List<Guid>();
        var totalCount = 0;
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
        await using (reader)
        {
            while (await reader.ReadAsync(ct))
            {
                if (totalCount == 0)
                {
                    totalCount = reader.GetInt32(7);
                }

                if (await reader.IsDBNullAsync(0, ct))
                {
                    continue;
                }

                var entityId = ReadGuid(reader, 0);
                var fieldPath = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);

                if (!resultsByid.TryGetValue(entityId, out var entry))
                {
                    var fieldValues = new Dictionary<string, object?>();
                    orderedIds.Add(entityId);

                    foreach (var field in fields)
                    {
                        fieldValues[field.Path] = null;
                    }

                    var created = ReadDateTimeOffset(reader, 8);
                    var lastUpdated = ReadDateTimeOffset(reader, 9);
                    var version = reader.GetInt32(10);

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
                    var field = fields.First(f => f.Path == fieldPath);
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

    // ───────────────────────── QueryFieldsCursorAsync ─────────────────────────

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

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        var queryClauses = BuildCursorQueryClauses(cmd, schema, prefix, filter, sort, tokenRange);

        var fieldPaths = fields.Select(f => f.Path).ToList();
        var fieldConditions = new List<string>();
        var paramIndex = 0;
        for (var i = 0; i < fieldPaths.Count; i++)
        {
            if (SystemFields.IsSystemField(fieldPaths[i]))
            {
                continue;
            }

            Dialect.AddParameter(cmd, $":select_field_{paramIndex}", DeterministicGuidGenerator.Create(fieldPaths[i].ToUpperInvariant()));
            fieldConditions.Add($"field_sv.field_path = :select_field_{paramIndex}");
            paramIndex++;
        }
        var fieldConditionsClause = fieldConditions.Count > 0
            ? string.Join(" OR ", fieldConditions)
            : "1=0";

        var query = $"""
            WITH filtered_ids AS (
                SELECT v.entity_id, v.created_at, v.last_updated_at, v.value_version, {queryClauses.SortColumnName} AS sort_value, ROW_NUMBER() OVER ({queryClauses.OrderByClause}) AS row_num
                FROM {prefix}ENTITIES v
                {queryClauses.JoinClause}
                WHERE v.entity_type_id = :entity_type_id
                  AND v.pool_id = :pool_id
                  AND ({queryClauses.WhereClause})
                  {queryClauses.SeekClause}
                {queryClauses.OrderByClause}
                OFFSET 0 ROWS
                FETCH NEXT :limit ROWS ONLY
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
            LEFT JOIN {prefix}SEARCH_VALUES field_sv
              ON fi.entity_id = field_sv.entity_id
              AND field_sv.entity_type_id = :entity_type_id
              AND field_sv.pool_id = :pool_id
              AND field_sv.item_index = -1
              AND ({fieldConditionsClause})
            ORDER BY fi.row_num, fi.entity_id
            """;

        Dialect.AddParameter(cmd, ":entity_type_id", entityTypeId);
        Dialect.AddParameter(cmd, ":pool_id", PoolId.Value);
        Dialect.AddParameter(cmd, ":limit", pageSize + 1);

        cmd.CommandText = query;

        Log.ExecutingQuery(logger, query);

        var resultsByid = new Dictionary<Guid, (Dictionary<string, object?> FieldValues, DateTimeOffset Created, DateTimeOffset LastUpdated, object? SortValue, int Version)>();
        var orderedIds = new List<Guid>();
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
        await using (reader)
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = ReadGuid(reader, 0);
                var fieldPath = await reader.IsDBNullAsync(1, ct) ? null : reader.GetString(1);

                if (!resultsByid.TryGetValue(entityId, out var entry))
                {
                    var fieldValues = new Dictionary<string, object?>();

                    foreach (var field in fields)
                    {
                        fieldValues[field.Path] = null;
                    }

                    var sortValue = await ReadSortValueAsync(reader, sort.Field!, 7, ct);
                    var created = ReadDateTimeOffset(reader, 8);
                    var lastUpdated = ReadDateTimeOffset(reader, 9);
                    var version = reader.GetInt32(10);

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

        ContinuationToken? previousToken = null;
        if (pageItems.Count > 0)
        {
            var firstItem = pageItems[0];
            var token = CreateCursorToken(firstItem.Id, firstItem.Item2.SortValue);
            previousToken = (ContinuationToken)token.Encode();
        }

        var items = pageItems
            .Select(item => new ProjectedResult(item.Id, item.Item2.FieldValues))
            .ToList();

        return new QueryResult<ProjectedResult>
        {
            Items = items,
            NextToken = nextToken,
            PreviousToken = previousToken,
            HasMoreData = hasMore
        };
    }

    // ───────────────────────── ExecuteCreateCoreAsync ─────────────────────────

    private async Task<OperationOutcome> ExecuteCreateCoreAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
        CreateOperation op,
        Ct ct)
    {
        var dsoVersion = op.DsoVersion;
        var entityType = dsoVersion.EntityType;
        var jsonDso = JsonSerializer.Serialize(op.Value);

        var expiresAt = op.Expiration.Resolve(timeProvider);
        if (expiresAt.HasValue && expiresAt.Value <= timeProvider.GetUtcNow())
        {
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
                    INSERT INTO {prefix}ENTITIES (
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
                        :entityTypeId,
                        :entityTypeName,
                        :entityId,
                        :originalEntityId,
                        :value,
                        :dsoTypeSchemaVersion,
                        1,
                        :now,
                        :now,
                        :poolId,
                        :expiresAt
                    )
                    """;

                Dialect.AddParameter(valuesCmd, ":entityTypeId", (int)entityType.Id);
                Dialect.AddParameter(valuesCmd, ":entityTypeName", entityType.Name);
                Dialect.AddParameter(valuesCmd, ":entityId", op.Id.Value);
                Dialect.AddParameter(valuesCmd, ":originalEntityId", op.Id.Value);
                BindClob(valuesCmd, ":value", jsonDso);
                Dialect.AddParameter(valuesCmd, ":dsoTypeSchemaVersion", (int)dsoVersion.SchemaVersion);
                Dialect.AddParameter(valuesCmd, ":now", timeProvider.GetUtcNow());
                Dialect.AddParameter(valuesCmd, ":poolId", PoolId.Value);
#pragma warning disable CA1508
                Dialect.AddParameter(valuesCmd, ":expiresAt", (object?)expiresAt ?? DBNull.Value);
#pragma warning restore CA1508

                Log.ExecutingSql(logger, valuesCmd.CommandText);
                _ = await valuesCmd.ExecuteNonQueryAsync(ct);
            }

            // Per-row insert keys
            if (op.Keys.Count > 0)
            {
                var keySql = $"""
                    INSERT INTO {prefix}ENTITY_KEYS (
                        entity_type_id, key_type_id, key_type_name, key_type_version,
                        key_value, key_json, entity_id, pool_id
                    )
                    VALUES (
                        :entityTypeId, :keyTypeId, :keyTypeName, :keyTypeVersion,
                        :keyValue, :keyJson, :entityId, :poolId
                    )
                    """;

                foreach (var key in op.Keys)
                {
                    await using var keyCmd = connection.CreateCommand();
                    keyCmd.Transaction = transaction;
                    keyCmd.CommandType = CommandType.Text;
                    keyCmd.CommandText = keySql;

                    Dialect.AddParameter(keyCmd, ":entityTypeId", (int)entityType.Id);
                    Dialect.AddParameter(keyCmd, ":keyTypeId", (int)key.DskVersion.KeyType.Id);
                    Dialect.AddParameter(keyCmd, ":keyTypeName", key.DskVersion.KeyType.Name);
                    Dialect.AddParameter(keyCmd, ":keyTypeVersion", (int)key.DskVersion.SchemaVersion);
                    Dialect.AddParameter(keyCmd, ":keyValue", key.Value);
                    BindClob(keyCmd, ":keyJson", key.KeyJsonValue);
                    Dialect.AddParameter(keyCmd, ":entityId", op.Id.Value);
                    Dialect.AddParameter(keyCmd, ":poolId", PoolId.Value);

                    Log.ExecutingSql(logger, keyCmd.CommandText);
                    _ = await keyCmd.ExecuteNonQueryAsync(ct);
                }
            }

            // Per-row insert search fields
            if (op.SearchFieldCollection.Count > 0)
            {
                await InsertSearchValuesAsync(connection, transaction, prefix, entityType, op.Id.Value, op.SearchFieldCollection, ct);
            }

            return OperationOutcome.Success;
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            // ORA-00001: unique constraint violation
            if (ex.Message.Contains("PK_ENTITIES", StringComparison.OrdinalIgnoreCase))
            {
                return OperationOutcome.AlreadyExists;
            }

            if (ex.Message.Contains("PK_ENTITY_KEYS", StringComparison.OrdinalIgnoreCase))
            {
                return OperationOutcome.KeyConflict;
            }

            throw;
        }
    }

    // ───────────────────────── ExecuteUpdateCoreAsync ─────────────────────────

    private async Task<OperationOutcome> ExecuteUpdateCoreAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
        UpdateOperation op,
        Ct ct)
    {
        var dsoVersion = op.DsoVersion;
        var entityType = dsoVersion.EntityType;
        var jsonDso = JsonSerializer.Serialize(op.Value);

        DateTimeOffset? expiresAt = null;
        var hasExpirationChange = op.Expiration is not null;
        if (hasExpirationChange)
        {
            expiresAt = op.Expiration!.Resolve(timeProvider);
        }

        Log.UpdatingDso(logger, entityType, op.Id.Value, dsoVersion.SchemaVersion, op.ExpectedEntityVersion);

        try
        {
            // Step 1: Read current version with row lock
            int? actualVersion;
            await using (var lockCmd = connection.CreateCommand())
            {
                lockCmd.Transaction = transaction;
                lockCmd.CommandType = CommandType.Text;
                lockCmd.CommandText = $"""
                    SELECT value_version
                    FROM {prefix}ENTITIES
                    WHERE entity_type_id = :entityTypeId AND entity_id = :entityId AND pool_id = :poolId
                    FOR UPDATE
                    """;

                Dialect.AddParameter(lockCmd, ":entityTypeId", (int)entityType.Id);
                Dialect.AddParameter(lockCmd, ":entityId", op.Id.Value);
                Dialect.AddParameter(lockCmd, ":poolId", PoolId.Value);

                Log.ExecutingSql(logger, lockCmd.CommandText);
                var scalar = await lockCmd.ExecuteScalarAsync(ct);
                actualVersion = scalar is null or DBNull ? null : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            }

            if (actualVersion is null)
            {
                return OperationOutcome.DoesNotExist;
            }

            if (actualVersion.Value != op.ExpectedEntityVersion)
            {
                return OperationOutcome.UnexpectedVersion;
            }

            // Step 2: Delete existing keys and search values
            await using (var delKeysCmd = connection.CreateCommand())
            {
                delKeysCmd.Transaction = transaction;
                delKeysCmd.CommandType = CommandType.Text;
                delKeysCmd.CommandText = $"""
                    DELETE FROM {prefix}ENTITY_KEYS
                    WHERE entity_type_id = :entityTypeId AND entity_id = :entityId AND pool_id = :poolId
                    """;
                Dialect.AddParameter(delKeysCmd, ":entityTypeId", (int)entityType.Id);
                Dialect.AddParameter(delKeysCmd, ":entityId", op.Id.Value);
                Dialect.AddParameter(delKeysCmd, ":poolId", PoolId.Value);
                Log.ExecutingSql(logger, delKeysCmd.CommandText);
                _ = await delKeysCmd.ExecuteNonQueryAsync(ct);
            }

            await using (var delSvCmd = connection.CreateCommand())
            {
                delSvCmd.Transaction = transaction;
                delSvCmd.CommandType = CommandType.Text;
                delSvCmd.CommandText = $"""
                    DELETE FROM {prefix}SEARCH_VALUES
                    WHERE entity_type_id = :entityTypeId AND entity_id = :entityId AND pool_id = :poolId
                    """;
                Dialect.AddParameter(delSvCmd, ":entityTypeId", (int)entityType.Id);
                Dialect.AddParameter(delSvCmd, ":entityId", op.Id.Value);
                Dialect.AddParameter(delSvCmd, ":poolId", PoolId.Value);
                Log.ExecutingSql(logger, delSvCmd.CommandText);
                _ = await delSvCmd.ExecuteNonQueryAsync(ct);
            }

            // Step 3: Update the main entities record
            await using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = transaction;
                updateCmd.CommandType = CommandType.Text;

                var expiresAtSql = hasExpirationChange
                    ? "expires_at = :expiresAt,"
                    : "";

                updateCmd.CommandText = $"""
                    UPDATE {prefix}ENTITIES
                    SET
                        entity_type_name = :entityTypeName,
                        value = :value,
                        dso_type_schema_version = :dsoTypeSchemaVersion,
                        value_version = value_version + 1,
                        {expiresAtSql}
                        last_updated_at = :now
                    WHERE entity_type_id = :entityTypeId AND entity_id = :entityId AND pool_id = :poolId
                    """;

                Dialect.AddParameter(updateCmd, ":entityTypeName", entityType.Name);
                BindClob(updateCmd, ":value", jsonDso);
                Dialect.AddParameter(updateCmd, ":dsoTypeSchemaVersion", (int)dsoVersion.SchemaVersion);
                Dialect.AddParameter(updateCmd, ":now", timeProvider.GetUtcNow());
                Dialect.AddParameter(updateCmd, ":entityTypeId", (int)entityType.Id);
                Dialect.AddParameter(updateCmd, ":entityId", op.Id.Value);
                Dialect.AddParameter(updateCmd, ":poolId", PoolId.Value);
                if (hasExpirationChange)
                {
#pragma warning disable CA1508
                    Dialect.AddParameter(updateCmd, ":expiresAt", (object?)expiresAt ?? DBNull.Value);
#pragma warning restore CA1508
                }

                Log.ExecutingSql(logger, updateCmd.CommandText);
                _ = await updateCmd.ExecuteNonQueryAsync(ct);
            }

            // Step 4: Re-insert keys
            if (op.Keys.Count > 0)
            {
                var keySql = $"""
                    INSERT INTO {prefix}ENTITY_KEYS (
                        entity_type_id, key_type_id, key_type_name, key_type_version,
                        key_value, key_json, entity_id, pool_id
                    )
                    VALUES (
                        :entityTypeId, :keyTypeId, :keyTypeName, :keyTypeVersion,
                        :keyValue, :keyJson, :entityId, :poolId
                    )
                    """;

                foreach (var key in op.Keys)
                {
                    await using var keyCmd = connection.CreateCommand();
                    keyCmd.Transaction = transaction;
                    keyCmd.CommandType = CommandType.Text;
                    keyCmd.CommandText = keySql;

                    Dialect.AddParameter(keyCmd, ":entityTypeId", (int)entityType.Id);
                    Dialect.AddParameter(keyCmd, ":keyTypeId", (int)key.DskVersion.KeyType.Id);
                    Dialect.AddParameter(keyCmd, ":keyTypeName", key.DskVersion.KeyType.Name);
                    Dialect.AddParameter(keyCmd, ":keyTypeVersion", (int)key.DskVersion.SchemaVersion);
                    Dialect.AddParameter(keyCmd, ":keyValue", key.Value);
                    BindClob(keyCmd, ":keyJson", key.KeyJsonValue);
                    Dialect.AddParameter(keyCmd, ":entityId", op.Id.Value);
                    Dialect.AddParameter(keyCmd, ":poolId", PoolId.Value);

                    Log.ExecutingSql(logger, keyCmd.CommandText);
                    _ = await keyCmd.ExecuteNonQueryAsync(ct);
                }
            }

            // Step 5: Re-insert search values
            if (op.SearchFieldCollection.Count > 0)
            {
                await InsertSearchValuesAsync(connection, transaction, prefix, entityType, op.Id.Value, op.SearchFieldCollection, ct);
            }

            return OperationOutcome.Success;
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            if (ex.Message.Contains("PK_ENTITY_KEYS", StringComparison.OrdinalIgnoreCase))
            {
                return OperationOutcome.KeyConflict;
            }

            throw;
        }
    }

    // ───────────────────────── ExecuteDeleteCoreAsync ─────────────────────────

    private async Task<(OperationOutcome Outcome, bool EntityDeleted)> ExecuteDeleteCoreAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
        DeleteOperation op,
        Ct ct)
    {
        var entityType = op.EntityType;

        Guid? deletedEntityId = null;

        if (op.Id is not null)
        {
            Log.DeletingDso(logger, entityType, op.Id.Value);

            // First find the entity_id so we can delete links
            await using var selectCmd = connection.CreateCommand();
            selectCmd.Transaction = transaction;
            selectCmd.CommandType = CommandType.Text;
            selectCmd.CommandText = $"""
                SELECT entity_id FROM {prefix}ENTITIES
                WHERE entity_type_id = :entityTypeId AND entity_id = :entityId AND pool_id = :poolId
                FOR UPDATE
                """;
            Dialect.AddParameter(selectCmd, ":entityTypeId", (int)entityType.Id);
            Dialect.AddParameter(selectCmd, ":entityId", op.Id.Value);
            Dialect.AddParameter(selectCmd, ":poolId", PoolId.Value);
            Log.ExecutingSql(logger, selectCmd.CommandText);
            var scalar = await selectCmd.ExecuteScalarAsync(ct);
            if (scalar is not null and not DBNull)
            {
                deletedEntityId = OracleGuidConverter.FromRaw((byte[])scalar);
            }

            if (deletedEntityId.HasValue)
            {
                await using var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandType = CommandType.Text;
                deleteCmd.CommandText = $"""
                    DELETE FROM {prefix}ENTITIES
                    WHERE entity_type_id = :entityTypeId AND entity_id = :entityId AND pool_id = :poolId
                    """;
                Dialect.AddParameter(deleteCmd, ":entityTypeId", (int)entityType.Id);
                Dialect.AddParameter(deleteCmd, ":entityId", op.Id.Value);
                Dialect.AddParameter(deleteCmd, ":poolId", PoolId.Value);
                Log.ExecutingSql(logger, deleteCmd.CommandText);
                _ = await deleteCmd.ExecuteNonQueryAsync(ct);
            }
        }
        else if (op.Key is not null)
        {
            var key = op.Key;

            // Look up entity_id from the key
            await using var findCmd = connection.CreateCommand();
            findCmd.Transaction = transaction;
            findCmd.CommandType = CommandType.Text;
            findCmd.CommandText = $"""
                SELECT entity_id
                FROM {prefix}ENTITY_KEYS
                WHERE entity_type_id = :entityTypeId
                  AND key_type_id = :keyTypeId
                  AND key_type_version = :keyTypeVersion
                  AND key_value = :keyValue
                  AND pool_id = :poolId
                """;
            Dialect.AddParameter(findCmd, ":entityTypeId", (int)entityType.Id);
            Dialect.AddParameter(findCmd, ":keyTypeId", (int)key.DskVersion.KeyType.Id);
            Dialect.AddParameter(findCmd, ":keyTypeVersion", (int)key.DskVersion.SchemaVersion);
            Dialect.AddParameter(findCmd, ":keyValue", key.Value);
            Dialect.AddParameter(findCmd, ":poolId", PoolId.Value);
            Log.ExecutingSql(logger, findCmd.CommandText);
            var keyResult = await findCmd.ExecuteScalarAsync(ct);

            if (keyResult is not null and not DBNull)
            {
                deletedEntityId = OracleGuidConverter.FromRaw((byte[])keyResult);

                await using var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandType = CommandType.Text;
                deleteCmd.CommandText = $"""
                    DELETE FROM {prefix}ENTITIES
                    WHERE entity_type_id = :entityTypeId AND entity_id = :entityId AND pool_id = :poolId
                    """;
                Dialect.AddParameter(deleteCmd, ":entityTypeId", (int)entityType.Id);
                Dialect.AddParameter(deleteCmd, ":entityId", deletedEntityId.Value);
                Dialect.AddParameter(deleteCmd, ":poolId", PoolId.Value);
                Log.ExecutingSql(logger, deleteCmd.CommandText);
                _ = await deleteCmd.ExecuteNonQueryAsync(ct);
            }
        }
        else
        {
            return (OperationOutcome.Success, false);
        }

        // Delete entity links (no FK to entities, must be done manually)
        if (deletedEntityId.HasValue)
        {
            await using var linkDeleteCmd = connection.CreateCommand();
            linkDeleteCmd.Transaction = transaction;
            linkDeleteCmd.CommandType = CommandType.Text;
            linkDeleteCmd.CommandText = $"""
                DELETE FROM {prefix}ENTITY_LINKS
                WHERE pool_id = :poolId
                  AND (left_entity_id = :entityId OR right_entity_id = :entityId)
                """;
            Dialect.AddParameter(linkDeleteCmd, ":poolId", PoolId.Value);
            Dialect.AddParameter(linkDeleteCmd, ":entityId", deletedEntityId.Value);
            Log.ExecutingSql(logger, linkDeleteCmd.CommandText);
            _ = await linkDeleteCmd.ExecuteNonQueryAsync(ct);
        }

        return (OperationOutcome.Success, deletedEntityId.HasValue);
    }

    // ───────────────────────── InsertSearchValuesAsync (shared helper) ─────────────────────────

    private async Task InsertSearchValuesAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
        EntityType entityType,
        Guid entityId,
        SearchFieldCollection searchFieldCollection,
        Ct ct)
    {
        var svSql = $"""
            INSERT INTO {prefix}SEARCH_VALUES (
                entity_type_id, entity_id, field_path, field_path_text, item_index,
                string_value, number_value, datetime_value, boolean_value, guid_value, pool_id
            )
            VALUES (
                :entityTypeId, :entityId, :fieldPath, :fieldPathText, :itemIndex,
                :stringValue, :numberValue, :datetimeValue, :booleanValue, :guidValue, :poolId
            )
            """;

        foreach (var field in searchFieldCollection)
        {
            await using var svCmd = connection.CreateCommand();
            svCmd.Transaction = transaction;
            svCmd.CommandType = CommandType.Text;
            svCmd.CommandText = svSql;

            Dialect.AddParameter(svCmd, ":entityTypeId", (int)entityType.Id);
            Dialect.AddParameter(svCmd, ":entityId", entityId);
            Dialect.AddParameter(svCmd, ":fieldPath", field.FieldPathId);
            Dialect.AddParameter(svCmd, ":fieldPathText", field.FieldPath);
            Dialect.AddParameter(svCmd, ":itemIndex", field.ItemIndex ?? -1);
            Dialect.AddParameter(svCmd, ":stringValue", (object?)field.StringValue ?? DBNull.Value);
            Dialect.AddParameter(svCmd, ":numberValue", field.NumberValue.HasValue ? field.NumberValue.Value : DBNull.Value);
            Dialect.AddParameter(svCmd, ":datetimeValue", field.DateTimeValue.HasValue ? field.DateTimeValue.Value : DBNull.Value);
            Dialect.AddParameter(svCmd, ":booleanValue", field.BooleanValue.HasValue ? field.BooleanValue.Value : DBNull.Value);
            Dialect.AddParameter(svCmd, ":guidValue", field.GuidValue.HasValue ? field.GuidValue.Value : DBNull.Value);
            Dialect.AddParameter(svCmd, ":poolId", PoolId.Value);

            Log.ExecutingSql(logger, svCmd.CommandText);
            _ = await svCmd.ExecuteNonQueryAsync(ct);
        }
    }

    // ───────────────────────── IStore — QueryLinksAsync ─────────────────────────

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

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        var joinSql = new StringBuilder();
        var whereLastJoin = "";

        for (var i = 0; i < query.Joins.Count; i++)
        {
            var join = query.Joins[i];
            var linkTypeParam = $":lt{i}";
            Dialect.AddParameter(cmd, linkTypeParam, (int)join.Definition.Link.Id);

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
                    $"JOIN {prefix}ENTITY_LINKS l0 ON l0.{sourceSide} = e.entity_id AND l0.link_type_id = {linkTypeParam} AND l0.pool_id = :pool_id");
            }
            else
            {
                var prevJoin = query.Joins[i - 1];
                var prevFilterSide = prevJoin.Direction == LinkJoinDirection.LeftToRight
                    ? "right_entity_id"
                    : "left_entity_id";

                _ = joinSql.AppendLine(CultureInfo.InvariantCulture,
                    $"JOIN {prefix}ENTITY_LINKS l{i} ON l{i}.{sourceSide} = l{i - 1}.{prevFilterSide} AND l{i}.link_type_id = {linkTypeParam} AND l{i}.pool_id = :pool_id");
            }

            if (i == query.Joins.Count - 1)
            {
                whereLastJoin = $"l{i}.{filterSide}";
            }
        }

        Dialect.AddParameter(cmd, ":pool_id", PoolId.Value);
        Dialect.AddParameter(cmd, ":source_entity_type_id", sourceEntityTypeId);
        Dialect.AddParameter(cmd, ":offset", skip);
        Dialect.AddParameter(cmd, ":limit", take);

        string whereClause;
        if (query.WhereEntityId is not null)
        {
            Dialect.AddParameter(cmd, ":where_entity_id", query.WhereEntityId.Value);
            whereClause = $"{whereLastJoin} = :where_entity_id";
        }
        else
        {
            whereClause = "1=1";
        }

        // Avoid SELECT DISTINCT on CLOB columns — use a CTE with DISTINCT on entity_id only
        var mainQuery = $"""
            WITH matched AS (
                SELECT DISTINCT e.entity_id
                FROM {prefix}ENTITIES e
                {joinSql}
                WHERE e.entity_type_id = :source_entity_type_id
                  AND e.pool_id = :pool_id
                  AND {whereClause}
            )
            SELECT ent.entity_id, ent.value, ent.dso_type_schema_version, ent.value_version, ent.created_at, ent.last_updated_at
            FROM matched m
            JOIN {prefix}ENTITIES ent ON ent.entity_id = m.entity_id
                AND ent.entity_type_id = :source_entity_type_id
                AND ent.pool_id = :pool_id
            ORDER BY ent.entity_id
            OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY
            """;

        cmd.CommandText = mainQuery;
        Log.ExecutingQuery(logger, mainQuery);

        var items = new List<MetadataEnvelope<TDso>>();
        var dsoType = dataStorageTypeRegistry.Get(dsoVersion);
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
        await using (reader)
        {
            while (await reader.ReadAsync(ct))
            {
                var entityId = ReadGuid(reader, 0);
                var jsonValue = reader.GetString(1);
                var valueVersion = reader.GetInt32(3);
                var created = ReadDateTimeOffset(reader, 4);
                var lastUpdated = ReadDateTimeOffset(reader, 5);
                var item = (TDso)JsonSerializer.Deserialize(jsonValue, dsoType)!;
                items.Add(new MetadataEnvelope<TDso>(item, entityId, valueVersion, created, lastUpdated));
            }
        }

        // Count query
        var countQuery = $"""
            SELECT COUNT(DISTINCT e.entity_id)
            FROM {prefix}ENTITIES e
            {joinSql}
            WHERE e.entity_type_id = :source_entity_type_id
              AND e.pool_id = :pool_id
              AND {whereClause}
            """;

        await using var countCmd = connection.CreateCommand();
        countCmd.CommandType = CommandType.Text;
        Dialect.AddParameter(countCmd, ":source_entity_type_id", sourceEntityTypeId);
        Dialect.AddParameter(countCmd, ":pool_id", PoolId.Value);
        if (query.WhereEntityId is not null)
        {
            Dialect.AddParameter(countCmd, ":where_entity_id", query.WhereEntityId.Value);
        }

        for (var i = 0; i < query.Joins.Count; i++)
        {
            Dialect.AddParameter(countCmd, $":lt{i}", (int)query.Joins[i].Definition.Link.Id);
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

    // ───────────────────────── IStore — CountAsync ─────────────────────────

    async Task<long> IStore.CountAsync(
        EntityType entityType,
        IQueryExpression? filter,
        Ct ct)
    {
        var entityTypeId = (int)entityType.Id;

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        string whereClause;
        if (filter is null or AllExpression)
        {
            whereClause = "1=1";
        }
        else
        {
            var whereBuilder = new SqlWhereClauseBuilder(schema, cmd, Dialect);
            whereClause = whereBuilder.BuildWhereClause(filter).Replace("@", ":", StringComparison.Ordinal);
        }

        var query = $"""
            SELECT COUNT(*)
            FROM {prefix}ENTITIES v
            WHERE v.entity_type_id = :entity_type_id
              AND v.pool_id = :pool_id
              AND ({whereClause})
            """;

        Dialect.AddParameter(cmd, ":entity_type_id", entityTypeId);
        Dialect.AddParameter(cmd, ":pool_id", PoolId.Value);

        cmd.CommandText = query;

        Log.ExecutingQuery(logger, query);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    // ───────────────────────── IStore — PurgeExpiredAsync ─────────────────────────

    async Task<int> IStore.PurgeExpiredAsync(int batchSize, Ct ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, StorageConstants.TtlCleanupMaxBatchSize);

        var now = timeProvider.GetUtcNow();

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // Step 1: Find and lock expired entities
        var expired = new List<(int PoolId, Guid EntityId, int EntityTypeId, string EntityTypeName, string Value, int? DsoTypeSchemaVersion)>();
        await using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.Transaction = (OracleTransaction)transaction;
            selectCmd.CommandType = CommandType.Text;
            selectCmd.CommandText = $"""
                SELECT pool_id, entity_id, entity_type_id, entity_type_name, value, dso_type_schema_version
                FROM {prefix}ENTITIES
                WHERE expires_at IS NOT NULL AND expires_at <= :now AND ROWNUM <= :batchSize
                FOR UPDATE SKIP LOCKED
                """;
            Dialect.AddParameter(selectCmd, ":now", now);
            Dialect.AddParameter(selectCmd, ":batchSize", batchSize);

            Log.ExecutingSql(logger, selectCmd.CommandText);
            var reader = (OracleDataReader)await selectCmd.ExecuteReaderAsync(ct);
            await using (reader)
            {
                while (await reader.ReadAsync(ct))
                {
                    var dsoTypeSchemaVersion = await reader.IsDBNullAsync(5, ct) ? null : (int?)reader.GetInt32(5);
                    expired.Add((
                        reader.GetInt32(0),
                        ReadGuid(reader, 1),
                        reader.GetInt32(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        dsoTypeSchemaVersion));
                }
            }
        }

        if (expired.Count == 0)
        {
            await transaction.CommitAsync(ct);
            return 0;
        }

        // Step 2: Insert outbox events per matching subscriber
        if (!outboxSubscribers.IsEmpty)
        {
            var eventName = OutboxEventName.EntityExpired;

            foreach (var (poolId, entityId, entityTypeId, entityTypeName, value, dsoTypeSchemaVersion) in expired)
            {
                var eventId = Guid.CreateVersion7();

                foreach (var subscriber in outboxSubscribers.Subscribers)
                {
                    if (subscriber.EventNames.Count > 0 && !subscriber.EventNames.Contains(eventName))
                    {
                        continue;
                    }
                    if (subscriber.EntityTypeIds.Count > 0 && !subscriber.EntityTypeIds.Contains(entityTypeId))
                    {
                        continue;
                    }

                    await using var outboxCmd = connection.CreateCommand();
                    outboxCmd.Transaction = (OracleTransaction)transaction;
                    outboxCmd.CommandType = CommandType.Text;
                    outboxCmd.CommandText = $"""
                        INSERT INTO {prefix}OUTBOX_SUBSCRIBER_QUEUE
                        (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version)
                        VALUES (:messageId, :eventId, :ts, :eventName, :subjectId, :entityTypeId, :entityTypeName, :poolId, :payload, :subscriberName, :dsoTypeSchemaVersion)
                        """;
                    Dialect.AddParameter(outboxCmd, ":messageId", Guid.CreateVersion7());
                    Dialect.AddParameter(outboxCmd, ":eventId", eventId);
                    Dialect.AddParameter(outboxCmd, ":ts", now);
                    Dialect.AddParameter(outboxCmd, ":eventName", eventName.ToString());
                    Dialect.AddParameter(outboxCmd, ":subjectId", entityId);
                    Dialect.AddParameter(outboxCmd, ":entityTypeId", entityTypeId);
                    Dialect.AddParameter(outboxCmd, ":entityTypeName", entityTypeName);
                    Dialect.AddParameter(outboxCmd, ":poolId", poolId);
                    BindClob(outboxCmd, ":payload", value);
                    Dialect.AddParameter(outboxCmd, ":subscriberName", subscriber.SubscriberName.ToString());
                    Dialect.AddParameter(outboxCmd, ":dsoTypeSchemaVersion", dsoTypeSchemaVersion.HasValue ? dsoTypeSchemaVersion.Value : DBNull.Value);
                    _ = await outboxCmd.ExecuteNonQueryAsync(ct);
                }
            }
        }

        // Step 3: Delete entity links (no FK cascade from entities)
        foreach (var (poolId, entityId, _, _, _, _) in expired)
        {
            await using var linkCmd = connection.CreateCommand();
            linkCmd.Transaction = (OracleTransaction)transaction;
            linkCmd.CommandType = CommandType.Text;
            linkCmd.CommandText = $"""
                DELETE FROM {prefix}ENTITY_LINKS
                WHERE pool_id = :poolId
                  AND (left_entity_id = :entityId OR right_entity_id = :entityId)
                """;
            Dialect.AddParameter(linkCmd, ":poolId", poolId);
            Dialect.AddParameter(linkCmd, ":entityId", entityId);
            _ = await linkCmd.ExecuteNonQueryAsync(ct);
        }

        // Step 4: Delete entities (keys & search values cascade via FK ON DELETE CASCADE)
        foreach (var (poolId, entityId, entityTypeId, _, _, _) in expired)
        {
            await using var delCmd = connection.CreateCommand();
            delCmd.Transaction = (OracleTransaction)transaction;
            delCmd.CommandType = CommandType.Text;
            delCmd.CommandText = $"""
                DELETE FROM {prefix}ENTITIES
                WHERE pool_id = :poolId AND entity_type_id = :entityTypeId AND entity_id = :entityId
                  AND expires_at <= :now
                """;
            Dialect.AddParameter(delCmd, ":poolId", poolId);
            Dialect.AddParameter(delCmd, ":entityTypeId", entityTypeId);
            Dialect.AddParameter(delCmd, ":entityId", entityId);
            Dialect.AddParameter(delCmd, ":now", now);
            _ = await delCmd.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return expired.Count;
    }

    Task<PurgeResult> IStore.PurgePoolAsync(Ct ct) => ((IStore)this).PurgePoolAsync(StorageConstants.PurgePoolDefaultBatchSize, ct);

    async Task<PurgeResult> IStore.PurgePoolAsync(int batchSize, Ct ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var totalOutboxDeleted = 0;
        var totalLinksDeleted = 0;
        var totalEntitiesDeleted = 0;

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            await using var transaction = await connection.BeginTransactionAsync(ct);

            int batchOutbox;
            int batchLinks;
            int batchEntities;

            await using (var outboxCmd = connection.CreateCommand())
            {
                outboxCmd.Transaction = (OracleTransaction)transaction;
                outboxCmd.CommandType = CommandType.Text;
                outboxCmd.CommandText = $"""
                    DELETE FROM {prefix}OUTBOX_SUBSCRIBER_QUEUE
                    WHERE pool_id = :poolId AND ROWNUM <= :batchSize
                    """;
                Dialect.AddParameter(outboxCmd, ":poolId", PoolId.Value);
                Dialect.AddParameter(outboxCmd, ":batchSize", batchSize);

                Log.ExecutingSql(logger, outboxCmd.CommandText);
                batchOutbox = await outboxCmd.ExecuteNonQueryAsync(ct);
            }

            await using (var linksCmd = connection.CreateCommand())
            {
                linksCmd.Transaction = (OracleTransaction)transaction;
                linksCmd.CommandType = CommandType.Text;
                linksCmd.CommandText = $"""
                    DELETE FROM {prefix}ENTITY_LINKS
                    WHERE pool_id = :poolId AND ROWNUM <= :batchSize
                    """;
                Dialect.AddParameter(linksCmd, ":poolId", PoolId.Value);
                Dialect.AddParameter(linksCmd, ":batchSize", batchSize);

                Log.ExecutingSql(logger, linksCmd.CommandText);
                batchLinks = await linksCmd.ExecuteNonQueryAsync(ct);
            }

            await using (var entitiesCmd = connection.CreateCommand())
            {
                entitiesCmd.Transaction = (OracleTransaction)transaction;
                entitiesCmd.CommandType = CommandType.Text;
                entitiesCmd.CommandText = $"""
                    DELETE FROM {prefix}ENTITIES
                    WHERE pool_id = :poolId AND ROWNUM <= :batchSize
                    """;
                Dialect.AddParameter(entitiesCmd, ":poolId", PoolId.Value);
                Dialect.AddParameter(entitiesCmd, ":batchSize", batchSize);

                Log.ExecutingSql(logger, entitiesCmd.CommandText);
                batchEntities = await entitiesCmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);

            totalOutboxDeleted += batchOutbox;
            totalLinksDeleted += batchLinks;
            totalEntitiesDeleted += batchEntities;

            if (batchOutbox + batchLinks + batchEntities == 0)
            {
                break;
            }
        }

        return new PurgeResult(totalEntitiesDeleted, totalLinksDeleted, totalOutboxDeleted);
    }

    // ───────────────────────── Query clause builders ─────────────────────────

    private static QueryClauses BuildQueryClauses(
        OracleCommand cmd,
        string schema,
        string prefix,
        IQueryExpression filter,
        SortParameter sort,
        int offset)
    {
        var whereBuilder = new SqlWhereClauseBuilder(schema, cmd, Dialect);
        var whereClause = whereBuilder.BuildWhereClause(filter).Replace("@", ":", StringComparison.Ordinal);

        string joinClause;
        string orderByClause;

        if (!sort.IsEmpty)
        {
            var sortFieldPath = sort.Field!.Path;
            var sortColumn = GetSortColumnName(sort.Field!);

            if (SystemFields.IsSystemField(sortFieldPath))
            {
                joinClause = "";
            }
            else
            {
                joinClause = $"""
                    LEFT JOIN {prefix}SEARCH_VALUES sort_sv
                      ON v.entity_type_id = sort_sv.entity_type_id
                      AND v.entity_id = sort_sv.entity_id
                      AND v.pool_id = sort_sv.pool_id
                      AND sort_sv.field_path = :sort_field_path
                      AND sort_sv.item_index = -1
                    """;

                Dialect.AddParameter(cmd, ":sort_field_path", DeterministicGuidGenerator.Create(sortFieldPath.ToUpperInvariant()));
            }

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

    private static CursorQueryClauses BuildCursorQueryClauses(
        OracleCommand cmd,
        string schema,
        string prefix,
        IQueryExpression filter,
        SortParameter sort,
        ContinuationTokenDataRange tokenRange)
    {
        var whereBuilder = new SqlWhereClauseBuilder(schema, cmd, Dialect);
        var whereClause = whereBuilder.BuildWhereClause(filter).Replace("@", ":", StringComparison.Ordinal);

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
                LEFT JOIN {prefix}SEARCH_VALUES sort_sv
                  ON v.entity_type_id = sort_sv.entity_type_id
                  AND v.entity_id = sort_sv.entity_id
                  AND v.pool_id = sort_sv.pool_id
                  AND sort_sv.field_path = :sort_field_path
                  AND sort_sv.item_index = -1
                """;

            Dialect.AddParameter(cmd, ":sort_field_path", DeterministicGuidGenerator.Create(sortFieldPath.ToUpperInvariant()));
        }

        // Build ORDER BY and seek clauses
        var tokenValue = tokenRange.Start.Value;

        var orderByClause = $"""
            ORDER BY
              CASE WHEN {sortColumn} IS NULL THEN 1 ELSE 0 END,
              {sortColumn} {sortDirection},
              v.entity_id ASC
            """;

        var seekClause = "";
        if (tokenValue != ContinuationToken.Beginning)
        {
            var decodedToken = CursorToken.Decode(tokenValue);
            if (decodedToken != null)
            {
                var lastSortParam = ":last_sort_value";
                var lastIdParam = ":last_id";

                if (decodedToken.GuidValue.HasValue)
                {
                    Dialect.AddParameter(cmd, lastSortParam, decodedToken.GuidValue.Value);
                }
                else if (decodedToken.StringValue != null)
                {
                    Dialect.AddParameter(cmd, lastSortParam, decodedToken.StringValue);
                }
                else if (decodedToken.NumberValue.HasValue)
                {
                    Dialect.AddParameter(cmd, lastSortParam, decodedToken.NumberValue.Value);
                }
                else if (decodedToken.DateTimeValue.HasValue)
                {
                    Dialect.AddParameter(cmd, lastSortParam, decodedToken.DateTimeValue.Value);
                }
                else if (decodedToken.BooleanValue.HasValue)
                {
                    Dialect.AddParameter(cmd, lastSortParam, decodedToken.BooleanValue.Value);
                }
                else
                {
                    Dialect.AddParameter(cmd, lastSortParam, DBNull.Value);
                }

                Dialect.AddParameter(cmd, lastIdParam, decodedToken.Id);

                if (sort.Direction == SortDirection.Ascending)
                {
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

    // ───────────────────────── Sort / field helpers ─────────────────────────

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

    private static async Task<object?> ReadFieldValueAsync(OracleDataReader reader, FieldType fieldType, int stringValueColumnIndex, Ct ct)
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
            FieldType.Number => reader.GetDecimal(columnIndex),
            FieldType.DateTime => ReadDateTimeOffset(reader, columnIndex),
            FieldType.Boolean => reader.GetInt32(columnIndex) != 0,
            FieldType.Guid => ReadGuid(reader, columnIndex),
            _ => throw new InvalidOperationException($"Unsupported field type: {fieldType}")
        };
    }

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

    private static async Task<object?> ReadSortValueAsync(OracleDataReader reader, Field sortField, int columnIndex, Ct ct)
    {
        if (await reader.IsDBNullAsync(columnIndex, ct))
        {
            return null;
        }

        return sortField switch
        {
            StringField => reader.GetString(columnIndex),
            NumberField => reader.GetDecimal(columnIndex),
            DateTimeField => ReadDateTimeOffset(reader, columnIndex),
            BooleanField => reader.GetInt32(columnIndex) != 0,
            GuidField or ExactMatchField => ReadGuid(reader, columnIndex),
            _ => throw new InvalidOperationException($"Unsupported field type for sorting: {sortField.GetType().Name}")
        };
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

        return (0, DataRangeSize.Default.Value);
    }

    // ───────────────────────── Record types ─────────────────────────

    private sealed record QueryClauses(string WhereClause, string JoinClause, string OrderByClause, int Offset);

    private sealed record CursorQueryClauses(
        string WhereClause,
        string JoinClause,
        string OrderByClause,
        string SeekClause,
        string SortColumnName);

}
