// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Text;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Visitor that translates query expression trees to SQL WHERE clauses with parameterized EXISTS subqueries.
/// All values are parameterized to prevent SQL injection.
/// </summary>
internal sealed class SqlWhereClauseBuilder(string schemaName, DbCommand command, ISqlDialect dialect) : IQueryExpressionVisitor<string>
{
    private readonly string _qualifiedSearchValues = $"{dialect.QuoteIdentifier(schemaName)}.{dialect.QuoteIdentifier("search_values")}";
    private int _parameterCounter;
    private int _subqueryCounter;

    /// <summary>
    /// Returns the entity table column name for a system/reserved field path, or null if not a system field.
    /// Throws if the field path is a system field but the field type is not DateTimeField.
    /// </summary>
    private static string? GetSystemColumnName(string fieldPath, Field? field)
    {
        if (string.Equals(fieldPath, SystemFields.Created, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldPath, SystemFields.CreatedAttributeName, StringComparison.OrdinalIgnoreCase))
        {
            if (field is not null and not DateTimeField)
            {
                throw new InvalidOperationException($"System field '{fieldPath}' must use DateTimeField, not {field.GetType().Name}.");
            }

            return "v.created_at";
        }

        if (string.Equals(fieldPath, SystemFields.LastUpdated, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldPath, SystemFields.LastUpdatedAttributeName, StringComparison.OrdinalIgnoreCase))
        {
            if (field is not null and not DateTimeField)
            {
                throw new InvalidOperationException($"System field '{fieldPath}' must use DateTimeField, not {field.GetType().Name}.");
            }

            return "v.last_updated_at";
        }

        return null;
    }

    /// <summary>
    /// Returns the item_index condition for a field. Scalar fields use item_index = -1 to hit
    /// the partial index; multi-valued fields use item_index >= 0 to match any array element.
    /// </summary>
    private static string ItemIndexCondition(string alias, Field field) =>
        field.IsMultiValued
            ? $"{alias}.item_index >= 0"
            : $"{alias}.item_index = -1";

    /// <summary>
    /// Converts a field path string to the dialect-specific parameter value (GUID as byte[] for SQLite, Guid for others).
    /// </summary>
    private object FieldPathToParam(string fieldPath) =>
        dialect.FieldPathToParameterValue(DeterministicGuidGenerator.Create(fieldPath.ToUpperInvariant()));

    private static object FieldPathToParam(ISqlDialect dialect, string fieldPath) =>
        dialect.FieldPathToParameterValue(DeterministicGuidGenerator.Create(fieldPath.ToUpperInvariant()));

    /// <summary>
    /// Builds the WHERE clause for the given expression.
    /// </summary>
    public string BuildWhereClause(IQueryExpression expression)
    {
        var clause = expression switch
        {
            AllExpression all => Visit(all),
            EqualExpression eq => Visit(eq),
            ContainsExpression contains => Visit(contains),
            StartsWithExpression startsWith => Visit(startsWith),
            EndsWithExpression endsWith => Visit(endsWith),
            GreaterThanExpression gt => Visit(gt),
            LessThanExpression lt => Visit(lt),
            GreaterOrEqualExpression gte => Visit(gte),
            LessOrEqualExpression lte => Visit(lte),
            BetweenExpression between => Visit(between),
            InExpression inExpr => Visit(inExpr),
            NotExpression not => Visit(not),
            AndExpression and => Visit(and),
            OrExpression or => Visit(or),
            ArrayFilterExpression arrayFilter => Visit(arrayFilter),
            ArrayContainsExpression arrayContains => Visit(arrayContains),
            PresentExpression present => Visit(present),
            _ => throw new NotSupportedException($"Expression type {expression.GetType().Name} is not supported.")
        };

        return clause;
    }

    // No filter - match all records
    public string Visit(AllExpression expression) => dialect.TrueLiteral;

    public string Visit(EqualExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var value = expression.Value;
        var (columnName, paramValue) = GetValueColumnAndParameter(value);

        var systemColumn = GetSystemColumnName(fieldPath, expression.Field);
        if (systemColumn is not null)
        {
            var valueParam = AddParameter($"value_{_parameterCounter}", paramValue);
            _parameterCounter++;
            return $"{systemColumn} = {valueParam}";
        }

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var valueParam2 = AddParameter($"value_{_parameterCounter}", paramValue);
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.{columnName} = {valueParam2}
            )
            """;
    }

    public string Visit(ContainsExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var value = expression.Value;

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var escapedValue = dialect.EscapeLikeWildcards(value);
        var valueParam = AddParameter($"value_{_parameterCounter}", $"%{escapedValue}%");
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.string_value {dialect.CaseInsensitiveLikeOperator} {valueParam}{dialect.LikeEscapeClause}
            )
            """;
    }

    public string Visit(StartsWithExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var value = expression.Value;

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var escapedValue = dialect.EscapeLikeWildcards(value);
        var valueParam = AddParameter($"value_{_parameterCounter}", $"{escapedValue}%");
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.string_value {dialect.CaseInsensitiveLikeOperator} {valueParam}{dialect.LikeEscapeClause}
            )
            """;
    }

    public string Visit(GreaterThanExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var value = expression.Value;
        var (columnName, paramValue) = GetValueColumnAndParameter(value);

        var systemColumn = GetSystemColumnName(fieldPath, expression.Field);
        if (systemColumn is not null)
        {
            var valueParam = AddParameter($"value_{_parameterCounter}", paramValue);
            _parameterCounter++;
            return $"{systemColumn} > {valueParam}";
        }

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var valueParam2 = AddParameter($"value_{_parameterCounter}", paramValue);
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.{columnName} > {valueParam2}
            )
            """;
    }

    public string Visit(LessThanExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var value = expression.Value;
        var (columnName, paramValue) = GetValueColumnAndParameter(value);

        var systemColumn = GetSystemColumnName(fieldPath, expression.Field);
        if (systemColumn is not null)
        {
            var valueParam = AddParameter($"value_{_parameterCounter}", paramValue);
            _parameterCounter++;
            return $"{systemColumn} < {valueParam}";
        }

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var valueParam2 = AddParameter($"value_{_parameterCounter}", paramValue);
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.{columnName} < {valueParam2}
            )
            """;
    }

    public string Visit(GreaterOrEqualExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var value = expression.Value;
        var (columnName, paramValue) = GetValueColumnAndParameter(value);

        var systemColumn = GetSystemColumnName(fieldPath, expression.Field);
        if (systemColumn is not null)
        {
            var valueParam = AddParameter($"value_{_parameterCounter}", paramValue);
            _parameterCounter++;
            return $"{systemColumn} >= {valueParam}";
        }

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var valueParam2 = AddParameter($"value_{_parameterCounter}", paramValue);
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.{columnName} >= {valueParam2}
            )
            """;
    }

    public string Visit(LessOrEqualExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var value = expression.Value;
        var (columnName, paramValue) = GetValueColumnAndParameter(value);

        var systemColumn = GetSystemColumnName(fieldPath, expression.Field);
        if (systemColumn is not null)
        {
            var valueParam = AddParameter($"value_{_parameterCounter}", paramValue);
            _parameterCounter++;
            return $"{systemColumn} <= {valueParam}";
        }

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var valueParam2 = AddParameter($"value_{_parameterCounter}", paramValue);
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.{columnName} <= {valueParam2}
            )
            """;
    }

    public string Visit(BetweenExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var minValue = expression.Min;
        var maxValue = expression.Max;
        var (columnName, minParamValue) = GetValueColumnAndParameter(minValue);
        var (_, maxParamValue) = GetValueColumnAndParameter(maxValue);

        var systemColumn = GetSystemColumnName(fieldPath, expression.Field);
        if (systemColumn is not null)
        {
            var minParam = AddParameter($"min_{_parameterCounter}", minParamValue);
            var maxParam = AddParameter($"max_{_parameterCounter}", maxParamValue);
            _parameterCounter++;
            return $"{systemColumn} BETWEEN {minParam} AND {maxParam}";
        }

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var minParam2 = AddParameter($"min_{_parameterCounter}", minParamValue);
        var maxParam2 = AddParameter($"max_{_parameterCounter}", maxParamValue);
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.{columnName} BETWEEN {minParam2} AND {maxParam2}
            )
            """;
    }

    public string Visit(InExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var values = expression.Values;

        var valuesList = new List<object>();
        foreach (var value in values)
        {
            valuesList.Add(value);
        }

        if (valuesList.Count == 0)
        {
            // IN with empty list never matches
            return dialect.FalseLiteral;
        }

        var systemColumn = GetSystemColumnName(fieldPath, expression.Field);
        if (systemColumn is not null)
        {
            var sysValueParams = new List<string>();
            for (var i = 0; i < valuesList.Count; i++)
            {
                var (_, paramValue) = GetValueColumnAndParameter(valuesList[i]);
                var valueParam = AddParameter($"in_value_{_parameterCounter}_{i}", paramValue);
                sysValueParams.Add(valueParam);
            }

            _parameterCounter++;
            var sysInClause = string.Join(", ", sysValueParams);
            return $"{systemColumn} IN ({sysInClause})";
        }

        var (columnName, _) = GetValueColumnAndParameter(valuesList[0]);
        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));

        // Add parameters for all values
        var valueParams = new List<string>();
        for (var i = 0; i < valuesList.Count; i++)
        {
            var (_, paramValue) = GetValueColumnAndParameter(valuesList[i]);
            var valueParam = AddParameter($"in_value_{_parameterCounter}_{i}", paramValue);
            valueParams.Add(valueParam);
        }

        _parameterCounter++;
        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        var inClause = string.Join(", ", valueParams);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.{columnName} IN ({inClause})
            )
            """;
    }

    public string Visit(AndExpression expression)
    {
        var parts = new List<string>();
        foreach (var part in expression.Parts)
        {
            var clause = BuildWhereClause(part);
            parts.Add($"({clause})");
        }

        return string.Join(" AND ", parts);
    }

    public string Visit(OrExpression expression)
    {
        var parts = new List<string>();
        foreach (var part in expression.Parts)
        {
            var clause = BuildWhereClause(part);
            parts.Add($"({clause})");
        }

        return string.Join(" OR ", parts);
    }

    public string Visit(ArrayFilterExpression expression)
    {
        var arrayFieldPath = expression.ArrayFieldPath;
        var filter = expression.Filter;

        // Create a scoped builder for array item filters
        var arrayItemBuilder = new ArrayItemSqlBuilder(schemaName, command, dialect, arrayFieldPath, _parameterCounter, _subqueryCounter);
        var filterClause = arrayItemBuilder.BuildArrayItemFilter(filter);

        // Update counters
        _parameterCounter = arrayItemBuilder.ParameterCounter;
        _subqueryCounter = arrayItemBuilder.SubqueryCounter;

        return filterClause;
    }

    public string Visit(NotExpression expression)
    {
        var innerClause = BuildWhereClause(expression.Inner);
        return $"NOT ({innerClause})";
    }

    public string Visit(EndsWithExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var value = expression.Value;

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var escapedValue = dialect.EscapeLikeWildcards(value);
        var valueParam = AddParameter($"value_{_parameterCounter}", $"%{escapedValue}");
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;
        var itemIndexCond = ItemIndexCondition($"sv{subqueryId}", expression.Field);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND {itemIndexCond}
                  AND sv{subqueryId}.string_value {dialect.CaseInsensitiveLikeOperator} {valueParam}{dialect.LikeEscapeClause}
            )
            """;
    }

    public string Visit(PresentExpression expression)
    {
        var fieldPath = expression.Field.Path;

        // System fields are NOT NULL columns on the entities table — they are always present.
        if (SystemFields.IsSystemField(fieldPath))
        {
            return "1=1";
        }

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;

        if (expression.Field.IsMultiValued)
        {
            // For multi-valued fields, check that at least one array element exists (item_index >= 0)
            return $"""
                EXISTS (
                    SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                    WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                      AND sv{subqueryId}.pool_id = v.pool_id
                      AND sv{subqueryId}.entity_id = v.entity_id
                      AND sv{subqueryId}.field_path = {fieldPathParam}
                      AND sv{subqueryId}.item_index >= 0
                )
                """;
        }

        // For scalar fields, check that a non-null value exists with item_index = -1
        var columnName = GetColumnNameForFieldType(expression.Field.Type);

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND sv{subqueryId}.item_index = -1
                  AND sv{subqueryId}.{columnName} IS NOT NULL
            )
            """;
    }

    public string Visit(ArrayContainsExpression expression)
    {
        var fieldPath = expression.Field.Path;
        var value = expression.Value;

        var fieldPathParam = AddParameter($"field_path_{_parameterCounter}", FieldPathToParam(fieldPath));
        var guidValue = DeterministicGuidGenerator.Create(value.ToUpperInvariant());
        var valueParam = AddParameter($"value_{_parameterCounter}", guidValue);
        _parameterCounter++;

        var subqueryId = _subqueryCounter++;

        return $"""
            EXISTS (
                SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}
                WHERE sv{subqueryId}.entity_type_id = v.entity_type_id
                  AND sv{subqueryId}.pool_id = v.pool_id
                  AND sv{subqueryId}.entity_id = v.entity_id
                  AND sv{subqueryId}.field_path = {fieldPathParam}
                  AND sv{subqueryId}.item_index >= 0
                  AND sv{subqueryId}.guid_value = {valueParam}
            )
            """;
    }

    /// <summary>
    /// Gets the appropriate column name and parameter value based on the value type.
    /// </summary>
    private static (string ColumnName, object ParameterValue) GetValueColumnAndParameter(object value) => value switch
    {
        Guid g => ("guid_value", g),
        string => ("string_value", value),
        decimal d => ("number_value", d),
        DateTimeOffset dto => ("datetime_value", dto.UtcDateTime),
        DateTime dt => ("datetime_value", dt.ToUniversalTime()),
        bool b => ("boolean_value", b),
        _ => throw new NotSupportedException($"Value type {value.GetType().Name} is not supported.")
    };

    /// <summary>
    /// Gets the column name for the given field type.
    /// </summary>
    private static string GetColumnNameForFieldType(FieldType fieldType) => fieldType switch
    {
        FieldType.String => "string_value",
        FieldType.Number => "number_value",
        FieldType.DateTime => "datetime_value",
        FieldType.Boolean => "boolean_value",
        FieldType.Guid => "guid_value",
        _ => throw new NotSupportedException($"Field type {fieldType} is not supported.")
    };

    /// <summary>
    /// Adds a parameter to the command and returns the parameter name.
    /// </summary>
    private string AddParameter(string name, object value)
    {
        var paramName = $"@{name}";
        dialect.AddParameter(command, paramName, value);
        return paramName;
    }

    /// <summary>
    /// Helper class for building SQL for array item filters with item_index correlation.
    /// </summary>
    private sealed class ArrayItemSqlBuilder(
        string schemaName,
        DbCommand command,
        ISqlDialect dialect,
        string arrayFieldPath,
        int parameterCounter,
        int subqueryCounter)
    {
        private readonly string _qualifiedSearchValues = $"{dialect.QuoteIdentifier(schemaName)}.{dialect.QuoteIdentifier("search_values")}";
        private int _parameterCounter = parameterCounter;
        private int _subqueryCounter = subqueryCounter;

        public int ParameterCounter => _parameterCounter;
        public int SubqueryCounter => _subqueryCounter;

        public string BuildArrayItemFilter(IQueryFilterExpression filter)
        {
            // Check if this is a top-level OR expression
            if (filter is OrExpression orExpr)
            {
                return BuildOrFilter(orExpr);
            }

            // For AND and leaf expressions, collect all conditions
            var conditions = new List<ArrayItemCondition>();
            CollectConditions(filter, conditions);

            if (conditions.Count == 0)
            {
                return dialect.FalseLiteral;
            }

            return BuildAndFilter(conditions);
        }

        private string BuildAndFilter(List<ArrayItemCondition> conditions)
        {
            // Build the correlated subquery with JOINs for AND conditions
            var subqueryId = _subqueryCounter++;
            var sb = new StringBuilder();

            _ = sb.AppendLine("EXISTS (");
            _ = sb.Append(CultureInfo.InvariantCulture, $"    SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}_0");

            // Add JOINs for additional conditions (all correlating on item_index)
            for (var i = 1; i < conditions.Count; i++)
            {
                _ = sb.Append(CultureInfo.InvariantCulture, $"""

                        INNER JOIN {_qualifiedSearchValues} sv{subqueryId}_{i}
                          ON sv{subqueryId}_0.entity_type_id = sv{subqueryId}_{i}.entity_type_id
                          AND sv{subqueryId}_0.pool_id = sv{subqueryId}_{i}.pool_id
                          AND sv{subqueryId}_0.entity_id = sv{subqueryId}_{i}.entity_id
                          AND sv{subqueryId}_0.item_index = sv{subqueryId}_{i}.item_index
                    """);
            }

            // WHERE clause
            _ = sb.Append(CultureInfo.InvariantCulture, $"""

                        WHERE sv{subqueryId}_0.entity_type_id = v.entity_type_id
                          AND sv{subqueryId}_0.pool_id = v.pool_id
                          AND sv{subqueryId}_0.entity_id = v.entity_id
                          AND sv{subqueryId}_0.item_index IS NOT NULL
                    """);

            // Add conditions for each joined table
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                var fieldPath = $"{arrayFieldPath}.{condition.FieldPath}";
                var fieldPathParam = AddParameter($"array_field_{_parameterCounter}_{i}", FieldPathToParam(dialect, fieldPath));

                var conditionClause = BuildConditionClause(condition, subqueryId, i);
                _ = sb.Append(CultureInfo.InvariantCulture, $"""

                          AND sv{subqueryId}_{i}.field_path = {fieldPathParam}
                          AND {conditionClause}
                    """);
            }

            _parameterCounter++;

            _ = sb.AppendLine();
            _ = sb.Append(')');

            return sb.ToString();
        }

        private string BuildOrFilter(OrExpression orExpr)
        {
            var parts = new List<string>();

            foreach (var part in orExpr.Parts)
            {
                // Each OR branch might be AND or a leaf condition
                if (part is AndExpression || IsLeafExpression(part))
                {
                    var conditions = new List<ArrayItemCondition>();
                    CollectConditions(part, conditions);

                    if (conditions.Count > 0)
                    {
                        parts.Add(BuildOrBranch(conditions));
                    }
                }
                else if (part is OrExpression nestedOr)
                {
                    // Flatten nested OR expressions
                    foreach (var nestedPart in nestedOr.Parts)
                    {
                        var conditions = new List<ArrayItemCondition>();
                        CollectConditions(nestedPart, conditions);

                        if (conditions.Count > 0)
                        {
                            parts.Add(BuildOrBranch(conditions));
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException($"Expression type {part.GetType().Name} is not supported in OR expressions within array filters.");
                }
            }

            if (parts.Count == 0)
            {
                return dialect.FalseLiteral;
            }

            // Combine branches with OR - each branch is already wrapped in EXISTS
            return string.Join(" OR ", parts.Select(p => $"({p})"));
        }

        private string BuildOrBranch(List<ArrayItemCondition> conditions)
        {
            var subqueryId = _subqueryCounter++;
            var sb = new StringBuilder();

            _ = sb.AppendLine("EXISTS (");
            _ = sb.Append(CultureInfo.InvariantCulture, $"    SELECT 1 FROM {_qualifiedSearchValues} sv{subqueryId}_0");

            // Add JOINs for additional conditions in this branch
            for (var i = 1; i < conditions.Count; i++)
            {
                _ = sb.Append(CultureInfo.InvariantCulture, $"""

                        INNER JOIN {_qualifiedSearchValues} sv{subqueryId}_{i}
                          ON sv{subqueryId}_0.entity_type_id = sv{subqueryId}_{i}.entity_type_id
                          AND sv{subqueryId}_0.pool_id = sv{subqueryId}_{i}.pool_id
                          AND sv{subqueryId}_0.entity_id = sv{subqueryId}_{i}.entity_id
                          AND sv{subqueryId}_0.item_index = sv{subqueryId}_{i}.item_index
                    """);
            }

            // WHERE clause
            _ = sb.Append(CultureInfo.InvariantCulture, $"""

                        WHERE sv{subqueryId}_0.entity_type_id = v.entity_type_id
                          AND sv{subqueryId}_0.pool_id = v.pool_id
                          AND sv{subqueryId}_0.entity_id = v.entity_id
                          AND sv{subqueryId}_0.item_index IS NOT NULL
                    """);

            // Add conditions for this branch
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                var fieldPath = $"{arrayFieldPath}.{condition.FieldPath}";
                var fieldPathParam = AddParameter($"array_field_{_parameterCounter}_{i}", FieldPathToParam(dialect, fieldPath));

                var conditionClause = BuildConditionClause(condition, subqueryId, i);
                _ = sb.Append(CultureInfo.InvariantCulture, $"""

                          AND sv{subqueryId}_{i}.field_path = {fieldPathParam}
                          AND {conditionClause}
                    """);
            }

            _parameterCounter++;

            _ = sb.AppendLine();
            _ = sb.Append(')');

            return sb.ToString();
        }

        private static bool IsLeafExpression(IQueryFilterExpression expr) =>
            expr is EqualExpression
                or ContainsExpression
                or StartsWithExpression
                or EndsWithExpression
                or GreaterThanExpression
                or LessThanExpression
                or GreaterOrEqualExpression
                or LessOrEqualExpression
                or BetweenExpression
                or InExpression
                or PresentExpression
                or NotExpression;

        private void CollectConditions(IQueryFilterExpression filter, List<ArrayItemCondition> conditions)
        {
            switch (filter)
            {
                case EqualExpression eq:
                    conditions.Add(new ArrayItemCondition(eq.Field.Path, "=", eq.Value));
                    break;
                case ContainsExpression contains:
                    {
                        var escapedValue = dialect.EscapeLikeWildcards(contains.Value);
                        conditions.Add(new ArrayItemCondition(contains.Field.Path, dialect.CaseInsensitiveLikeOperator, $"%{escapedValue}%"));
                        break;
                    }
                case StartsWithExpression startsWith:
                    {
                        var escapedValue = dialect.EscapeLikeWildcards(startsWith.Value);
                        conditions.Add(new ArrayItemCondition(startsWith.Field.Path, dialect.CaseInsensitiveLikeOperator, $"{escapedValue}%"));
                        break;
                    }
                case EndsWithExpression endsWith:
                    {
                        var escapedValue = dialect.EscapeLikeWildcards(endsWith.Value);
                        conditions.Add(new ArrayItemCondition(endsWith.Field.Path, dialect.CaseInsensitiveLikeOperator, $"%{escapedValue}"));
                        break;
                    }
                case GreaterThanExpression gt:
                    conditions.Add(new ArrayItemCondition(gt.Field.Path, ">", gt.Value));
                    break;
                case LessThanExpression lt:
                    conditions.Add(new ArrayItemCondition(lt.Field.Path, "<", lt.Value));
                    break;
                case GreaterOrEqualExpression gte:
                    conditions.Add(new ArrayItemCondition(gte.Field.Path, ">=", gte.Value));
                    break;
                case LessOrEqualExpression lte:
                    conditions.Add(new ArrayItemCondition(lte.Field.Path, "<=", lte.Value));
                    break;
                case BetweenExpression between:
                    // Split BETWEEN into two conditions
                    conditions.Add(new ArrayItemCondition(between.Field.Path, ">=", between.Min));
                    conditions.Add(new ArrayItemCondition(between.Field.Path, "<=", between.Max));
                    break;
                case InExpression inExpr:
                    conditions.Add(new ArrayItemCondition(inExpr.Field.Path, "IN", inExpr.Values));
                    break;
                case NotExpression notExpr:
                    CollectNotConditions(notExpr, conditions);
                    break;
                case PresentExpression present:
                    conditions.Add(new ArrayItemCondition(present.Field.Path, "IS NOT NULL", present.Field.Type));
                    break;
                case AndExpression and:
                    // For AND within array filter, collect all conditions (they must all match the same item)
                    foreach (var part in and.Parts)
                    {
                        CollectConditions(part, conditions);
                    }
                    break;
                case OrExpression:
                    // OR expressions should be handled at a higher level in BuildArrayItemFilter
                    throw new InvalidOperationException("OR expressions should not reach CollectConditions. This is a bug.");
                case ArrayFilterExpression:
                    throw new NotSupportedException("Nested array filters are not supported.");
                default:
                    throw new NotSupportedException($"Expression type {filter.GetType().Name} is not supported in array filters.");
            }
        }

        private void CollectNotConditions(NotExpression notExpr, List<ArrayItemCondition> conditions)
        {
            switch (notExpr.Inner)
            {
                case EqualExpression eq:
                    conditions.Add(new ArrayItemCondition(eq.Field.Path, "!=", eq.Value));
                    break;
                case ContainsExpression contains:
                    {
                        var escapedValue = dialect.EscapeLikeWildcards(contains.Value);
                        conditions.Add(new ArrayItemCondition(contains.Field.Path, $"NOT {dialect.CaseInsensitiveLikeOperator}", $"%{escapedValue}%"));
                        break;
                    }
                case StartsWithExpression startsWith:
                    {
                        var escapedValue = dialect.EscapeLikeWildcards(startsWith.Value);
                        conditions.Add(new ArrayItemCondition(startsWith.Field.Path, $"NOT {dialect.CaseInsensitiveLikeOperator}", $"{escapedValue}%"));
                        break;
                    }
                case EndsWithExpression endsWith:
                    {
                        var escapedValue = dialect.EscapeLikeWildcards(endsWith.Value);
                        conditions.Add(new ArrayItemCondition(endsWith.Field.Path, $"NOT {dialect.CaseInsensitiveLikeOperator}", $"%{escapedValue}"));
                        break;
                    }
                case PresentExpression present:
                    conditions.Add(new ArrayItemCondition(present.Field.Path, "IS NULL", present.Field.Type));
                    break;
                default:
                    throw new NotSupportedException($"NOT wrapping {notExpr.Inner.GetType().Name} is not supported in array filters. Use NOT at the top level instead.");
            }
        }

        private string BuildConditionClause(ArrayItemCondition condition, int subqueryId, int tableIndex)
        {
            if (condition.Operator is "IS NOT NULL" or "IS NULL")
            {
                // For presence checks, the Value holds the FieldType enum to determine the column
                var fieldType = (FieldType)condition.Value;
                var columnName = fieldType switch
                {
                    FieldType.String => "string_value",
                    FieldType.Number => "number_value",
                    FieldType.DateTime => "datetime_value",
                    FieldType.Boolean => "boolean_value",
                    FieldType.Guid => "guid_value",
                    _ => throw new NotSupportedException($"Field type {fieldType} is not supported.")
                };
                return $"sv{subqueryId}_{tableIndex}.{columnName} {condition.Operator}";
            }

            if (condition.Operator == "IN")
            {
                var valuesList = new List<object>();
                foreach (var value in (IEnumerable)condition.Value)
                {
                    valuesList.Add(value);
                }

                if (valuesList.Count == 0)
                {
                    return dialect.FalseLiteral;
                }

                // Get column name from first value in the list
                var (columnName, _) = GetValueColumnAndParameter(valuesList[0]);

                var valueParams = new List<string>();
                for (var i = 0; i < valuesList.Count; i++)
                {
                    var (_, val) = GetValueColumnAndParameter(valuesList[i]);
                    var valueParam = AddParameter($"array_in_{_parameterCounter}_{tableIndex}_{i}", val);
                    valueParams.Add(valueParam);
                }

                var inClause = string.Join(", ", valueParams);
                return $"sv{subqueryId}_{tableIndex}.{columnName} IN ({inClause})";
            }
            else
            {
                var (columnName, paramValue) = GetValueColumnAndParameter(condition.Value);
                var valueParam = AddParameter($"array_value_{_parameterCounter}_{tableIndex}", paramValue);
                var likeEscape = condition.Operator.Contains("LIKE", StringComparison.OrdinalIgnoreCase)
                    ? dialect.LikeEscapeClause
                    : "";
                return $"sv{subqueryId}_{tableIndex}.{columnName} {condition.Operator} {valueParam}{likeEscape}";
            }
        }

        private static (string ColumnName, object ParameterValue) GetValueColumnAndParameter(object value) => value switch
        {
            Guid g => ("guid_value", g),
            string => ("string_value", value),
            decimal d => ("number_value", d),
            DateTimeOffset dto => ("datetime_value", dto),
            DateTime dt => ("datetime_value", new DateTimeOffset(dt.ToUniversalTime())),
            bool b => ("boolean_value", b),
            IEnumerable => ("", value), // For IN operator, return the collection as-is
            _ => throw new NotSupportedException($"Value type {value.GetType().Name} is not supported.")
        };

        private string AddParameter(string name, object value)
        {
            var paramName = $"@{name}";
            dialect.AddParameter(command, paramName, value);
            return paramName;
        }

        private sealed record ArrayItemCondition(string FieldPath, string Operator, object Value);
    }
}
