// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Internal.Pagination;

namespace Duende.Storage.Internal.Querying.Cursors;

/// <summary>
/// The result of assembling one cursor page: the fetched rows reordered into the requested order,
/// the Next/Previous continuation tokens, and the before/after flags.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <typeparam name="TRow">The row type returned by the provider's query.</typeparam>
/// <param name="ItemsInRequestedOrder">The page's rows, in the order the caller asked for (never reversed for a backward page).</param>
/// <param name="Next">A token that continues forward from the end of this page.</param>
/// <param name="Previous">A token that continues backward from the start of this page.</param>
/// <param name="HasMoreData">Whether more rows exist after this page in the requested order, independent of traversal direction.</param>
/// <param name="HasPreviousData">Whether more rows exist before this page in the requested order, independent of traversal direction.</param>
internal sealed record CursorPageResult<TRow>(
    IReadOnlyList<TRow> ItemsInRequestedOrder,
    ContinuationToken Next,
    ContinuationToken Previous,
    bool HasMoreData,
    bool HasPreviousData);

/// <summary>
/// Turns the rows fetched for one traversal direction into a page in the requested order, with
/// Next/Previous tokens and before/after flags. Shared by every provider's cursor query path.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal static class CursorPageAssembler
{
    /// <summary>
    /// Assembles a cursor page.
    /// </summary>
    /// <typeparam name="TRow">The row type returned by the provider's query.</typeparam>
    /// <param name="fetched">
    /// The rows fetched in traversal order, up to <paramref name="pageSize"/> + 1 (the extra row, if present,
    /// signals that more rows exist on the same side the query traversed).
    /// </param>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="traversal">The direction the query traversed in.</param>
    /// <param name="requestPosition">The cursor position the query started from, used to build tokens for an empty page.</param>
    /// <param name="entityType">The entity type the query ran against.</param>
    /// <param name="sort">The sort parameter the query ran with, or <see cref="SortParameter.Empty"/> when unsorted.</param>
    /// <param name="key">Extracts a row's entity id and raw sort value.</param>
    /// <remarks>
    /// The opposite-side flag (the one not covered by the fetched "+1" row) is inferred rather than
    /// probed: it is <see langword="false"/> only for a forward traversal starting from
    /// <see cref="CursorPositionKind.Start"/>, and <see langword="true"/> otherwise (including a
    /// backward traversal starting from <see cref="CursorPositionKind.Start"/>). This is exact for the
    /// side the query traversed and conservative for the other side: after deletes, the inferred
    /// <see langword="true"/> can turn out to have no rows, in which case following that token returns
    /// an empty page.
    /// </remarks>
    public static CursorPageResult<TRow> Assemble<TRow>(
        IReadOnlyList<TRow> fetched,
        int pageSize,
        CursorDirection traversal,
        CursorPosition requestPosition,
        EntityType entityType,
        SortParameter sort,
        Func<TRow, (Guid Id, object? SortValue)> key)
    {
        var hasExtra = fetched.Count > pageSize;
        var page = hasExtra ? fetched.Take(pageSize).ToList() : fetched.ToList();

        var itemsInRequestedOrder = traversal == CursorDirection.Backward
            ? Enumerable.Reverse(page).ToList()
            : page;

        var oppositeSideExists = !(traversal == CursorDirection.Forward && requestPosition.Kind == CursorPositionKind.Start);

        bool hasMoreData;
        bool hasPreviousData;
        if (traversal == CursorDirection.Forward)
        {
            hasMoreData = hasExtra;
            hasPreviousData = oppositeSideExists;
        }
        else
        {
            hasPreviousData = hasExtra;
            hasMoreData = oppositeSideExists;
        }

        ContinuationToken next;
        ContinuationToken previous;
        if (itemsInRequestedOrder.Count == 0)
        {
            next = CursorToken.ForPosition(CursorDirection.Forward, requestPosition, entityType, sort).Encode();
            previous = CursorToken.ForPosition(CursorDirection.Backward, requestPosition, entityType, sort).Encode();
        }
        else
        {
            var (lastId, lastSortValue) = key(itemsInRequestedOrder[^1]);
            var (firstId, firstSortValue) = key(itemsInRequestedOrder[0]);
            next = CursorToken.ForPage(CursorDirection.Forward, entityType, sort, lastId, lastSortValue).Encode();
            previous = CursorToken.ForPage(CursorDirection.Backward, entityType, sort, firstId, firstSortValue).Encode();
        }

        return new CursorPageResult<TRow>(itemsInRequestedOrder, next, previous, hasMoreData, hasPreviousData);
    }
}
