// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Pagination;

/// <summary>
/// Represents page-number-based pagination: a 1-based page number and page size.
/// </summary>
public sealed record PagedDataRange
{
    /// <summary>
    /// Represents page-number-based pagination: a 1-based page number and page size.
    /// </summary>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    public PagedDataRange(PageNumber? page, DataRangeSize? pageSize)
    {
        Page = page ?? 1;
        PageSize = pageSize ?? DataRangeSize.Default;
    }

    /// <summary>
    /// Creates a <see cref="PagedDataRange"/> starting from the first page.
    /// </summary>
    public static PagedDataRange First(DataRangeSize pageSize) => new((PageNumber)1, pageSize);

    /// <summary>
    /// Creates a <see cref="PagedDataRange"/> starting from the first page.
    /// </summary>
    public static PagedDataRange First() => new((PageNumber)1, DataRangeSize.Default);

    /// <summary>The 1-based page number.</summary>
    public PageNumber Page { get; init; }

    /// <summary>The number of items per page.</summary>
    public DataRangeSize PageSize { get; init; }
}
