// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.SearchFields;

/// <summary>
/// Represents a single search field value that can be stored and queried.
/// This is a pure data structure for Entity-Attribute-Value (EAV) storage pattern.
/// </summary>
/// <remarks>
/// For string fields, both StringValue and GuidValue are set — StringValue holds the raw (uppercased)
/// string for LIKE queries, and GuidValue holds a deterministic hash for fast exact-match queries.
/// For GuidField and ExactMatchField, only GuidValue is set.
/// For Number, DateTime, and Boolean fields, only the respective typed value is set.
/// 
/// The ItemIndex is used to correlate fields within the same array item. For example:
/// - FieldPath = "emails.type", ItemIndex = 0, StringValue = "work"
/// - FieldPath = "emails.value", ItemIndex = 0, StringValue = "bob@work.com"
/// 
/// For scalar (non-array) fields, ItemIndex should be null.
/// </remarks>
public sealed record SearchFieldValue
{
    /// <summary>
    /// The path to the field (e.g., "userName", "consoleProperties.id", "emails.type").
    /// Supports dotted paths for nested properties.
    /// </summary>
    public string FieldPath { get; }

    /// <summary>
    /// A deterministic GUID computed from <see cref="FieldPath"/> (uppercased).
    /// Used to identify the field path column in storage without storing the raw string.
    /// </summary>
    public Guid FieldPathId { get; }

    /// <summary>
    /// Optional index to correlate fields within the same array item.
    /// Used for array properties to indicate which array element this value belongs to.
    /// Should be null for scalar fields.
    /// </summary>
    public int? ItemIndex { get; }

    /// <summary>
    /// String-typed value. Set only when the field type is string.
    /// </summary>
    public string? StringValue { get; }

    /// <summary>
    /// Number-typed value. Set only when the field type is numeric (int, long, decimal, etc.).
    /// </summary>
    public decimal? NumberValue { get; }

    /// <summary>
    /// DateTime-typed value. Set only when the field type is DateTime or DateTimeOffset.
    /// </summary>
    public DateTimeOffset? DateTimeValue { get; }

    /// <summary>
    /// Boolean-typed value. Set only when the field type is bool.
    /// </summary>
    public bool? BooleanValue { get; }

    /// <summary>
    /// Guid-typed value. Set for GuidField and ExactMatchField (only guid_value stored),
    /// and also set alongside StringValue for string fields (deterministic hash for fast exact-match queries).
    /// </summary>
    internal Guid? GuidValue { get; }

    private SearchFieldValue(
        string fieldPath,
        int? itemIndex,
        string? stringValue,
        decimal? numberValue,
        DateTimeOffset? dateTimeValue,
        bool? booleanValue,
        Guid? guidValue)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or whitespace.", nameof(fieldPath));
        }

        if (itemIndex.HasValue && itemIndex.Value < 0)
        {
            throw new ArgumentException("Item index must be non-negative.", nameof(itemIndex));
        }

        // Validate that at least one value is set and values are compatible.
        // number/datetime/boolean are "exclusive" — only one can be set, and none can coexist with string/guid.
        // string and guid may coexist (string fields store both for LIKE and hash-based equality).
        // guid may appear alone (GuidField / ExactMatchField).
        var exclusiveCount = 0;
        if (numberValue.HasValue) { exclusiveCount++; }
        if (dateTimeValue.HasValue) { exclusiveCount++; }
        if (booleanValue.HasValue) { exclusiveCount++; }

        if (exclusiveCount > 1)
        {
            throw new ArgumentException("Only one of number, datetime, or boolean values can be set at a time.");
        }

        var hasString = stringValue is not null;
        var hasGuid = guidValue.HasValue;
        var hasExclusive = exclusiveCount > 0;

        if (!hasString && !hasGuid && !hasExclusive)
        {
            throw new ArgumentException("At least one typed value must be set.");
        }

        if (hasExclusive && (hasString || hasGuid))
        {
            throw new ArgumentException("Number, datetime, and boolean values cannot coexist with string or guid values.");
        }

        FieldPath = fieldPath.ToUpperInvariant();
        FieldPathId = DeterministicGuidGenerator.Create(FieldPath);
        ItemIndex = itemIndex;
        StringValue = stringValue?.ToUpperInvariant();
        NumberValue = numberValue;
        DateTimeValue = dateTimeValue;
        BooleanValue = booleanValue;
        GuidValue = guidValue;
    }

    /// <summary>
    /// Creates a string-typed search field value for a scalar (non-array) field.
    /// Populates both StringValue (for LIKE queries) and GuidValue (for fast exact-match queries).
    /// </summary>
#pragma warning disable CA1720 // Identifier contains type name
    public static SearchFieldValue String(string fieldPath, string value)
#pragma warning restore CA1720
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("String value cannot be null or empty.", nameof(value));
        }

        var guidValue = DeterministicGuidGenerator.Create(value.ToUpperInvariant());
        return new SearchFieldValue(fieldPath, null, value, null, null, null, guidValue);
    }

    /// <summary>
    /// Creates a string-typed search field value for an array field.
    /// Populates both StringValue (for LIKE queries) and GuidValue (for fast exact-match queries).
    /// </summary>
#pragma warning disable CA1720 // Identifier contains type name
    public static SearchFieldValue String(string fieldPath, string value, int itemIndex)
#pragma warning restore CA1720
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("String value cannot be null or empty.", nameof(value));
        }

        var guidValue = DeterministicGuidGenerator.Create(value.ToUpperInvariant());
        return new SearchFieldValue(fieldPath, itemIndex, value, null, null, null, guidValue);
    }

    /// <summary>
    /// Creates a number-typed search field value for a scalar (non-array) field.
    /// </summary>
    public static SearchFieldValue Number(string fieldPath, decimal value) =>
        new(fieldPath, null, null, value, null, null, null);

    /// <summary>
    /// Creates a number-typed search field value for an array field.
    /// </summary>
    public static SearchFieldValue Number(string fieldPath, decimal value, int itemIndex) =>
        new(fieldPath, itemIndex, null, value, null, null, null);

    /// <summary>
    /// Creates a DateTime-typed search field value for a scalar (non-array) field.
    /// </summary>
    public static SearchFieldValue DateTime(string fieldPath, DateTimeOffset value) =>
        new(fieldPath, null, null, null, value, null, null);

    /// <summary>
    /// Creates a DateTime-typed search field value for an array field.
    /// </summary>
    public static SearchFieldValue DateTime(string fieldPath, DateTimeOffset value, int itemIndex) =>
        new(fieldPath, itemIndex, null, null, value, null, null);

    /// <summary>
    /// Creates a boolean-typed search field value for a scalar (non-array) field.
    /// </summary>
    public static SearchFieldValue Boolean(string fieldPath, bool value) =>
        new(fieldPath, null, null, null, null, value, null);

    /// <summary>
    /// Creates a boolean-typed search field value for an array field.
    /// </summary>
    public static SearchFieldValue Boolean(string fieldPath, bool value, int itemIndex) =>
        new(fieldPath, itemIndex, null, null, null, value, null);

    /// <summary>
    /// Creates a Guid-typed search field value for a scalar (non-array) field.
    /// Only guid_value is stored — use for GuidField.
    /// </summary>
#pragma warning disable CA1720 // Identifier contains type name
    public static SearchFieldValue Guid(string fieldPath, Guid value) =>
#pragma warning restore CA1720
        new(fieldPath, null, null, null, null, null, value);

    /// <summary>
    /// Creates a Guid-typed search field value for an array field.
    /// Only guid_value is stored — use for GuidField.
    /// </summary>
#pragma warning disable CA1720 // Identifier contains type name
    public static SearchFieldValue Guid(string fieldPath, Guid value, int itemIndex) =>
#pragma warning restore CA1720
        new(fieldPath, itemIndex, null, null, null, null, value);

    /// <summary>
    /// Creates an exact-match search field value that stores a deterministic GUID hash.
    /// No string_value is stored — only the hash in guid_value. Use for ExactMatchField.
    /// </summary>
    internal static SearchFieldValue ExactMatch(string fieldPath, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("String value cannot be null or empty.", nameof(value));
        }

        var hash = DeterministicGuidGenerator.Create(value.ToUpperInvariant());
        return new SearchFieldValue(fieldPath, null, null, null, null, null, hash);
    }

    /// <summary>
    /// Creates an exact-match search field value for an array field.
    /// No string_value is stored — only the hash in guid_value. Use for ExactMatchField.
    /// </summary>
    internal static SearchFieldValue ExactMatch(string fieldPath, string value, int itemIndex)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("String value cannot be null or empty.", nameof(value));
        }

        var hash = DeterministicGuidGenerator.Create(value.ToUpperInvariant());
        return new SearchFieldValue(fieldPath, itemIndex, null, null, null, null, hash);
    }
}
