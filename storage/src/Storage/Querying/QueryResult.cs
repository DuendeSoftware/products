// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using Duende.Storage.Pagination;

namespace Duende.Storage.Querying;

/// <summary>
/// The unified result of a query operation. Works for all pagination modes.
/// Implements <see cref="IReadOnlyList{TItem}"/> so results can be iterated directly.
/// </summary>
/// <typeparam name="TItem">The type of items in the result.</typeparam>
public sealed record QueryResult<TItem> : IReadOnlyList<TItem>
{
    /// <summary>The items in the current page.</summary>
    public required IReadOnlyList<TItem> Items { get; init; }

    /// <summary>
    /// The continuation token to retrieve the previous page of results.
    /// </summary>
    public ContinuationToken? PreviousToken { get; init; }

    /// <summary>
    /// The continuation token to retrieve the next page of results.
    /// </summary>
    public ContinuationToken? NextToken { get; init; }

    /// <summary>
    /// If available, the total number of items. This may be null if the total count
    /// is not known or expensive to compute.
    /// </summary>
    public int? TotalCount { get; init; }

    /// <summary>
    /// If available, the total number of pages. This may be null if the total count
    /// is not known or expensive to compute.
    /// </summary>
    public int? TotalPages { get; init; }

    /// <summary>
    /// Indicates whether there is more data available.
    /// </summary>
    public bool HasMoreData { get; init; }

    /// <inheritdoc />
    public IEnumerator<TItem> GetEnumerator() => Items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Items).GetEnumerator();

    /// <inheritdoc />
    int IReadOnlyCollection<TItem>.Count => Items.Count;

    /// <inheritdoc />
    public TItem this[int index] => Items[index];

    /// <summary>
    /// Converts the items in this result to a different type using the specified conversion function.
    /// </summary>
    /// <typeparam name="TConverted">The target item type.</typeparam>
    /// <param name="convert">The function to convert each item.</param>
    /// <returns>A new <see cref="QueryResult{TConverted}"/> with converted items and the same pagination metadata.</returns>
    public QueryResult<TConverted> ConvertTo<TConverted>(Func<TItem, TConverted> convert) =>
        new()
        {
            Items = Items.Select(convert).ToArray(),
            TotalPages = TotalPages,
            TotalCount = TotalCount,
            HasMoreData = HasMoreData,
            NextToken = NextToken,
            PreviousToken = PreviousToken
        };
}
