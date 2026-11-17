// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Pagination;

/// <summary>
/// Represents cursor-based pagination using a continuation token and page size.
/// The token is checked against the entity type and sort of the query it's used with; an invalid
/// or mismatched token throws <see cref="InvalidContinuationTokenException"/>. Pass back the same
/// filter the token was produced with: the filter is not part of that check, so querying with a
/// different filter returns rows from the token's position without an error.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record ContinuationTokenDataRange
{
    /// <summary>
    /// Represents cursor-based pagination: a continuation token and page size.
    /// </summary>
    /// <param name="start">The continuation token to resume from, or <see cref="ContinuationToken.Beginning"/> for the first page.</param>
    /// <param name="size">The number of items per page.</param>
    public ContinuationTokenDataRange(ContinuationToken? start, DataRangeSize? size)
    {
        Start = start ?? ContinuationToken.Beginning;
        Size = size ?? DataRangeSize.Default;
    }

    /// <summary>
    /// Creates a <see cref="ContinuationTokenDataRange"/> starting from the first page.
    /// </summary>
    public static ContinuationTokenDataRange Beginning(DataRangeSize? size = null) => new(ContinuationToken.Beginning, size);

    /// <summary>
    /// The continuation token indicating where to resume. <see cref="ContinuationToken.Beginning"/>
    /// starts at the first page.
    /// </summary>
    public ContinuationToken Start { get; init; }

    /// <summary>The number of items per page.</summary>
    public DataRangeSize Size { get; init; }
}
