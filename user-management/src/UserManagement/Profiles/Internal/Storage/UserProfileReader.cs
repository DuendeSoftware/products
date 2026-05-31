// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal.Filtering;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement.Internal.Storage;
using QueryBuilder = Duende.Storage.Internal.Querying.Query;

namespace Duende.UserManagement.Profiles.Internal.Storage;

/// <summary>
/// Reads users from the store using SCIM filter expressions.
/// Translates SCIM filter strings into platform query expressions using the user's
/// dynamic schema to resolve attribute types.
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserProfileReader(IStoreFactory storeFactory, AttributeSchemaRepository schemaRepo)
{
    /// <summary>
    /// Queries users using a SCIM filter expression with page-based pagination and sort support.
    /// </summary>
    /// <param name="filter">
    /// A SCIM filter expression string (e.g., <c>userName eq "john"</c>,
    /// <c>attribute_1 eq true</c>). Pass null or empty to return all users.
    /// </param>
    /// <param name="sortBy">The attribute name to sort by. Pass null for no sorting.</param>
    /// <param name="sortDirection">The sort direction.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page. Clamped to [1, 200].</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing <see cref="UserProfileListItem"/> instances.</returns>
    internal async Task<QueryResult<UserProfileListItem>> QueryAsync(
        string? filter,
        string? sortBy,
        SortDirection sortDirection,
        int pageNumber,
        int pageSize,
        Ct ct)
    {
        // Clamp page parameters
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);
        return await ExecuteQueryAsync(filter, sortBy, sortDirection, DataRange.FromPage(pageNumber, pageSize), ct);
    }

    /// <summary>
    /// Queries users using a SCIM filter expression with explicit data range pagination and sort support.
    /// Use <see cref="DataRange.FromOffset(OffsetSkip?, DataRangeSize?)"/> for SCIM-style offset-based pagination.
    /// </summary>
    /// <param name="filter">A SCIM filter expression string. Pass null or empty to return all users.</param>
    /// <param name="sortBy">The attribute name to sort by. Pass null for no sorting.</param>
    /// <param name="sortDirection">The sort direction.</param>
    /// <param name="dataRange">The data range controlling skip and take. Use <see cref="DataRange.FromOffset(OffsetSkip?, DataRangeSize?)"/> for SCIM offset pagination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing <see cref="UserProfileListItem"/> instances.</returns>
    internal Task<QueryResult<UserProfileListItem>> QueryAsync(
        string? filter,
        string? sortBy,
        SortDirection sortDirection,
        DataRange dataRange,
        Ct ct) =>
        ExecuteQueryAsync(filter, sortBy, sortDirection, dataRange, ct);

    private async Task<QueryResult<UserProfileListItem>> ExecuteQueryAsync(
        string? filter,
        string? sortBy,
        SortDirection sortDirection,
        DataRange dataRange,
        Ct ct)
    {
        // Note: filtering and sorting only work on attributes that have IsQueryable = true in the schema.
        // Non-indexed attributes are stored only in the entity's JSON payload and cannot be used in
        // filter expressions or sort parameters — attempting to do so throws NotSupportedException.
        var queryStore = storeFactory.GetStore();
        var schema = (await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct))?.AttributeSchema;
        var attributeDefinitions = schema?.AttributeDefinitions ??
                                   new Dictionary<AttributeCode, AttributeDefinition>();

        var queryFilter = TranslateFilter(filter, attributeDefinitions);
        var sort = BuildSortParameter(sortBy, sortDirection, attributeDefinitions);

        var result = await queryStore.QueryAsync<UserProfileDso.V1>(
            UserProfileDso.EntityType,
            queryFilter,
            sort,
            dataRange,
            ct);

        return result.ConvertTo(e => ToListItem(e.Value, schema));
    }

    private static SortParameter BuildSortParameter(
        string? sortBy,
        SortDirection sortDirection,
        IReadOnlyDictionary<AttributeCode, AttributeDefinition> attributeDefinitions)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return SortParameter.Empty;
        }

        var resolver = new AttributeTypeResolver(attributeDefinitions);
        try
        {
            var field = resolver.ResolveField(sortBy);
            return new SortParameter(field, sortDirection);
        }
        catch (NotSupportedException)
        {
            return SortParameter.Empty; // Unknown sort field → ignore sort
        }
    }

    private static IQueryExpression TranslateFilter(
        string? filter,
        IReadOnlyDictionary<AttributeCode, AttributeDefinition> attributeDefinitions)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return QueryBuilder.All();
        }

        var resolver = new AttributeTypeResolver(attributeDefinitions);
        var translator = new FilterTranslator(resolver);
        var translated = translator.Translate(filter);

        return translated is not null
            ? QueryBuilder.Where(translated)
            : QueryBuilder.All();
    }

    private static UserProfileListItem ToListItem(UserProfileDso.V1 dso, AttributeSchema? schema)
    {
        var subjectId = UserSubjectId.Load(dso.SubjectId);

        // Build the attributes dictionary from the DSO's schema attributes
        var attributes = new Dictionary<string, object>();

        if (schema is not null)
        {
            foreach (var attrDso in dso.Attributes)
            {
                var name = AttributeCode.Load(attrDso.Name);
                if (!schema.AttributeDefinitions.TryGetValue(name, out var definition))
                {
                    continue;
                }

                var value = ParseAttributeValue(attrDso.Value, definition.AttributeType);
                if (value is not null)
                {
                    attributes[attrDso.Name] = value;
                }
            }
        }

        return new UserProfileListItem(subjectId, attributes);
    }

    private static object? ParseAttributeValue(object? rawValue, AttributeType type)
    {
        // System.Text.Json may return JsonElement on deserialization — normalize first.
        var value = rawValue is JsonElement je ? NormalizeJsonElement(je) : rawValue;

        return type switch
        {
            ScalarAttributeType scalar => ParseScalarValue(value, scalar.DataType),
            ComplexAttributeType complexType => ParseComplexValue(value, complexType),
            ListAttributeType listType => ParseListValue(value, listType),
            _ => null
        };
    }

    private static object? ParseScalarValue(object? value, ScalarDataType dataType)
    {
        if (value is null)
        {
            return null;
        }

        // Handle already-typed CLR values (e.g. from NormalizeJsonElement) directly,
        // avoiding culture-sensitive ToString() round-trips.
        switch (dataType)
        {
            case ScalarDataType.Boolean when value is bool b:
                return b;
            case ScalarDataType.Date when value is DateOnly d:
                return d;
            case ScalarDataType.DateTime when value is DateTimeOffset dto:
                return dto;
            case ScalarDataType.Decimal when value is decimal dec:
                return dec;
            case ScalarDataType.Decimal when value is double dbl:
                return (decimal)dbl;
            case ScalarDataType.Integer when value is int i:
                return i;
            case ScalarDataType.Integer when value is decimal dec:
                return (int)dec;
            case ScalarDataType.String when value is string s:
                return s;
        }

        // Fall back to invariant string parsing for other representations.
        var str = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();

        if (str is null)
        {
            return null;
        }

        return dataType switch
        {
            ScalarDataType.Boolean => bool.TryParse(str, out var b) ? b : null,
            ScalarDataType.Date => DateOnly.TryParse(str, CultureInfo.InvariantCulture, out var d) ? d : (object?)null,
            ScalarDataType.DateTime => DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, out var dt) ? dt : null,
            ScalarDataType.Decimal => decimal.TryParse(str, CultureInfo.InvariantCulture, out var dec) ? dec : null,
            ScalarDataType.Integer => int.TryParse(str, CultureInfo.InvariantCulture, out var i) ? i : null,
            ScalarDataType.String => str,
            _ => null
        };
    }

    private static Dictionary<string, object>? ParseComplexValue(object? value, ComplexAttributeType type)
    {
        if (value is not IDictionary<string, object> dict)
        {
            return null;
        }

        var result = new Dictionary<string, object>();
        foreach (var (key, propRaw) in dict)
        {
            if (!type.TryGetProperty(key, out _, out var prop))
            {
                continue;
            }

            var parsed = ParseAttributeValue(propRaw, prop.Type);
            if (parsed is not null)
            {
                result[key] = parsed;
            }
        }
        return result;
    }

    private static List<object>? ParseListValue(object? value, ListAttributeType type)
    {
        if (value is not IList<object> list)
        {
            return null;
        }

        var result = new List<object>(list.Count);
        foreach (var element in list)
        {
            var parsed = ParseAttributeValue(element, type.ElementType);
            if (parsed is not null)
            {
                result.Add(parsed);
            }
        }
        return result;
    }

    /// <summary>
    ///     Converts a <see cref="JsonElement"/> to a CLR object suitable for further parsing.
    /// </summary>
    private static object? NormalizeJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetDecimal(out var d) ? (object)d : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => NormalizeJsonElement(p.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(NormalizeJsonElement)
                .ToList() as object,
            _ => element.GetRawText()
        };
}
