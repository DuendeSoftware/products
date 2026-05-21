// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Querying;

namespace Duende.Storage.Internal.Querying.Sorting;

/// <summary>
/// Specifies a field and direction for sorting query results.
/// </summary>
public sealed record SortParameter
{
    /// <summary>
    /// Represents an empty sort parameter indicating no sorting should be applied.
    /// </summary>
    public static readonly SortParameter Empty = new();

    /// <summary>
    /// Gets a value indicating whether this is an empty sort parameter.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Field))]
    public bool IsEmpty => Field is null;

    /// <summary>
    /// The field to sort by.
    /// </summary>
    public Field? Field { get; }

    /// <summary>
    /// The direction to sort in.
    /// </summary>
    public SortDirection Direction { get; }

    /// <summary>
    /// Private constructor for creating empty sort parameter.
    /// </summary>
    private SortParameter()
    {
        Field = null;
        Direction = SortDirection.Ascending;
    }

    /// <summary>
    /// Creates a new sort parameter.
    /// </summary>
    /// <param name="field">The field to sort by.</param>
    /// <param name="direction">The direction to sort in. Defaults to Ascending.</param>
    public SortParameter(Field field, SortDirection direction = SortDirection.Ascending)
    {
        ArgumentNullException.ThrowIfNull(field);
        Field = field;
        Direction = direction;
    }
}
