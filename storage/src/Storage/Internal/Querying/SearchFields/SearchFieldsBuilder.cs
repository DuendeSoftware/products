// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.SearchFields;

/// <summary>
/// Builder for constructing <see cref="SearchFieldCollection"/> collections.
/// Provides helper methods for adding scalar values, nested values, and array item values.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed class SearchFieldsBuilder
{
    private readonly List<SearchFieldValue> _values = new();
    private readonly HashSet<SearchFieldKey> _keys = new();

    /// <summary>
    /// Adds a string-typed search field value.
    /// </summary>
    /// <param name="fieldPath">The field path (e.g., "userName", "consoleProperties.id")</param>
    /// <param name="value">The string value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, string value)
    {
        ValidateUniqueKey(fieldPath, null);
        _values.Add(SearchFieldValue.String(fieldPath, value));
        return this;
    }

    /// <summary>
    /// Adds a string-typed search field value for an array item.
    /// </summary>
    /// <param name="fieldPath">The field path (e.g., "emails.type")</param>
    /// <param name="itemIndex">The array item index (0-based)</param>
    /// <param name="value">The string value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, int itemIndex, string value)
    {
        ValidateUniqueKey(fieldPath, itemIndex);
        _values.Add(SearchFieldValue.String(fieldPath, value, itemIndex));
        return this;
    }

    /// <summary>
    /// Adds a decimal-typed search field value.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="value">The decimal value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, decimal value)
    {
        ValidateUniqueKey(fieldPath, null);
        _values.Add(SearchFieldValue.Number(fieldPath, value));
        return this;
    }

    /// <summary>
    /// Adds a decimal-typed search field value for an array item.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="itemIndex">The array item index (0-based)</param>
    /// <param name="value">The decimal value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, int itemIndex, decimal value)
    {
        ValidateUniqueKey(fieldPath, itemIndex);
        _values.Add(SearchFieldValue.Number(fieldPath, value, itemIndex));
        return this;
    }

    /// <summary>
    /// Adds a DateTime-typed search field value.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="value">The DateTime value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, DateTime value)
    {
        ValidateUniqueKey(fieldPath, null);
        _values.Add(SearchFieldValue.DateTime(fieldPath, value));
        return this;
    }

    /// <summary>
    /// Adds a DateTime-typed search field value for an array item.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="itemIndex">The array item index (0-based)</param>
    /// <param name="value">The DateTime value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, int itemIndex, DateTime value)
    {
        ValidateUniqueKey(fieldPath, itemIndex);
        _values.Add(SearchFieldValue.DateTime(fieldPath, value, itemIndex));
        return this;
    }

    /// <summary>
    /// Adds a DateTimeOffset-typed search field value.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="value">The DateTimeOffset value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, DateTimeOffset value)
    {
        ValidateUniqueKey(fieldPath, null);
        _values.Add(SearchFieldValue.DateTime(fieldPath, value));
        return this;
    }

    /// <summary>
    /// Adds a DateTimeOffset-typed search field value for an array item.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="itemIndex">The array item index (0-based)</param>
    /// <param name="value">The DateTimeOffset value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, int itemIndex, DateTimeOffset value)
    {
        ValidateUniqueKey(fieldPath, itemIndex);
        _values.Add(SearchFieldValue.DateTime(fieldPath, value, itemIndex));
        return this;
    }

    /// <summary>
    /// Adds a boolean-typed search field value.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="value">The boolean value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, bool value)
    {
        ValidateUniqueKey(fieldPath, null);
        _values.Add(SearchFieldValue.Boolean(fieldPath, value));
        return this;
    }

    /// <summary>
    /// Adds a boolean-typed search field value for an array item.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="itemIndex">The array item index (0-based)</param>
    /// <param name="value">The boolean value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, int itemIndex, bool value)
    {
        ValidateUniqueKey(fieldPath, itemIndex);
        _values.Add(SearchFieldValue.Boolean(fieldPath, value, itemIndex));
        return this;
    }

    /// <summary>
    /// Adds a Guid-typed search field value for a scalar (non-array) field.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="value">The Guid value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, Guid value)
    {
        ValidateUniqueKey(fieldPath, null);
        _values.Add(SearchFieldValue.Guid(fieldPath, value));
        return this;
    }

    /// <summary>
    /// Adds a Guid-typed search field value for an array item.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="itemIndex">The array item index (0-based)</param>
    /// <param name="value">The Guid value</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder Add(string fieldPath, int itemIndex, Guid value)
    {
        ValidateUniqueKey(fieldPath, itemIndex);
        _values.Add(SearchFieldValue.Guid(fieldPath, value, itemIndex));
        return this;
    }

    /// <summary>
    /// Adds an exact-match search field value (stores deterministic GUID hash) for a scalar (non-array) field.
    /// Use this with ExactMatchField for fast exact-match string lookups without storing the full string.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="value">The string value to hash</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder AddExactMatch(string fieldPath, string value)
    {
        ValidateUniqueKey(fieldPath, null);
        _values.Add(SearchFieldValue.ExactMatch(fieldPath, value));
        return this;
    }

    /// <summary>
    /// Adds an exact-match search field value (stores deterministic GUID hash) for an array item.
    /// Use this with ExactMatchField for fast exact-match string lookups without storing the full string.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="itemIndex">The array item index (0-based)</param>
    /// <param name="value">The string value to hash</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate key (fieldPath + itemIndex) is added</exception>
    public SearchFieldsBuilder AddExactMatch(string fieldPath, int itemIndex, string value)
    {
        ValidateUniqueKey(fieldPath, itemIndex);
        _values.Add(SearchFieldValue.ExactMatch(fieldPath, value, itemIndex));
        return this;
    }

    /// <summary>
    /// Reserved field paths that cannot be used as user-defined search field names.
    /// These map to system columns on the entities table.
    /// </summary>
    private static readonly HashSet<string> ReservedFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        SystemFields.Created, SystemFields.LastUpdated,
        SystemFields.CreatedAttributeName, SystemFields.LastUpdatedAttributeName
    };

    /// <summary>
    /// Validates that the key (fieldPath + itemIndex) has not already been added.
    /// Normalizes the fieldPath to upper-invariant to match the normalization applied by <see cref="SearchFieldValue"/>.
    /// </summary>
    /// <param name="fieldPath">The field path</param>
    /// <param name="itemIndex">The optional item index</param>
    private void ValidateUniqueKey(string fieldPath, int? itemIndex)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or whitespace.", nameof(fieldPath));
        }

        if (ReservedFieldNames.Contains(fieldPath))
        {
            throw new ArgumentException($"Field path '{fieldPath}' is reserved and cannot be used as a search field name.", nameof(fieldPath));
        }

        var normalizedFieldPath = fieldPath.ToUpperInvariant();
        var key = new SearchFieldKey(normalizedFieldPath, itemIndex);
        if (!_keys.Add(key))
        {
            var indexInfo = itemIndex.HasValue ? $" with item index {itemIndex.Value}" : " (scalar field)";
            throw new ArgumentException($"Duplicate search field key detected: field path '{fieldPath}'{indexInfo} has already been added.");
        }
    }

    /// <summary>
    /// Represents a unique key for a search field (fieldPath + itemIndex).
    /// </summary>
    private sealed record SearchFieldKey(string FieldPath, int? ItemIndex);

    /// <summary>
    /// Builds the immutable <see cref="SearchFieldCollection"/> collection.
    /// </summary>
    /// <returns>An immutable SearchFields collection containing all added values</returns>
    public SearchFieldCollection Build() => new SearchFieldCollection(_values.ToArray());
}
