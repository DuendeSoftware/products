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
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.Oracle.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
internal sealed class OracleStorageEngine(
    CreateOracleConnection createConnection,
    OracleStorageEngineOptions options,
    DataStorageTypeRegistry dataStorageTypeRegistry,
    TimeProvider timeProvider,
    OutboxSubscriptions outboxSubscriptions,
    ILogger<OracleStorageEngine> logger) : StoreBase, IPartitionedStorage, ICrossPartitionStorage, IStorageInstanceSchema
{
    private const int RequiredSchemaVersion = 3;
    private static readonly ISqlDialect Dialect = new OracleDialect();
    private string? _resolvedSchema;

    // ───────────────────────── helpers ─────────────────────────

    private async Task<OracleConnection> OpenConnectionAsync(Ct ct, bool configureSession = true)
    {
        var conn = createConnection();
        await conn.OpenAsync(ct);

        if (configureSession)
        {
            await using var sessionCmd = conn.CreateCommand();
            sessionCmd.CommandText = "ALTER SESSION SET NLS_COMP=LINGUISTIC NLS_SORT=BINARY_CI";
            _ = await sessionCmd.ExecuteNonQueryAsync(ct);
        }

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

    private static string? IndexColumnName(string? column, string? expression)
    {
        if (expression is null)
        {
            return column;
        }

        // Oracle stores timestamp and descending index keys as hidden expression columns.
        const string utcPrefix = "SYS_EXTRACT_UTC(";
        if (expression.StartsWith(utcPrefix, StringComparison.OrdinalIgnoreCase) && expression.EndsWith(')'))
        {
            expression = expression[utcPrefix.Length..^1];
        }

        if (expression.Length >= 2 && expression[0] == '"' && expression[^1] == '"' &&
            !expression.AsSpan(1, expression.Length - 2).Contains('"'))
        {
            return expression[1..^1];
        }

        return null;
    }

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

    // ───────────────────────── IStorageInstanceSchema ─────────────────────────

    async Task<CheckSchemaVersionResult> IStorageInstanceSchema.CheckVersionAsync(Ct ct)
    {
        Log.CheckingSchemaVersion(logger);

        await using var connection = await OpenConnectionAsync(ct, configureSession: false);
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

    async Task IStorageInstanceSchema.MigrateAsync(Ct ct)
    {
        var schemaConfigured = options.SchemaName?.ToUpperInvariant() ?? "";
        Log.MigratingSchema(logger, schemaConfigured);

        var versionResult = await ((IStorageInstanceSchema)this).CheckVersionAsync(ct);
        var currentVersion = new DatabaseSchemaVersion((int)versionResult.CurrentVersion);

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        var scripts = MigrationScriptLoader.GetScripts(typeof(OracleStorageEngine).Assembly, currentVersion, prefix).ToList();

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

        var verifyResult = await ((IStorageInstanceSchema)this).VerifySchemaAsync(ct);
        if (!verifyResult.IsValid)
        {
            var errors = string.Join("; ", verifyResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Schema verification failed after migration: {errors}");
        }
    }

    async Task<SchemaVerificationResult> IStorageInstanceSchema.VerifySchemaAsync(Ct ct)
    {
        await using var connection = await OpenConnectionAsync(ct, configureSession: false);
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
                ["RESTRICT_DELETE"] = "NUMBER",
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
            ["__SCHEMA_INFO"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["VERSION"] = "NUMBER",
            },
        };

        var actualColumns = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // Query actual columns from ALL_TAB_COLUMNS
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = """
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, NULLABLE
                FROM ALL_TAB_COLUMNS
                WHERE OWNER = :owner
                ORDER BY TABLE_NAME, COLUMN_NAME
                """;
            Dialect.AddParameter(cmd, ":owner", schema);
            Log.ExecutingSql(logger, cmd.CommandText);

            var actualColumnTypes = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var actualColumnNullability = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);

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
                        cols = new HashSet<string>(StringComparer.Ordinal);
                        actualColumns[tableName] = cols;
                        actualColumnTypes[tableName] = new Dictionary<string, string>(StringComparer.Ordinal);
                        actualColumnNullability[tableName] = new Dictionary<string, bool>(StringComparer.Ordinal);
                    }

                    _ = cols.Add(columnName);
                    actualColumnTypes[tableName][columnName] = dataType;
                    actualColumnNullability[tableName][columnName] = reader.GetString(3) == "Y";
                }
            }

            foreach (var (tableName, columns) in expectedColumns)
            {
                if (!actualColumns.TryGetValue(tableName, out var tableColumns))
                {
                    errors.Add(new SchemaVerificationError(
                        tableName, null,
                        $"Table '{schema}.{tableName}' is missing.",
                        SchemaVerificationErrorKind.MissingTable));
                    continue;
                }

                foreach (var (columnName, expectedTypePrefix) in columns)
                {
                    if (!tableColumns.Contains(columnName))
                    {
                        errors.Add(new SchemaVerificationError(
                            tableName, columnName,
                            $"Column '{columnName}' is missing from table '{schema}.{tableName}'.",
                            SchemaVerificationErrorKind.MissingColumn));
                    }
                    else
                    {
                        if (!actualColumnTypes[tableName][columnName]
                                .StartsWith(expectedTypePrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(new SchemaVerificationError(
                                tableName, columnName,
                                $"Column '{columnName}' in table '{schema}.{tableName}' has type '{actualColumnTypes[tableName][columnName]}' but expected prefix '{expectedTypePrefix}'.",
                                SchemaVerificationErrorKind.WrongType));
                        }

                        SchemaVerification.VerifyNullability(
                            errors, tableName, columnName, actualColumnNullability[tableName][columnName]);
                    }
                }
            }
        }

        var actualKeys = new List<SchemaVerification.KeyColumn>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT c.TABLE_NAME, c.CONSTRAINT_NAME, c.CONSTRAINT_TYPE, cols.COLUMN_NAME, cols.POSITION
                FROM ALL_CONSTRAINTS c
                JOIN ALL_CONS_COLUMNS cols
                  ON cols.OWNER = c.OWNER AND cols.CONSTRAINT_NAME = c.CONSTRAINT_NAME AND cols.TABLE_NAME = c.TABLE_NAME
                WHERE c.OWNER = :owner AND c.CONSTRAINT_TYPE IN ('P', 'U') AND c.STATUS = 'ENABLED'
                ORDER BY c.TABLE_NAME, c.CONSTRAINT_NAME, cols.POSITION
                """;
            Dialect.AddParameter(cmd, ":owner", schema);
            Log.ExecutingSql(logger, cmd.CommandText);
            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            await using (reader)
            {
                while (await reader.ReadAsync(ct))
                {
                    actualKeys.Add(new SchemaVerification.KeyColumn(
                        reader.GetString(0), reader.GetString(1), reader.GetString(2) == "P",
                        reader.GetString(3), reader.GetInt32(4)));
                }
            }
        }

        SchemaVerification.VerifyKeys(errors, actualColumns, actualKeys, schemaInfoPrimaryKey: "version",
            normalizeExpectedIdentifier: static name => name.ToUpperInvariant());

        // Verify required indexes
        var expectedIndexes = new (string Table, string Index, string Definition)[]
        {
            ("ENTITIES", "IX_ENTITIES_EXPIRES_AT", "entities_expires_at"),
            ("ENTITIES", "IX_ENTITIES_ENTITY_TYPE_NAME", "entities_entity_type_name"),
            ("ENTITIES", "IX_ENTITIES_CREATED_AT", "entities_created_at"),
            ("ENTITIES", "IX_ENTITIES_LAST_UPDATED_AT", "entities_last_updated_at"),
            ("ENTITY_KEYS", "IX_ENTITY_KEYS_ENTITY", "entity_keys_entity_type_id_entity_id"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_STRING", "search_values_string_value"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_NUMBER", "search_values_number_value"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_DATETIME", "search_values_datetime_value"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_BOOLEAN", "search_values_boolean_value"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_GUID", "search_values_guid_value"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_STRING", "search_values_array_string_value"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_NUMBER", "search_values_array_number_value"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_DATETIME", "search_values_array_datetime_value"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_BOOLEAN", "search_values_array_boolean_value"),
            ("SEARCH_VALUES", "IX_SEARCH_VALUES_ARR_GUID", "search_values_array_guid_value"),
            ("ENTITY_LINKS", "IX_ENTITY_LINKS_RIGHT", "entity_links_right_entity"),
            ("ENTITY_LINKS", "IX_ENTITY_LINKS_LEFT_FK", "entity_links_left_fk"),
            ("ENTITY_LINKS", "IX_ENTITY_LINKS_RIGHT_FK", "entity_links_right_fk_trailing"),
            ("OUTBOX_SUBSCRIBER_QUEUE", "IX_OUTBOX_SUBSCRIBER", "outbox_subscriber_queue_subscriber"),
            ("OUTBOX_SUBSCRIBER_QUEUE", "IX_OUTBOX_POOL_ID", "outbox_subscriber_queue_pool_id"),
        };

        var actualIndexes = new List<SchemaVerification.IndexColumn>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            // Joined catalog views require the LONG expression to be fetched with the row.
            cmd.InitialLONGFetchSize = -1;
            cmd.CommandText = """
                SELECT i.TABLE_NAME, i.INDEX_NAME, cols.COLUMN_NAME, cols.COLUMN_POSITION, expr.COLUMN_EXPRESSION
                FROM ALL_INDEXES i
                LEFT JOIN ALL_IND_COLUMNS cols
                  ON cols.INDEX_OWNER = i.OWNER AND cols.INDEX_NAME = i.INDEX_NAME
                  AND cols.TABLE_OWNER = i.TABLE_OWNER AND cols.TABLE_NAME = i.TABLE_NAME
                LEFT JOIN ALL_IND_EXPRESSIONS expr
                  ON expr.INDEX_OWNER = cols.INDEX_OWNER AND expr.INDEX_NAME = cols.INDEX_NAME
                  AND expr.TABLE_OWNER = cols.TABLE_OWNER AND expr.TABLE_NAME = cols.TABLE_NAME
                  AND expr.COLUMN_POSITION = cols.COLUMN_POSITION
                WHERE i.OWNER = :owner AND i.TABLE_OWNER = :owner
                  AND (
                    (i.PARTITIONED = 'NO' AND i.STATUS = 'VALID')
                    OR (
                        i.PARTITIONED = 'YES'
                        AND NOT EXISTS (
                            SELECT 1
                            FROM ALL_IND_PARTITIONS p
                            WHERE p.INDEX_OWNER = i.OWNER AND p.INDEX_NAME = i.INDEX_NAME
                              AND p.STATUS = 'UNUSABLE'
                        )
                        AND NOT EXISTS (
                            SELECT 1
                            FROM ALL_IND_SUBPARTITIONS sp
                            WHERE sp.INDEX_OWNER = i.OWNER AND sp.INDEX_NAME = i.INDEX_NAME
                              AND sp.STATUS = 'UNUSABLE'
                        )
                    )
                  )
                ORDER BY i.TABLE_NAME, i.INDEX_NAME, cols.COLUMN_POSITION
                """;
            Dialect.AddParameter(cmd, ":owner", schema);
            Log.ExecutingSql(logger, cmd.CommandText);

            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            await using (reader)
            {
                while (await reader.ReadAsync(ct))
                {
                    var column = await reader.IsDBNullAsync(2, ct) ? null : reader.GetString(2);
                    var expression = await reader.IsDBNullAsync(4, ct) ? null : reader.GetString(4);
                    actualIndexes.Add(new SchemaVerification.IndexColumn(
                        reader.GetString(0), reader.GetString(1),
                        IndexColumnName(column, expression),
                        await reader.IsDBNullAsync(3, ct) ? 0 : reader.GetInt32(3)));
                }
            }
        }

        SchemaVerification.VerifyIndexes(errors, actualColumns, expectedIndexes, actualIndexes, (tableName, indexName) =>
            new SchemaVerificationError(
                tableName, null,
                $"Index '{indexName}' is missing from table '{schema}.{tableName}'.",
                SchemaVerificationErrorKind.MissingIndex),
            normalizeExpectedIdentifier: static name => name.ToUpperInvariant());

        // Verify required foreign keys. entity_keys/search_values are checked by the shared
        // SchemaVerification.VerifyForeignKeys. entity_links has two FKs to the same table, so
        // they are checked below by constraint name instead, plus the full ordered source-to-target
        // column mapping, the referenced table/schema and the delete rule (both NO ACTION, since a
        // foreign key's ON DELETE action belongs to the constraint, not the row — Restrict is
        // enforced per-row via restrict_delete and the engine's own delete logic).
        var expectedForeignKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["entity_keys"] = "FK_ENTITY_KEYS_ENTITIES",
            ["search_values"] = "FK_SEARCH_VALUES_ENTITIES",
        };

        var expectedNamedForeignKeys = new (string Table, string ConstraintName, string DeleteRule, (string Source, string Target)[] Columns)[]
        {
            ("ENTITY_LINKS", "FK_ENTITY_LINKS_LEFT", "NO ACTION",
                [("POOL_ID", "POOL_ID"), ("LEFT_ENTITY_TYPE_ID", "ENTITY_TYPE_ID"), ("LEFT_ENTITY_ID", "ENTITY_ID")]),
            ("ENTITY_LINKS", "FK_ENTITY_LINKS_RIGHT", "NO ACTION",
                [("POOL_ID", "POOL_ID"), ("RIGHT_ENTITY_TYPE_ID", "ENTITY_TYPE_ID"), ("RIGHT_ENTITY_ID", "ENTITY_ID")]),
        };

        var actualForeignKeys = new List<SchemaVerification.ForeignKeyColumn>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = """
                SELECT fk.TABLE_NAME, fk.CONSTRAINT_NAME, target.TABLE_NAME, target.OWNER,
                       source_cols.COLUMN_NAME, target_cols.COLUMN_NAME, fk.DELETE_RULE, source_cols.POSITION
                FROM ALL_CONSTRAINTS fk
                JOIN ALL_CONSTRAINTS target ON target.OWNER = fk.R_OWNER AND target.CONSTRAINT_NAME = fk.R_CONSTRAINT_NAME
                JOIN ALL_CONS_COLUMNS source_cols ON source_cols.OWNER = fk.OWNER AND source_cols.CONSTRAINT_NAME = fk.CONSTRAINT_NAME
                JOIN ALL_CONS_COLUMNS target_cols
                  ON target_cols.OWNER = target.OWNER AND target_cols.CONSTRAINT_NAME = target.CONSTRAINT_NAME
                  AND target_cols.POSITION = source_cols.POSITION
                WHERE fk.OWNER = :owner AND fk.CONSTRAINT_TYPE = 'R' AND fk.STATUS = 'ENABLED'
                ORDER BY fk.TABLE_NAME, fk.CONSTRAINT_NAME, source_cols.POSITION
                """;
            Dialect.AddParameter(cmd, ":owner", schema);
            Log.ExecutingSql(logger, cmd.CommandText);

            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            await using (reader)
            {
                while (await reader.ReadAsync(ct))
                {
                    actualForeignKeys.Add(new SchemaVerification.ForeignKeyColumn(
                        reader.GetString(0), reader.GetString(1), reader.GetString(2),
                        string.Equals(reader.GetString(3), schema, StringComparison.Ordinal),
                        reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt32(7)));
                }
            }
        }

        SchemaVerification.VerifyForeignKeys(errors, actualColumns, actualForeignKeys, expectedForeignKeys,
            normalizeExpectedIdentifier: static name => name.ToUpperInvariant());

        var entitiesTableName = SchemaVerification.FindName(actualColumns.Keys, "entities", static name => name.ToUpperInvariant());
        foreach (var (table, constraintName, deleteRule, expectedFkColumns) in expectedNamedForeignKeys)
        {
            var group = actualForeignKeys
                .Where(fk => string.Equals(fk.Table, table, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(fk.Constraint, constraintName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(fk => fk.Position)
                .ToArray();

            if (group.Length == 0)
            {
                errors.Add(new SchemaVerificationError(
                    table, null,
                    $"Foreign key '{constraintName}' is missing from table '{schema}.{table}'.",
                    SchemaVerificationErrorKind.MissingForeignKey));
                continue;
            }

            var columnsMatch = group.Length == expectedFkColumns.Length &&
                group[0].IsSameSchema &&
                entitiesTableName is not null &&
                string.Equals(group[0].ReferencedTable, entitiesTableName, StringComparison.OrdinalIgnoreCase) &&
                expectedFkColumns.All(expected => group.Any(fk =>
                    string.Equals(fk.Column, expected.Source, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(fk.ReferencedColumn, expected.Target, StringComparison.OrdinalIgnoreCase)));

            if (!columnsMatch)
            {
                errors.Add(new SchemaVerificationError(
                    table, null,
                    $"Foreign key '{constraintName}' on table '{schema}.{table}' references the wrong table/schema or columns.",
                    SchemaVerificationErrorKind.MissingForeignKey));
            }
            else if (!string.Equals(group[0].DeleteRule, deleteRule, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new SchemaVerificationError(
                    table, null,
                    $"Foreign key '{constraintName}' on table '{schema}.{table}' has delete rule '{group[0].DeleteRule}', expected '{deleteRule}'.",
                    SchemaVerificationErrorKind.Other));
            }
        }

        // Oracle has no user-defined table types (TVPs) — skip that verification.

        return new SchemaVerificationResult(errors);
    }

    string IStorageInstanceSchema.BuildMigrationScript(DatabaseSchemaVersion fromVersion)
    {
        var schemaUpper = options.SchemaName?.ToUpperInvariant();
        var prefix = string.IsNullOrEmpty(schemaUpper) ? "" : SchemaPrefix(schemaUpper);

        var scripts = MigrationScriptLoader.GetScripts(typeof(OracleStorageEngine).Assembly, fromVersion, prefix);
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

    /// <summary>
    /// Returns whether the database error says that a table does not exist: Oracle error ORA-00942 (table or view does not exist).
    /// </summary>
    private static bool IsMissingStorageTable(OracleException ex) => ex.Number == 942;

    /// <summary>
    /// Qualified table names <see cref="SqlWhereClauseBuilder"/> needs to compile a link filter, matching the
    /// qualified names used elsewhere in this engine.
    /// </summary>
    private static StorageTableNames LinkFilterTableNames(string prefix) => new(
        $"{prefix}ENTITIES",
        $"{prefix}ENTITY_LINKS");

    /// <summary>
    /// Returns whether an exception is an Oracle deadlock (ORA-00060), worth retrying the whole
    /// operation from a fresh connection and transaction. ORA-00060 rolls back only the statement
    /// Oracle chose as the victim, but rerunning the whole operation after a full rollback is
    /// still correct, and keeps the retry shape the same across all three engines.
    /// </summary>
    private static bool IsDeadlock(Exception ex) => ex is OracleException { Number: 60 };

    // ───────────────────────── IPartitionedStorage — Create ─────────────────────────

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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — TryRead (by id) ─────────────────────────

    async Task<StoreGetResult> IPartitionedStorage.TryReadAsync(
        EntityType entityType,
        Storage.UuidV7 id,
        Ct ct)
    {
        try
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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — TryRead (by key) ─────────────────────────

    async Task<StoreGetResult> IPartitionedStorage.TryReadAsync(
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
    {
        try
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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — TryReadMany ─────────────────────────

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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — Update ─────────────────────────

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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — Delete (by id) ─────────────────────────

    async Task<DeleteResult> IPartitionedStorage.DeleteAsync(EntityType entityType, Storage.UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            return await DeadlockRetry.RunAsync(
                async _ =>
                {
                    var deleteOp = DeleteOperation.ById(entityType, id);

                    await using var connection = await OpenConnectionAsync(ct);
                    var schema = await ResolveSchemaAsync(connection, ct);
                    var prefix = SchemaPrefix(schema);
                    await using var transaction = await connection.BeginTransactionAsync(ct);

                    var (outcome, entityDeleted) = await ExecuteDeleteCoreAsync(connection, (OracleTransaction)transaction, prefix, deleteOp, ct);

                    if (outcome == OperationOutcome.StillLinked)
                    {
                        return DeleteResult.StillLinked;
                    }

                    if (entityDeleted && outboxEvents is { Count: > 0 })
                    {
                        await ExecuteOutboxInsertBatchCoreAsync(connection, (OracleTransaction)transaction, prefix, outboxEvents, ct);
                    }
                    await transaction.CommitAsync(ct);

                    return DeleteResult.Success;
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — Delete (by key) ─────────────────────────

    async Task<DeleteResult> IPartitionedStorage.DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            return await DeadlockRetry.RunAsync(
                async _ =>
                {
                    var deleteOp = DeleteOperation.ByKey(entityType, key);

                    await using var connection = await OpenConnectionAsync(ct);
                    var schema = await ResolveSchemaAsync(connection, ct);
                    var prefix = SchemaPrefix(schema);
                    await using var transaction = await connection.BeginTransactionAsync(ct);

                    var (outcome, entityDeleted) = await ExecuteDeleteCoreAsync(connection, (OracleTransaction)transaction, prefix, deleteOp, ct);

                    if (outcome == OperationOutcome.StillLinked)
                    {
                        return DeleteResult.StillLinked;
                    }

                    if (entityDeleted && outboxEvents is { Count: > 0 })
                    {
                        await ExecuteOutboxInsertBatchCoreAsync(connection, (OracleTransaction)transaction, prefix, outboxEvents, ct);
                    }
                    await transaction.CommitAsync(ct);

                    return DeleteResult.Success;
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — Link ─────────────────────────

    /// <inheritdoc/>
    async Task<LinkResult> IPartitionedStorage.LinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            return await DeadlockRetry.RunAsync(
                async _ =>
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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — Unlink ─────────────────────────

    /// <inheritdoc/>
    async Task<UnlinkResult> IPartitionedStorage.UnlinkAsync(LinkDefinition definition, Storage.UuidV7 leftEntityId, Storage.UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        try
        {
            return await DeadlockRetry.RunAsync(
                async attempt =>
                {
                    _ = attempt;
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
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
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
            INSERT INTO {prefix}ENTITY_LINKS (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id, restrict_delete)
            VALUES (:poolId, :linkTypeId, :leftEntityTypeId, :leftEntityId, :rightEntityTypeId, :rightEntityId, :restrictDelete)
            """;
        Dialect.AddParameter(cmd, ":poolId", PoolId.Value);
        Dialect.AddParameter(cmd, ":linkTypeId", (int)op.Definition.Link.Id);
        Dialect.AddParameter(cmd, ":leftEntityTypeId", (int)op.Definition.Left.Id);
        Dialect.AddParameter(cmd, ":leftEntityId", op.LeftEntityId.Value);
        Dialect.AddParameter(cmd, ":rightEntityTypeId", (int)op.Definition.Right.Id);
        Dialect.AddParameter(cmd, ":rightEntityId", op.RightEntityId.Value);
        Dialect.AddParameter(cmd, ":restrictDelete", op.Definition.OnRightDelete == LinkDeleteBehavior.Restrict ? 1 : 0);

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
        catch (OracleException ex) when (ex.Number == 2291)
        {
            // ORA-02291: integrity constraint violated - parent key not found. The left or
            // right entity doesn't exist in this pool with the definition's entity type.
            return OperationOutcome.DoesNotExist;
        }
    }

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

        // Per-row INSERT (sequence_number is GENERATED ALWAYS AS IDENTITY)
        var sql = $"""
            INSERT INTO {prefix}OUTBOX_SUBSCRIBER_QUEUE
            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version)
            VALUES (:messageId, :eventId, :ts, :eventName, :subjectId, :entityTypeId, :entityTypeName, :poolId, :payload, :subscriptionName, :dsoTypeSchemaVersion)
            """;

        foreach (var (evt, subscription) in rows)
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
            Dialect.AddParameter(cmd, ":subscriptionName", subscription.SubscriberName.ToString());
            Dialect.AddParameter(cmd, ":dsoTypeSchemaVersion", evt.DsoTypeSchemaVersion.HasValue ? evt.DsoTypeSchemaVersion.Value : DBNull.Value);

            Log.ExecutingSql(logger, cmd.CommandText);
            _ = await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    // ───────────────────────── IPartitionedStorage — ExecuteBatch ─────────────────────────

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
                },
                IsDeadlock,
                (_, _) => Log.RetryingAfterDeadlock(logger),
                ct);
        }
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — GetOutboxEventsForSubscription ─────────────────────────

    async Task<OutboxEventsPage> ICrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, Ct ct)
    {
        try
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
            Dialect.AddParameter(cmd, ":subscriber_name", subscriptionName.ToString());

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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — DeleteOutboxEvents ─────────────────────────

    async Task ICrossPartitionStorage.DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct)
    {
        try
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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — QueryAsync ─────────────────────────

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

            await using var connection = await OpenConnectionAsync(ct);
            var schema = await ResolveSchemaAsync(connection, ct);
            var prefix = SchemaPrefix(schema);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var queryClauses = BuildQueryClauses(cmd, schema, prefix, filter, sort, skip, entityType);

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

            var items = new List<MetadataEnvelope>();
            var totalCount = 0;
            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            await using (reader)
            {
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
                    var dsoTypeVersion = reader.GetInt32(2);
                    var valueVersion = reader.GetInt32(3);
                    var created = ReadDateTimeOffset(reader, 4);
                    var lastUpdated = ReadDateTimeOffset(reader, 5);

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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── QueryCursorAsync ─────────────────────────

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

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        var queryClauses = BuildCursorQueryClauses(cmd, schema, prefix, filter, sort, position, traversal, entityType);

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

        var items = new List<(Guid Id, MetadataEnvelope Item, object? SortValue)>();
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

    // ───────────────────────── IPartitionedStorage — QueryFieldsAsync ─────────────────────────

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

            await using var connection = await OpenConnectionAsync(ct);
            var schema = await ResolveSchemaAsync(connection, ct);
            var prefix = SchemaPrefix(schema);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var queryClauses = BuildQueryClauses(cmd, schema, prefix, filter, sort, skip, entityType);

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
                HasMoreData = skip + take < totalCount,
                HasPreviousData = skip > 0 && totalCount > 0
            };
        }
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
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
        var pageSize = tokenRange.Size.Value;
        var decoded = CursorToken.Decode(tokenRange.Start, sort, entityType);
        var traversal = decoded.Direction;
        var position = decoded.Position;

        var entityTypeId = (int)entityType.Id;

        Log.QueryingFieldsDsos(logger, entityType, fields.Count, 0, pageSize);

        await using var connection = await OpenConnectionAsync(ct);
        var schema = await ResolveSchemaAsync(connection, ct);
        var prefix = SchemaPrefix(schema);

        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;

        var queryClauses = BuildCursorQueryClauses(cmd, schema, prefix, filter, sort, position, traversal, entityType);

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

                    var sortValue = sort.IsEmpty ? null : await ReadSortValueAsync(reader, sort.Field!, 7, ct);
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

        var entityId = op.Id?.Value;
        if (entityId is null && op.Key is not null)
        {
            entityId = await ResolveEntityIdByKeyAsync(connection, transaction, prefix, entityType, op.Key, ct);
        }

        if (entityId is null)
        {
            return (OperationOutcome.Success, false);
        }

        if (op.Id is not null)
        {
            Log.DeletingDso(logger, entityType, op.Id.Value);
        }

        return await DeleteEntityAndLinksAsync(connection, transaction, prefix, entityType, entityId.Value, allowRetry: true, ct);
    }

    /// <summary>
    /// Resolves the entity id for a by-key delete, or <see langword="null"/> if no such entity exists.
    /// </summary>
    private async Task<Guid?> ResolveEntityIdByKeyAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
        EntityType entityType,
        DataStorageKey key,
        Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT entity_id
            FROM {prefix}ENTITY_KEYS
            WHERE entity_type_id = :entityTypeId
              AND key_type_id = :keyTypeId
              AND key_type_version = :keyTypeVersion
              AND key_value = :keyValue
              AND pool_id = :poolId
            """;
        Dialect.AddParameter(cmd, ":entityTypeId", (int)entityType.Id);
        Dialect.AddParameter(cmd, ":keyTypeId", (int)key.DskVersion.KeyType.Id);
        Dialect.AddParameter(cmd, ":keyTypeVersion", (int)key.DskVersion.SchemaVersion);
        Dialect.AddParameter(cmd, ":keyValue", key.Value);
        Dialect.AddParameter(cmd, ":poolId", PoolId.Value);
        Log.ExecutingSql(logger, cmd.CommandText);
        var result = await cmd.ExecuteScalarAsync(ct);

        return result is null or DBNull ? null : OracleGuidConverter.FromRaw((byte[])result);
    }

    /// <summary>
    /// Deletes links where the given entity is the left side, and right-side links that are not
    /// restricted (<c>restrict_delete = 0</c>; both FKs on ENTITY_LINKS are NO ACTION on Oracle,
    /// so the engine performs this cleanup itself, in the same transaction, before deleting the
    /// entity), then deletes the entity itself. If a restricted right-side link remains, the
    /// entity delete's ORA-02292 is reported as <see cref="OperationOutcome.StillLinked"/> after
    /// re-checking for a restricted link. If no restricted link is found (a cascading link was
    /// inserted concurrently between the two statements), both statements are retried once.
    /// </summary>
    private async Task<(OperationOutcome Outcome, bool EntityDeleted)> DeleteEntityAndLinksAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
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
                DELETE FROM {prefix}ENTITY_LINKS
                WHERE pool_id = :poolId
                  AND (
                    (left_entity_type_id = :entityTypeId AND left_entity_id = :entityId)
                    OR (right_entity_type_id = :entityTypeId AND right_entity_id = :entityId AND restrict_delete = 0)
                  )
                """;
            Dialect.AddParameter(linkDeleteCmd, ":poolId", PoolId.Value);
            Dialect.AddParameter(linkDeleteCmd, ":entityTypeId", (int)entityType.Id);
            Dialect.AddParameter(linkDeleteCmd, ":entityId", entityId);
            Log.ExecutingSql(logger, linkDeleteCmd.CommandText);
            _ = await linkDeleteCmd.ExecuteNonQueryAsync(ct);
        }

        await using var deleteCmd = connection.CreateCommand();
        deleteCmd.Transaction = transaction;
        deleteCmd.CommandType = CommandType.Text;
        deleteCmd.CommandText = $"""
            DELETE FROM {prefix}ENTITIES
            WHERE entity_type_id = :entityTypeId AND entity_id = :entityId AND pool_id = :poolId
            """;
        Dialect.AddParameter(deleteCmd, ":entityTypeId", (int)entityType.Id);
        Dialect.AddParameter(deleteCmd, ":entityId", entityId);
        Dialect.AddParameter(deleteCmd, ":poolId", PoolId.Value);
        Log.ExecutingSql(logger, deleteCmd.CommandText);

        try
        {
            var rowsAffected = await deleteCmd.ExecuteNonQueryAsync(ct);
            return (OperationOutcome.Success, rowsAffected > 0);
        }
        catch (OracleException ex) when (ex.Number == 2292)
        {
            // ORA-02292: integrity constraint violated - child record found.
            if (await HasRestrictedRightLinkAsync(connection, transaction, prefix, entityType, entityId, ct))
            {
                return (OperationOutcome.StillLinked, false);
            }

            if (!allowRetry)
            {
                throw;
            }

            // No restricted link remains: a cascading link must have been inserted concurrently
            // between the link-cleanup statement and the entity-delete statement. Retry once.
            Log.RetryingDeleteAfterLinkRace(logger, entityType, entityId);
            return await DeleteEntityAndLinksAsync(connection, transaction, prefix, entityType, entityId, allowRetry: false, ct);
        }
    }

    /// <summary>
    /// Returns whether a restricted (<c>restrict_delete = 1</c>) link still exists with the given
    /// entity as the right side.
    /// </summary>
    private async Task<bool> HasRestrictedRightLinkAsync(
        OracleConnection connection,
        OracleTransaction transaction,
        string prefix,
        EntityType entityType,
        Guid entityId,
        Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $"""
            SELECT COUNT(*) FROM {prefix}ENTITY_LINKS
            WHERE pool_id = :poolId
              AND right_entity_type_id = :entityTypeId
              AND right_entity_id = :entityId
              AND restrict_delete = 1
            """;
        Dialect.AddParameter(cmd, ":poolId", PoolId.Value);
        Dialect.AddParameter(cmd, ":entityTypeId", (int)entityType.Id);
        Dialect.AddParameter(cmd, ":entityId", entityId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture) != 0;
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

    // ───────────────────────── IPartitionedStorage — QueryLinksAsync ─────────────────────────

    private static (StringBuilder JoinSql, string LastFilterColumn) BuildLinkJoins(
        LinkQueryDescriptor query,
        OracleCommand cmd,
        string prefix)
    {
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

            await using var connection = await OpenConnectionAsync(ct);
            var schema = await ResolveSchemaAsync(connection, ct);
            var prefix = SchemaPrefix(schema);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var (joinSql, anchorColumn) = BuildLinkJoins(query, cmd, prefix);
            var anchorParams = new List<string>(query.WhereEntityIds.Count);
            var anchorIndex = 0;
            foreach (var id in query.WhereEntityIds)
            {
                var name = $":a{anchorIndex++}";
                anchorParams.Add(name);
                Dialect.AddParameter(cmd, name, id.Value);
            }

            var inClause = string.Join(", ", anchorParams);

            Dialect.AddParameter(cmd, ":pool_id", PoolId.Value);
            Dialect.AddParameter(cmd, ":source_entity_type_id", (int)query.SourceEntityType.Id);
            Dialect.AddParameter(cmd, ":offset", skip);
            Dialect.AddParameter(cmd, ":limit", take + 1);

            // Dedupe and page on ids only, then fetch the values for the page, so the sort doesn't carry `value` for every skipped row.
            cmd.CommandText = $"""
                WITH matched AS (
                    SELECT DISTINCT {anchorColumn} AS anchor_id, e.entity_id
                    FROM {prefix}ENTITIES e
                    {joinSql}
                    WHERE e.entity_type_id = :source_entity_type_id
                      AND e.pool_id = :pool_id
                      AND {anchorColumn} IN ({inClause})
                    ORDER BY anchor_id, e.entity_id
                    OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY
                )
                SELECT m.anchor_id, ent.entity_id, ent.value, ent.dso_type_schema_version, ent.value_version, ent.created_at, ent.last_updated_at
                FROM matched m
                JOIN {prefix}ENTITIES ent ON ent.entity_id = m.entity_id
                    AND ent.entity_type_id = :source_entity_type_id
                    AND ent.pool_id = :pool_id
                ORDER BY m.anchor_id, ent.entity_id
                """;
            Log.ExecutingQuery(logger, cmd.CommandText);

            var items = new List<LinkedEnvelope>();
            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            await using (reader)
            {
                while (await reader.ReadAsync(ct))
                {
                    var anchorId = Storage.UuidV7.From(ReadGuid(reader, 0));
                    var entityId = ReadGuid(reader, 1);
                    var jsonValue = reader.GetString(2);
                    var dsoTypeVersion = reader.GetInt32(3);
                    var valueVersion = reader.GetInt32(4);
                    var created = ReadDateTimeOffset(reader, 5);
                    var lastUpdated = ReadDateTimeOffset(reader, 6);

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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
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

            await using var connection = await OpenConnectionAsync(ct);
            var schema = await ResolveSchemaAsync(connection, ct);
            var prefix = SchemaPrefix(schema);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var (joinSql, anchorColumn) = BuildLinkJoins(query, cmd, prefix);
            var anchorParams = new List<string>(query.WhereEntityIds.Count);
            var anchorIndex = 0;
            foreach (var id in query.WhereEntityIds)
            {
                var name = $":a{anchorIndex++}";
                anchorParams.Add(name);
                Dialect.AddParameter(cmd, name, id.Value);
            }

            var inClause = string.Join(", ", anchorParams);

            Dialect.AddParameter(cmd, ":pool_id", PoolId.Value);
            Dialect.AddParameter(cmd, ":source_entity_type_id", (int)query.SourceEntityType.Id);

            cmd.CommandText = $"""
                SELECT {anchorColumn} AS anchor_id, COUNT(DISTINCT e.entity_id)
                FROM {prefix}ENTITIES e
                {joinSql}
                WHERE e.entity_type_id = :source_entity_type_id
                  AND e.pool_id = :pool_id
                  AND {anchorColumn} IN ({inClause})
                GROUP BY {anchorColumn}
                """;
            Log.ExecutingSql(logger, cmd.CommandText);

            var counts = query.WhereEntityIds.ToDictionary(id => id, _ => 0L);
            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                counts[Storage.UuidV7.From(OracleGuidConverter.FromRaw((byte[])reader.GetValue(0)))] = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            }

            return counts;
        }
        catch (OracleException ex) when (IsMissingStorageTable(ex))
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

            await using var connection = await OpenConnectionAsync(ct);
            var schema = await ResolveSchemaAsync(connection, ct);
            var prefix = SchemaPrefix(schema);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            var (joinSql, whereLastJoin) = BuildLinkJoins(query, cmd, prefix);

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

            var items = new List<MetadataEnvelope>();
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

                    var version = new DataStorageObjectVersion(query.SourceEntityType, (uint)dsoTypeVersion);
                    var dsoType = dataStorageTypeRegistry.Get(version);
                    var item = (IDataStorageObject)JsonSerializer.Deserialize(jsonValue, dsoType)!;

                    items.Add(new MetadataEnvelope(item, entityId, valueVersion, created, lastUpdated));
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

            return new QueryResult<MetadataEnvelope>
            {
                Items = items,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / take),
                HasMoreData = skip + take < totalCount,
                HasPreviousData = skip > 0 && totalCount > 0
            };
        }
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — CountAsync ─────────────────────────

    async Task<long> IPartitionedStorage.CountAsync(
        EntityType entityType,
        IQueryExpression? filter,
        Ct ct)
    {
        try
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
                var whereBuilder = new SqlWhereClauseBuilder(schema, cmd, Dialect, LinkFilterTableNames(prefix), entityType);
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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── IPartitionedStorage — PurgeExpiredAsync ─────────────────────────

    async Task<int> ICrossPartitionStorage.PurgeExpiredAsync(int batchSize, Ct ct)
    {
        try
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

            // Step 2: Insert outbox events per matching subscription
            if (!outboxSubscriptions.IsEmpty)
            {
                var eventName = OutboxEventName.EntityExpired;

                foreach (var (poolId, entityId, entityTypeId, entityTypeName, value, dsoTypeSchemaVersion) in expired)
                {
                    var eventId = Guid.CreateVersion7();

                    foreach (var subscription in outboxSubscriptions.Subscriptions)
                    {
                        if (subscription.EventNames.Count > 0 && !subscription.EventNames.Contains(eventName))
                        {
                            continue;
                        }
                        if (subscription.EntityTypeIds.Count > 0 && !subscription.EntityTypeIds.Contains(entityTypeId))
                        {
                            continue;
                        }

                        await using var outboxCmd = connection.CreateCommand();
                        outboxCmd.Transaction = (OracleTransaction)transaction;
                        outboxCmd.CommandType = CommandType.Text;
                        outboxCmd.CommandText = $"""
                            INSERT INTO {prefix}OUTBOX_SUBSCRIBER_QUEUE
                            (message_id, event_id, timestamp, event_name, subject_id, entity_type_id, entity_type_name, pool_id, payload, subscriber_name, dso_type_schema_version)
                            VALUES (:messageId, :eventId, :ts, :eventName, :subjectId, :entityTypeId, :entityTypeName, :poolId, :payload, :subscriptionName, :dsoTypeSchemaVersion)
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
                        Dialect.AddParameter(outboxCmd, ":subscriptionName", subscription.SubscriberName.ToString());
                        Dialect.AddParameter(outboxCmd, ":dsoTypeSchemaVersion", dsoTypeSchemaVersion.HasValue ? dsoTypeSchemaVersion.Value : DBNull.Value);
                        _ = await outboxCmd.ExecuteNonQueryAsync(ct);
                    }
                }
            }

            // Step 3: Delete all links touching an expired entity, on either side. Both FKs on
            // ENTITY_LINKS are NO ACTION on Oracle, so this explicit delete stands in for the
            // cascade the database can't do; restricted links are removed too — purging an
            // expired entity overrides Restrict.
            foreach (var (poolId, entityId, entityTypeId, _, _, _) in expired)
            {
                await using var linkCmd = connection.CreateCommand();
                linkCmd.Transaction = (OracleTransaction)transaction;
                linkCmd.CommandType = CommandType.Text;
                linkCmd.CommandText = $"""
                    DELETE FROM {prefix}ENTITY_LINKS
                    WHERE pool_id = :poolId
                      AND (
                        (left_entity_type_id = :entityTypeId AND left_entity_id = :entityId)
                        OR (right_entity_type_id = :entityTypeId AND right_entity_id = :entityId)
                      )
                    """;
                Dialect.AddParameter(linkCmd, ":poolId", poolId);
                Dialect.AddParameter(linkCmd, ":entityTypeId", entityTypeId);
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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
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

            await using var connection = await OpenConnectionAsync(ct);
            var schema = await ResolveSchemaAsync(connection, ct);
            var prefix = SchemaPrefix(schema);

            // Drain entity_links completely before any entity batch: both FKs on ENTITY_LINKS
            // are NO ACTION on Oracle, so purging it explicitly first avoids a foreign key
            // violation on the later entity deletes. It must be fully drained (not just one batch
            // per entity batch), because an independent entity batch could otherwise select an
            // entity that still has links left over from a batch size smaller than its in-degree.
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                await using var transaction = await connection.BeginTransactionAsync(ct);

                int batchLinks;
                await using (var linksCmd = connection.CreateCommand())
                {
                    linksCmd.Transaction = (OracleTransaction)transaction;
                    linksCmd.CommandType = CommandType.Text;
                    linksCmd.CommandText = $"""
                        DELETE FROM {prefix}ENTITY_LINKS
                        WHERE pool_id = :poolId AND ROWNUM <= :batchSize
                        """;
                    Dialect.AddParameter(linksCmd, ":poolId", poolId.Value);
                    Dialect.AddParameter(linksCmd, ":batchSize", batchSize);

                    Log.ExecutingSql(logger, linksCmd.CommandText);
                    batchLinks = await linksCmd.ExecuteNonQueryAsync(ct);
                }

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

                int batchOutbox;
                int batchEntities;

                await using (var outboxCmd = connection.CreateCommand())
                {
                    outboxCmd.Transaction = (OracleTransaction)transaction;
                    outboxCmd.CommandType = CommandType.Text;
                    outboxCmd.CommandText = $"""
                        DELETE FROM {prefix}OUTBOX_SUBSCRIBER_QUEUE
                        WHERE pool_id = :poolId AND ROWNUM <= :batchSize
                        """;
                    Dialect.AddParameter(outboxCmd, ":poolId", poolId.Value);
                    Dialect.AddParameter(outboxCmd, ":batchSize", batchSize);

                    Log.ExecutingSql(logger, outboxCmd.CommandText);
                    batchOutbox = await outboxCmd.ExecuteNonQueryAsync(ct);
                }

                await using (var entitiesCmd = connection.CreateCommand())
                {
                    entitiesCmd.Transaction = (OracleTransaction)transaction;
                    entitiesCmd.CommandType = CommandType.Text;
                    entitiesCmd.CommandText = $"""
                        DELETE FROM {prefix}ENTITIES
                        WHERE pool_id = :poolId AND ROWNUM <= :batchSize
                        """;
                    Dialect.AddParameter(entitiesCmd, ":poolId", poolId.Value);
                    Dialect.AddParameter(entitiesCmd, ":batchSize", batchSize);

                    Log.ExecutingSql(logger, entitiesCmd.CommandText);
                    batchEntities = await entitiesCmd.ExecuteNonQueryAsync(ct);
                }

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
        catch (OracleException ex) when (IsMissingStorageTable(ex))
        {
            throw StorageSchemaMissing(ex);
        }
    }

    // ───────────────────────── Query clause builders ─────────────────────────

    private static QueryClauses BuildQueryClauses(
        OracleCommand cmd,
        string schema,
        string prefix,
        IQueryExpression filter,
        SortParameter sort,
        int offset,
        EntityType entityType)
    {
        var whereBuilder = new SqlWhereClauseBuilder(schema, cmd, Dialect, LinkFilterTableNames(prefix), entityType);
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

    /// <summary>
    /// Builds the WHERE clause, JOIN clause, ORDER BY clause, and seek clause for cursor-based pagination.
    /// </summary>
    private static CursorQueryClauses BuildCursorQueryClauses(
        OracleCommand cmd,
        string schema,
        string prefix,
        IQueryExpression filter,
        SortParameter sort,
        CursorPosition position,
        CursorDirection traversal,
        EntityType entityType)
    {
        var (whereClause, joinClause, sortColumn) = BuildCursorFilterAndJoin(cmd, schema, prefix, filter, sort, entityType);

        var builder = new KeysetSqlBuilder(Dialect, sortColumn, sort.IsEmpty ? SortDirection.Ascending : sort.Direction);
        var orderByClause = $"ORDER BY {builder.OrderBy(traversal)}";
        var predicate = builder.StrictlyAfter(traversal, position, ":last_sort_value", ":last_id");
        var seekClause = predicate.Length == 0 ? "" : $"AND ({predicate})";

        BindCursorPositionParameters(cmd, position, sortColumn);

        return new CursorQueryClauses(whereClause, joinClause, orderByClause, seekClause, sortColumn ?? "NULL");
    }

    /// <summary>
    /// Builds the WHERE clause, JOIN clause, and sort column for a cursor query, without the
    /// ORDER BY/seek predicate (shared between the main query and the opposite-side probe).
    /// </summary>
    private static (string WhereClause, string JoinClause, string? SortColumn) BuildCursorFilterAndJoin(
        OracleCommand cmd,
        string schema,
        string prefix,
        IQueryExpression filter,
        SortParameter sort,
        EntityType entityType)
    {
        var whereBuilder = new SqlWhereClauseBuilder(schema, cmd, Dialect, LinkFilterTableNames(prefix), entityType);
        var whereClause = whereBuilder.BuildWhereClause(filter).Replace("@", ":", StringComparison.Ordinal);

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
                LEFT JOIN {prefix}SEARCH_VALUES sort_sv
                  ON v.entity_type_id = sort_sv.entity_type_id
                  AND v.entity_id = sort_sv.entity_id
                  AND v.pool_id = sort_sv.pool_id
                  AND sort_sv.field_path = :sort_field_path
                  AND sort_sv.item_index = -1
                """;

            Dialect.AddParameter(cmd, ":sort_field_path", DeterministicGuidGenerator.Create(sortFieldPath.ToUpperInvariant()));
        }

        return (whereClause, joinClause, sortColumn);
    }

    /// <summary>
    /// Binds <c>:last_id</c> and, when the seek predicate references it, <c>:last_sort_value</c>
    /// for a decoded cursor key position, using <see cref="OracleDialect.AddParameter"/> for the
    /// per-kind conversions.
    /// </summary>
    private static void BindCursorPositionParameters(OracleCommand cmd, CursorPosition position, string? sortColumn)
    {
        if (position.Kind != CursorPositionKind.Key)
        {
            return;
        }

        Dialect.AddParameter(cmd, ":last_id", position.Id);

        if (sortColumn is null || position.ValueKind == CursorValueKind.Null)
        {
            return;
        }

        Dialect.AddParameter(cmd, ":last_sort_value", position.Value!);
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
