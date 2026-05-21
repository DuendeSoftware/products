// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Pagination;

#pragma warning disable CA2225 // Allow implicit conversions from related types for ease of use.

/// <summary>
/// Specifies where to begin fetching results in a query.
/// Can be created from a <see cref="FromContinuationToken(Duende.Storage.Pagination.ContinuationToken?,Duende.Storage.Pagination.DataRangeSize?)"/>,
/// a <see cref="PagedDataRange"/>, or an <see cref="FromOffset(Duende.Storage.Pagination.OffsetDataRange)"/>.
/// </summary>
public sealed class DataRange
{
    private DataRange()
    {
    }

    public ContinuationTokenDataRange? TokenValue { get; private init; }

    public OffsetDataRange? OffsetValue { get; private init; }

    public PagedDataRange? PageValue { get; private init; }

    /// <summary>
    /// Implicitly converts a <see cref="FromContinuationToken(Duende.Storage.Pagination.ContinuationToken?,Duende.Storage.Pagination.DataRangeSize?)"/> to a <see cref="DataRange"/>.
    /// </summary>
    public static implicit operator DataRange(ContinuationTokenDataRange? token) => new()
    {
        TokenValue = token
    };

    /// <summary>
    /// Implicitly converts a <see cref="PagedDataRange"/> to a <see cref="DataRange"/>.
    /// </summary>
    public static implicit operator DataRange(PagedDataRange page) => new()
    {
        PageValue = page
    };

    /// <summary>
    /// Implicitly converts an <see cref="FromOffset(Duende.Storage.Pagination.OffsetDataRange)"/> to a <see cref="DataRange"/>.
    /// </summary>
    public static implicit operator DataRange(OffsetDataRange offsetDataRange) => new()
    {
        OffsetValue = offsetDataRange
    };


    /// <summary>
    /// Creates a <see cref="DataRange"/> from page-number-based pagination.
    /// </summary>
    public static DataRange FromPage(PageNumber page) => new()
    {
        PageValue = new PagedDataRange(page, null)
    };


    /// <summary>
    /// Creates a <see cref="DataRange"/> from page-number-based pagination.
    /// </summary>
    public static DataRange FromPage(PageNumber? page, DataRangeSize? size) => new()
    {
        PageValue = new PagedDataRange(page, size)
    };

    /// <summary>
    /// Creates a <see cref="DataRange"/> from page-number-based pagination.
    /// </summary>
    public static DataRange FromPage(PagedDataRange page) => new()
    {
        PageValue = page
    };

    public static DataRange FromOffset(OffsetSkip? skip) => new()
    {
        OffsetValue = new OffsetDataRange(skip, null)
    };

    public static DataRange FromOffset(OffsetSkip? skip, DataRangeSize? size) => new()
    {
        OffsetValue = new OffsetDataRange(skip, size)
    };

    /// <summary>
    /// Creates a <see cref="DataRange"/> from offset-based pagination.
    /// </summary>
    public static DataRange FromOffset(OffsetDataRange offsetDataRange) => new()
    {
        OffsetValue = offsetDataRange
    };

    /// <summary>
    /// Creates a <see cref="DataRange"/> from a continuation token.
    /// </summary>
    public static DataRange FromContinuationToken(ContinuationTokenDataRange? token) => new()
    {
        TokenValue = token
    };

    /// <summary>
    /// Creates a <see cref="DataRange"/> from a continuation token.
    /// </summary>
    public static DataRange FromContinuationToken(ContinuationToken? token) => new()
    {
        TokenValue = new ContinuationTokenDataRange(token, null)
    };

    /// <summary>
    /// Creates a <see cref="DataRange"/> from a continuation token.
    /// </summary>
    public static DataRange FromContinuationToken(ContinuationToken? token, DataRangeSize? size) => new()
    {
        TokenValue = new ContinuationTokenDataRange(token, size)
    };
}
