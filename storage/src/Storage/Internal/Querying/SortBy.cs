// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Specifies sort criteria for a query.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public abstract record SortBy
{
    /// <summary>The sort direction.</summary>
    public SortDirection Direction { get; }

    private SortBy(SortDirection direction) => Direction = direction;

    /// <summary>
    /// Sort by a named search-field attribute code (string-based, for product-defined attribute sets).
    /// </summary>
    public static SortByAttributeCode Attribute(string code, SortDirection direction = SortDirection.Ascending) => new(code, direction);

    /// <summary>
    /// Sort by a typed sort field enum value (for fixed-schema entities).
    /// </summary>
    public static SortByField<TField> Field<TField>(TField field, SortDirection direction = SortDirection.Ascending)
        where TField : struct, Enum => new(field, direction);

    /// <summary>Sort by a named attribute code.</summary>
    public sealed record SortByAttributeCode : SortBy
    {
        /// <summary>The attribute code to sort by.</summary>
        public string Code { get; }

        internal SortByAttributeCode(string code, SortDirection direction) : base(direction)
        {
            ArgumentException.ThrowIfNullOrEmpty(code);
            Code = code;
        }
    }

    /// <summary>Sort by a typed enum field.</summary>
    public sealed record SortByField<TField> : SortBy where TField : struct, Enum
    {
        /// <summary>The sort field.</summary>
        public TField Field { get; }

        internal SortByField(TField field, SortDirection direction) : base(direction) => Field = field;
    }
}
