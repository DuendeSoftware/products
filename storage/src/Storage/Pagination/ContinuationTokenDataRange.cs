// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Pagination;

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

    /// <summary>The continuation token indicating where to resume.</summary>
    public ContinuationToken Start { get; init; }

    /// <summary>The number of items per page.</summary>
    public DataRangeSize Size { get; init; }
}
