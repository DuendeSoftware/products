// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using Duende.Storage.Internal.Pagination;

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// The unified result of a query operation. Works for all pagination modes.
/// Implements <see cref="IReadOnlyList{TItem}"/> so results can be iterated directly.
/// </summary>
/// <typeparam name="TItem">The type of items in the result.</typeparam>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record QueryResult<TItem> : IReadOnlyList<TItem>
{
    /// <summary>The items in the current page.</summary>
    public required IReadOnlyList<TItem> Items { get; init; }

    /// <summary>
    /// The continuation token to retrieve the previous page of results.
    /// In continuation-token mode this is always set, including on the first, last and empty pages
    /// (an empty page carries the position it was given). It is <c>null</c> in page/offset mode.
    /// The token carries its direction and sort fingerprint, and must be passed back unchanged with
    /// the same query and sort.
    /// </summary>
    public ContinuationToken? PreviousToken { get; init; }

    /// <summary>
    /// The continuation token to retrieve the next page of results.
    /// In continuation-token mode this is always set, including on the first, last and empty pages
    /// (an empty page carries the position it was given). It is <c>null</c> in page/offset mode.
    /// The token carries its direction and sort fingerprint, and must be passed back unchanged with
    /// the same query and sort. It is not <c>null</c> on the last page, so callers can poll for new
    /// records that sort after the current page.
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
    /// Indicates whether more rows exist after this page in the requested order.
    /// In continuation-token mode this is exact when the page was fetched forward (the query reads
    /// <c>pageSize + 1</c> rows to detect it). When the page was fetched backward, it isn't read from
    /// the database and is always <c>true</c>. It can be <c>true</c> even when no rows remain on that
    /// side (for example after deletes); following the token then returns an empty page.
    /// In page or offset mode, it is <c>true</c> when rows exist after this page, i.e.
    /// <c>skip + take &lt; TotalCount</c>.
    /// </summary>
    public bool HasMoreData { get; init; }

    /// <summary>
    /// Indicates whether more rows exist before this page in the requested order.
    /// In continuation-token mode this is exact when the page was fetched backward (the query reads
    /// <c>pageSize + 1</c> rows to detect it). When the page was fetched forward, it is inferred from
    /// the position, not read from the database: <c>false</c> only when the request started from the
    /// very first page (<see cref="Pagination.ContinuationToken.Beginning"/>), <c>true</c> otherwise.
    /// It can be <c>true</c> even when no rows actually remain on that side (for example after
    /// deletes); in that case, following the token returns an empty page.
    /// In page or offset mode, it is <c>true</c> when matching rows exist before this page, i.e.
    /// <c>skip &gt; 0</c> and <c>TotalCount &gt; 0</c>.
    /// </summary>
    public bool HasPreviousData { get; init; }

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
            HasPreviousData = HasPreviousData,
            NextToken = NextToken,
            PreviousToken = PreviousToken
        };
}
