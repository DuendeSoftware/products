// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Cursors;

/// <summary>
/// Builds the ORDER BY and strictly-after seek predicate for keyset (cursor) pagination over a
/// single optional sort field plus a unique id tie-breaker.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
///
/// The builder only emits SQL text. It never binds parameter values; callers pass in the parameter
/// names (<c>@last_sort_value</c>/<c>@last_id</c> or Oracle's <c>:last_sort_value</c>/<c>:last_id</c>)
/// and bind the actual values themselves, since binding is dialect- and type-specific.
///
/// Forward traversal orders nulls last, the sort column in its requested direction, then the id
/// ascending. Backward traversal is the exact mirror: nulls first, the sort column reversed, then the
/// id descending. An unsorted query (<paramref name="sortColumn"/> is null) orders by the id alone,
/// ascending forward and descending backward.
/// </remarks>
internal sealed class KeysetSqlBuilder(ISqlDialect dialect, string? sortColumn, SortDirection sortDirection, string idColumn = "v.entity_id")
{
    /// <summary>
    /// Returns the ORDER BY clause (without the <c>ORDER BY</c> keyword) for the given traversal direction.
    /// </summary>
    public string OrderBy(CursorDirection traversal)
    {
        var idOrder = traversal == CursorDirection.Forward ? "ASC" : "DESC";

        if (sortColumn is null)
        {
            return $"{idColumn} {idOrder}";
        }

        var effectiveDirection = EffectiveDirection(traversal);
        var directionText = effectiveDirection == SortDirection.Ascending ? "ASC" : "DESC";
        var nullsLast = traversal == CursorDirection.Forward;

        if (dialect.SupportsNullsOrdering)
        {
            var nullsText = nullsLast ? "NULLS LAST" : "NULLS FIRST";
            return $"{sortColumn} {directionText} {nullsText}, {idColumn} {idOrder}";
        }

        var nullsCaseOrder = nullsLast ? "ASC" : "DESC";
        return $"CASE WHEN {sortColumn} IS NULL THEN 1 ELSE 0 END {nullsCaseOrder}, {sortColumn} {directionText}, {idColumn} {idOrder}";
    }

    /// <summary>
    /// Returns the predicate that selects rows strictly after <paramref name="position"/> in the
    /// given traversal order. Returns an empty string for the forward start position (no predicate
    /// needed) and <c>1=0</c> for the backward start position (nothing is "after" the end).
    /// </summary>
    /// <param name="traversal">The traversal direction the predicate applies to.</param>
    /// <param name="position">The decoded cursor position.</param>
    /// <param name="valueParam">The parameter name bound to the cursor's sort value, e.g. <c>@last_sort_value</c>.</param>
    /// <param name="idParam">The parameter name bound to the cursor's entity id, e.g. <c>@last_id</c>.</param>
    public string StrictlyAfter(CursorDirection traversal, CursorPosition position, string valueParam, string idParam)
    {
        if (position.Kind == CursorPositionKind.Start)
        {
            return traversal == CursorDirection.Forward ? "" : "1=0";
        }

        var idOp = traversal == CursorDirection.Forward ? ">" : "<";

        if (sortColumn is null)
        {
            return $"{idColumn} {idOp} {idParam}";
        }

        var op = EffectiveDirection(traversal) == SortDirection.Ascending ? ">" : "<";
        var nullsLast = traversal == CursorDirection.Forward;

        if (position.ValueKind == CursorValueKind.Null)
        {
            return nullsLast
                ? $"({sortColumn} IS NULL AND {idColumn} {idOp} {idParam})"
                : $"({sortColumn} IS NOT NULL OR ({sortColumn} IS NULL AND {idColumn} {idOp} {idParam}))";
        }

        return nullsLast
            ? $"({sortColumn} {op} {valueParam} OR ({sortColumn} = {valueParam} AND {idColumn} {idOp} {idParam}) OR {sortColumn} IS NULL)"
            : $"({sortColumn} {op} {valueParam} OR ({sortColumn} = {valueParam} AND {idColumn} {idOp} {idParam}))";
    }

    private SortDirection EffectiveDirection(CursorDirection traversal) =>
        traversal == CursorDirection.Forward ? sortDirection : Opposite(sortDirection);

    private static SortDirection Opposite(SortDirection direction) =>
        direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
}
