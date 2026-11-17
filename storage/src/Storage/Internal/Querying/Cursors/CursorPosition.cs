// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Cursors;

/// <summary>
/// Whether a <see cref="CursorPosition"/> marks the start of the traversal or a specific key.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal enum CursorPositionKind
{
    /// <summary>
    /// The position before the first row (forward) or after the last row (backward) of the requested order.
    /// Carries no entity id or sort value.
    /// </summary>
    Start = 0,

    /// <summary>
    /// The position of a specific row, identified by its entity id and sort value.
    /// </summary>
    Key = 1
}

/// <summary>
/// A decoded cursor position: either the start of the traversal, or a specific row's entity id and sort value.
/// </summary>
/// <param name="Kind">Whether this is the start of the traversal or a specific key.</param>
/// <param name="Id">The entity id of the referenced row. <see cref="Guid.Empty"/> when <paramref name="Kind"/> is <see cref="CursorPositionKind.Start"/>.</param>
/// <param name="ValueKind">The kind of the sort value carried alongside <paramref name="Id"/>.</param>
/// <param name="Value">
/// The sort value, typed per <paramref name="ValueKind"/>: <see cref="string"/>, <see cref="decimal"/>, <see cref="DateTimeOffset"/> (always UTC), <see cref="bool"/>, or <see cref="Guid"/>.
/// Null when <paramref name="ValueKind"/> is <see cref="CursorValueKind.None"/> or <see cref="CursorValueKind.Null"/>.
/// </param>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal sealed record CursorPosition(CursorPositionKind Kind, Guid Id, CursorValueKind ValueKind, object? Value)
{
    /// <summary>
    /// The position before the first row (forward) or after the last row (backward) of the requested order.
    /// </summary>
    public static readonly CursorPosition Start = new(CursorPositionKind.Start, Guid.Empty, CursorValueKind.None, null);

    /// <summary>
    /// Creates a key position for an unsorted query: only the entity id participates in ordering.
    /// </summary>
    public static CursorPosition ForUnsortedKey(Guid id) => new(CursorPositionKind.Key, id, CursorValueKind.None, null);

    /// <summary>
    /// Creates a key position for a sorted query from a row's entity id and sort value.
    /// </summary>
    /// <param name="id">The row's entity id.</param>
    /// <param name="valueKind">The kind of <paramref name="value"/>.</param>
    /// <param name="value">The row's sort value, or null when <paramref name="valueKind"/> is <see cref="CursorValueKind.Null"/>.</param>
    public static CursorPosition ForKey(Guid id, CursorValueKind valueKind, object? value) => new(CursorPositionKind.Key, id, valueKind, value);
}
