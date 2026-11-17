// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Specifies sort criteria for a query.
/// </summary>
public abstract record SortBy
{
    /// <summary>The sort direction.</summary>
    public SortDirection Direction { get; }

    private SortBy(SortDirection direction) => Direction = direction;

    /// <summary>
    /// Converts this sort criteria to its Duende.Storage equivalent. Implemented per subtype so
    /// <see cref="Internal.StorageMapping"/> doesn't need reflection or a type switch over the
    /// closed set of subtypes, which isn't possible for the open-generic <see cref="SortByField{TField}"/>.
    /// </summary>
    internal abstract StorageSortBy ToStorageSortBy();

    private protected static StorageSortDirection ToStorageDirection(SortDirection direction) => direction switch
    {
        SortDirection.Ascending => StorageSortDirection.Ascending,
        SortDirection.Descending => StorageSortDirection.Descending,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
    };

    /// <summary>
    /// Sort by a named attribute (for dynamic/EAV entities).
    /// </summary>
    public static SortByAttributeCode Attribute(Duende.IdentityServer.DataExtensions.AttributeCode code, SortDirection direction = SortDirection.Ascending) => new(code, direction);

    /// <summary>
    /// Sort by a typed sort field enum value (for fixed-schema entities).
    /// </summary>
    public static SortByField<TField> Field<TField>(TField field, SortDirection direction = SortDirection.Ascending)
        where TField : struct, Enum => new(field, direction);

    /// <summary>Sort by a named attribute code.</summary>
    public sealed record SortByAttributeCode : SortBy
    {
        /// <summary>The attribute code to sort by.</summary>
        public Duende.IdentityServer.DataExtensions.AttributeCode Code { get; }

        internal SortByAttributeCode(Duende.IdentityServer.DataExtensions.AttributeCode code, SortDirection direction) : base(direction) => Code = code;

        internal override StorageSortBy ToStorageSortBy() =>
            StorageSortBy.Attribute(Code.Value, ToStorageDirection(Direction));
    }

    /// <summary>Sort by a typed enum field.</summary>
    public sealed record SortByField<TField> : SortBy where TField : struct, Enum
    {
        /// <summary>The sort field.</summary>
        public TField Field { get; }

        internal SortByField(TField field, SortDirection direction) : base(direction) => Field = field;

        internal override StorageSortBy ToStorageSortBy() =>
            StorageSortBy.Field(Field, ToStorageDirection(Direction));
    }
}
