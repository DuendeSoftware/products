// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Schema;

namespace Duende.Storage.Internal;

internal static class SchemaVerification
{
    private static readonly HashSet<string> NullableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "entities.expires_at",
        "entity_keys.key_json",
        "search_values.string_value",
        "search_values.number_value",
        "search_values.datetime_value",
        "search_values.boolean_value",
        "search_values.guid_value",
        "outbox_subscriber_queue.dso_type_schema_version",
    };

    private static readonly (string Table, string[] Columns)[] PrimaryKeys =
    [
        ("entities", ["pool_id", "entity_type_id", "entity_id"]),
        ("entity_keys", ["pool_id", "entity_type_id", "key_type_id", "key_type_version", "key_value"]),
        ("search_values", ["pool_id", "entity_type_id", "entity_id", "field_path", "item_index"]),
        ("entity_links", ["pool_id", "link_type_id", "left_entity_id", "right_entity_id"]),
        ("outbox_subscriber_queue", ["sequence_number"]),
    ];

    private static readonly Dictionary<string, string[]> IndexKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["entities_expires_at"] = ["expires_at"],
        ["entities_entity_type_name"] = ["entity_type_name"],
        ["entities_created_at"] = ["pool_id", "entity_type_id", "created_at"],
        ["entities_last_updated_at"] = ["pool_id", "entity_type_id", "last_updated_at"],
        ["entity_keys_entity_type_id_entity_id"] = ["entity_type_id", "entity_id"],
        ["search_values_string_value"] = ["pool_id", "entity_type_id", "field_path", "string_value"],
        ["search_values_number_value"] = ["pool_id", "entity_type_id", "field_path", "number_value"],
        ["search_values_datetime_value"] = ["pool_id", "entity_type_id", "field_path", "datetime_value"],
        ["search_values_boolean_value"] = ["pool_id", "entity_type_id", "field_path", "boolean_value"],
        ["search_values_guid_value"] = ["pool_id", "entity_type_id", "field_path", "guid_value"],
        ["search_values_array_string_value"] = ["pool_id", "entity_type_id", "entity_id", "field_path", "item_index", "string_value"],
        ["search_values_array_number_value"] = ["pool_id", "entity_type_id", "entity_id", "field_path", "item_index", "number_value"],
        ["search_values_array_datetime_value"] = ["pool_id", "entity_type_id", "entity_id", "field_path", "item_index", "datetime_value"],
        ["search_values_array_boolean_value"] = ["pool_id", "entity_type_id", "entity_id", "field_path", "item_index", "boolean_value"],
        ["search_values_array_guid_value"] = ["pool_id", "entity_type_id", "entity_id", "field_path", "item_index", "guid_value"],
        ["entity_links_right_entity"] = ["pool_id", "link_type_id", "right_entity_id"],
        ["entity_links_left_fk"] = ["pool_id", "left_entity_id", "left_entity_type_id"],
        ["entity_links_right_fk"] = ["pool_id", "right_entity_id", "right_entity_type_id", "link_type_id"],
        ["entity_links_right_fk_trailing"] = ["pool_id", "right_entity_id", "right_entity_type_id", "link_type_id", "restrict_delete", "left_entity_id", "left_entity_type_id"],
        ["outbox_subscriber_queue_subscriber"] = ["subscriber_name", "sequence_number"],
        ["outbox_subscriber_queue_pool_id"] = ["pool_id"],
    };

    internal sealed record IndexColumn(string Table, string Index, string? Column, int Position);

    internal sealed record KeyColumn(string Table, string Constraint, bool IsPrimary, string Column, int Position);

    internal sealed record ForeignKeyColumn(
        string Table,
        string Constraint,
        string ReferencedTable,
        bool IsSameSchema,
        string Column,
        string ReferencedColumn,
        string DeleteRule,
        int Position);

    internal static void VerifyNullability(
        List<SchemaVerificationError> errors,
        string table,
        string column,
        bool isNullable,
        bool isSqlite = false)
    {
        var expectedNullable = NullableColumns.Contains($"{table}.{column}") ||
                               (isSqlite && EqualsIgnoreCase(table, "__schema_info") && EqualsIgnoreCase(column, "key"));

        if (isNullable != expectedNullable)
        {
            errors.Add(new SchemaVerificationError(
                table, column,
                $"Column '{table}.{column}' is {(isNullable ? "nullable" : "not nullable")}, expected {(expectedNullable ? "nullable" : "not nullable")}.",
                SchemaVerificationErrorKind.Other));
        }
    }

    internal static void VerifyIndexes(
        List<SchemaVerificationError> errors,
        IReadOnlyDictionary<string, HashSet<string>> tables,
        IEnumerable<(string Table, string Index, string Definition)> expectedIndexes,
        IReadOnlyList<IndexColumn> actualIndexes,
        Func<string, string, SchemaVerificationError> missingIndex,
        Func<string, string>? normalizeExpectedIdentifier = null)
    {
        foreach (var (table, index, definition) in expectedIndexes)
        {
            var actualTable = FindName(tables.Keys, table, normalizeExpectedIdentifier);
            if (actualTable is null)
            {
                errors.Add(missingIndex(table, index));
                continue;
            }

            var candidates = actualIndexes
                .Where(i => string.Equals(i.Table, actualTable, StringComparison.Ordinal) && EqualsIgnoreCase(i.Index, index))
                .GroupBy(i => i.Index, StringComparer.Ordinal)
                .ToArray();

            if (candidates.Length == 0)
            {
                errors.Add(missingIndex(table, index));
                continue;
            }

            var exact = candidates.FirstOrDefault(candidate => string.Equals(candidate.Key, index, StringComparison.Ordinal));
            var matchingIndexes = exact is null ? candidates : [exact];
            var actualColumns = matchingIndexes
                .Select(candidate => candidate.OrderBy(i => i.Position)
                    .Where(i => i.Position > 0)
                    .Select(i => i.Column ?? "<expression>").ToArray())
                .ToArray();
            var expectedColumns = IndexKeys[definition];
            if (!actualColumns.Any(columns => ColumnsMatch(tables[actualTable], columns, expectedColumns, normalizeExpectedIdentifier)))
            {
                var expectedColumnNames = expectedColumns.Select(column => normalizeExpectedIdentifier?.Invoke(column) ?? column);
                errors.Add(new SchemaVerificationError(
                    table, null,
                    $"Index '{index}' on table '{table}' has key columns ({string.Join(", ", actualColumns[0])}), expected ({string.Join(", ", expectedColumnNames)}).",
                    SchemaVerificationErrorKind.Other));
            }
        }
    }

    internal static void VerifyKeys(
        List<SchemaVerificationError> errors,
        IReadOnlyDictionary<string, HashSet<string>> tables,
        IReadOnlyList<KeyColumn> actualKeys,
        string? schemaInfoPrimaryKey = null,
        Func<string, string>? normalizeExpectedIdentifier = null)
    {
        foreach (var (table, columns) in PrimaryKeys)
        {
            VerifyKey(errors, tables, actualKeys, table, columns, isPrimary: true, normalizeExpectedIdentifier);
        }

        if (schemaInfoPrimaryKey is not null)
        {
            VerifyKey(errors, tables, actualKeys, "__schema_info", [schemaInfoPrimaryKey], isPrimary: true, normalizeExpectedIdentifier);
        }

        VerifyKey(errors, tables, actualKeys, "outbox_subscriber_queue", ["message_id"], isPrimary: false, normalizeExpectedIdentifier);
    }

    internal static void VerifyForeignKeys(
        List<SchemaVerificationError> errors,
        IReadOnlyDictionary<string, HashSet<string>> tables,
        IReadOnlyList<ForeignKeyColumn> actualForeignKeys,
        IReadOnlyDictionary<string, string>? expectedNames = null,
        Func<string, string>? normalizeExpectedIdentifier = null,
        bool referencedTableNamesAreCaseSensitive = false)
    {
        string[] columns = ["pool_id", "entity_type_id", "entity_id"];
        var targetTable = FindName(tables.Keys, "entities", normalizeExpectedIdentifier);
        string? FindReferencedTable(string name) =>
            referencedTableNamesAreCaseSensitive ? name : FindName(tables.Keys, name);

        foreach (var table in new[] { "entity_keys", "search_values" })
        {
            var actualTable = FindName(tables.Keys, table, normalizeExpectedIdentifier);
            if (actualTable is null)
            {
                continue;
            }

            var candidates = actualForeignKeys
                .Where(fk => string.Equals(fk.Table, actualTable, StringComparison.Ordinal))
                .GroupBy(fk => fk.Constraint, StringComparer.Ordinal)
                .Select(group => group.OrderBy(fk => fk.Position).ToArray())
                .Where(group =>
                    (expectedNames is null || EqualsIgnoreCase(group[0].Constraint, expectedNames[table])) &&
                    group[0].IsSameSchema &&
                    targetTable is not null &&
                    string.Equals(FindReferencedTable(group[0].ReferencedTable), targetTable, StringComparison.Ordinal) &&
                    ColumnsMatch(tables[actualTable], group.Select(fk => fk.Column).ToArray(), columns, normalizeExpectedIdentifier) &&
                    ColumnsMatch(tables[targetTable], group.Select(fk => fk.ReferencedColumn).ToArray(), columns, normalizeExpectedIdentifier))
                .ToArray();

            if (candidates.Length == 0)
            {
                var expectedColumnNames = columns.Select(column => normalizeExpectedIdentifier?.Invoke(column) ?? column);
                errors.Add(new SchemaVerificationError(
                    table, null,
                    $"Foreign key from '{table}' to 'entities' with columns ({string.Join(", ", expectedColumnNames)}) is missing.",
                    SchemaVerificationErrorKind.MissingForeignKey));
            }
            else if (!candidates.Any(group => EqualsIgnoreCase(group[0].DeleteRule, "CASCADE")))
            {
                errors.Add(new SchemaVerificationError(
                    table, null,
                    $"Foreign key from '{table}' to 'entities' has delete rule '{candidates[0][0].DeleteRule}', expected 'CASCADE'.",
                    SchemaVerificationErrorKind.Other));
            }
        }
    }

    private static void VerifyKey(
        List<SchemaVerificationError> errors,
        IReadOnlyDictionary<string, HashSet<string>> tables,
        IReadOnlyList<KeyColumn> actualKeys,
        string table,
        string[] expectedColumns,
        bool isPrimary,
        Func<string, string>? normalizeExpectedIdentifier)
    {
        var actualTable = FindName(tables.Keys, table, normalizeExpectedIdentifier);
        if (actualTable is null)
        {
            return;
        }

        var keys = actualKeys
            .Where(key => string.Equals(key.Table, actualTable, StringComparison.Ordinal) && key.IsPrimary == isPrimary)
            .GroupBy(key => key.Constraint, StringComparer.Ordinal)
            .Select(group => group.OrderBy(key => key.Position).Select(key => key.Column).ToArray())
            .ToArray();
        if (keys.Any(columns => ColumnsMatch(tables[actualTable], columns, expectedColumns, normalizeExpectedIdentifier)))
        {
            return;
        }

        var kind = isPrimary ? "Primary key" : "Unique constraint";
        var detail = keys.Length == 0
            ? "is missing"
            : $"has columns ({string.Join(", ", keys[0])})";
        var expectedColumnNames = expectedColumns.Select(column => normalizeExpectedIdentifier?.Invoke(column) ?? column);
        errors.Add(new SchemaVerificationError(
            table, null,
            $"{kind} on table '{table}' {detail}, expected ({string.Join(", ", expectedColumnNames)}).",
            SchemaVerificationErrorKind.Other));
    }

    internal static string? FindName(
        IEnumerable<string> names,
        string expected,
        Func<string, string>? normalizeExpectedIdentifier = null)
    {
        expected = normalizeExpectedIdentifier?.Invoke(expected) ?? expected;
        string? match = null;
        foreach (var name in names)
        {
            if (string.Equals(name, expected, StringComparison.Ordinal))
            {
                return name;
            }

            if (match is null && EqualsIgnoreCase(name, expected))
            {
                match = name;
            }
        }

        return match;
    }

    private static bool ColumnsMatch(
        IReadOnlyCollection<string> names,
        string[] actual,
        string[] expected,
        Func<string, string>? normalizeExpectedIdentifier)
    {
        if (actual.Length != expected.Length)
        {
            return false;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            var expectedName = FindName(names, expected[i], normalizeExpectedIdentifier);
            if (expectedName is null || !string.Equals(expectedName, FindName(names, actual[i]), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EqualsIgnoreCase(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
