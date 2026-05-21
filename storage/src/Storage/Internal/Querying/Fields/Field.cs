// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying.Fields;

/// <summary>
/// Base class for all field types, representing a queryable field path.
/// </summary>
public abstract record Field
{
    /// <summary>
    /// The field path (e.g., "userName", "emails.value", "consoleProperties.id").
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The type of the field, indicating which typed column to read from (string_value, number_value, datetime_value, or boolean_value).
    /// This ensures QueryFields reads from the correct column instead of checking the first non-null value.
    /// </summary>
    public FieldType Type { get; }

    /// <summary>
    /// Indicates whether this field is multi-valued (i.e., stored with item_index >= 0).
    /// When true, queries omit the item_index = -1 condition so any array element can match.
    /// When false, queries include item_index = -1 to target scalar values only.
    /// </summary>
    public bool IsMultiValued { get; }

    protected Field(string path, FieldType type, bool isMultiValued = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Field path cannot be null or whitespace.", nameof(path));
        }

        Path = path.ToUpperInvariant();
        Type = type;
        IsMultiValued = isMultiValued;
    }

    /// <summary>
    /// Creates an expression that checks if this field has a value (is present).
    /// For scalar fields, checks that a non-null value exists.
    /// For multi-valued fields, checks that at least one array element exists.
    /// </summary>
    public IQueryFilterExpression Present() => new PresentExpression(this);
}
