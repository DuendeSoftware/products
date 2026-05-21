// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Pagination;

/// <summary>
/// Represents offset-based pagination: a 0-based row offset and batch size.
/// </summary>
public sealed record OffsetDataRange
{
    /// <summary>
    /// Represents offset-based pagination: a 0-based row offset and batch size.
    /// </summary>
    /// <param name="skip">The 0-based row offset.</param>
    /// <param name="take">The number of items to return.</param>
    public OffsetDataRange(OffsetSkip? skip, DataRangeSize? take)
    {
        Skip = skip ?? 0;
        Take = take ?? DataRangeSize.Default;
    }

    /// <summary>
    /// Creates an <see cref="OffsetDataRange"/> starting from the first row.
    /// </summary>
    public static OffsetDataRange First(DataRangeSize? pageSize = null) => new(0L, pageSize);

    /// <summary>The 0-based row offset.</summary>
    public OffsetSkip Skip { get; init; }

    /// <summary>The number of items to return.</summary>
    public DataRangeSize Take { get; init; }
}
