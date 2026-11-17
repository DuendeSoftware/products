// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.Storage.Internal.Pagination;

namespace Duende.IdentityServer.Internal;

/// <summary>
/// Converts between the public query/result clones in <see cref="Duende.IdentityServer.Models"/>
/// and the preview Duende.Storage types they're built from. Request types convert one way, from the
/// model to Storage. Result types convert the other way, from Storage to the model.
/// </summary>
internal static class StorageMapping
{
    // ----- Requests: Model -> Storage -----

    internal static StorageQueryRequest ToStorage(this Models.QueryRequest request) => StorageQueryRequest.Create() with
    {
        Filter = request.Filter?.ToStorage(),
        Sort = request.Sort?.ToStorage(),
        Range = request.Range.ToStorage()
    };

    internal static StorageQuerying.QueryRequest<TFilter, TSort> ToStorage<TFilter, TSort>(
        this Models.QueryRequest<TFilter, TSort> request)
        where TSort : struct, Enum => new()
        {
            Filter = request.Filter?.ToStorage(),
            Sort = request.Sort?.ToStorageField(),
            Range = request.Range.ToStorage()
        };

    internal static StorageFilterBy? ToStorage(this Models.FilterBy? filter) => filter switch
    {
        null => null,
        { SearchExpressionValue: { } expr } => StorageFilterBy.FromSearchExpression(expr.ToStorage()!),
        _ => null
    };

    internal static StorageQuerying.FilterBy<T>? ToStorage<T>(this Models.FilterBy<T>? filter) => filter switch
    {
        null => null,
        { SearchExpressionValue: { } expr } => expr.ToStorage()!,
        { FilterValue: { } value } => StorageFilterBy.Filter(value),
        _ => null
    };

    internal static StorageSearchExpression? ToStorage(this Models.SearchExpression expr) =>
        expr is null ? null : StorageSearchExpression.Create(expr.Value);

    internal static StorageSortBy ToStorage(this Models.SortBy sort) => sort.ToStorageSortBy();

    internal static Duende.Storage.Internal.Querying.SortBy.SortByField<TSort> ToStorageField<TSort>(this Models.SortBy.SortByField<TSort> sort)
        where TSort : struct, Enum => StorageSortBy.Field(sort.Field, sort.Direction.ToStorageDirection());

    internal static StorageSortDirection ToStorageDirection(this Models.SortDirection direction) => direction switch
    {
        Models.SortDirection.Ascending => StorageSortDirection.Ascending,
        Models.SortDirection.Descending => StorageSortDirection.Descending,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
    };

    internal static StorageDataRange? ToStorage(this Models.DataRange? range) => range switch
    {
        null => null,
        { PageValue: { } p } => StorageDataRange.FromPage(new StoragePagination.PagedDataRange(
            (PageNumber)p.Page.Value, (DataRangeSize)p.PageSize.Value)),
        { OffsetValue: { } o } => StorageDataRange.FromOffset(new StoragePagination.OffsetDataRange(
            (OffsetSkip)o.Skip.Value, (DataRangeSize)o.Take.Value)),
        { TokenValue: { } t } => StorageDataRange.FromContinuationToken(new StoragePagination.ContinuationTokenDataRange(
            (ContinuationToken)t.Start.Value, (DataRangeSize)t.Size.Value)),
        _ => StorageDataRange.FromContinuationToken((ContinuationToken?)null, null)
    };

    /// <summary>
    /// Runs a Storage query, translating a <see cref="Duende.Storage.Internal.Pagination.InvalidContinuationTokenException"/>
    /// thrown while executing the query (for example because the token was tampered with, or was issued for a
    /// different sort) into the corresponding <see cref="Models.InvalidContinuationTokenException"/>.
    /// </summary>
    internal static async Task<T> ExecuteQueryAsync<T>(Func<Task<T>> query)
    {
        try
        {
            return await query();
        }
        catch (Duende.Storage.Internal.Pagination.InvalidContinuationTokenException ex)
        {
            throw new Models.InvalidContinuationTokenException((Models.InvalidContinuationTokenReason)(int)ex.Reason, ex.Message, ex);
        }
    }

    internal static StorageDataVersion? ToStorage(this Models.DataVersion? version) =>
        version is null ? null : (StorageDataVersion)version.Value;

    // ----- Results: Storage -> Model -----

    internal static Models.QueryResult<T> ToModel<T>(this StorageQuerying.QueryResult<T> result, Models.DataRange? range) => new()
    {
        Results = result.Items,
        ResultsToken = result.NextToken?.Value!,
        PreviousToken = result.PreviousToken?.Value,
        HasNextResults = result.HasMoreData,
        HasPrevResults = result.HasPreviousData,
        TotalCount = result.TotalCount,
        TotalPages = result.TotalPages
    };

    internal static Models.DataRange ToModel(this StorageDataRange range) => range switch
    {
        { PageValue: { } p } => Models.DataRange.FromPage(p.Page.Value, p.PageSize.Value),
        { OffsetValue: { } o } => Models.DataRange.FromOffset(o.Skip.Value, o.Take.Value),
        { TokenValue: { } t } => Models.DataRange.FromContinuationToken(t.Start.Value, t.Size.Value),
        _ => Models.DataRange.FromContinuationToken((Models.ContinuationToken?)null)
    };

    internal static Models.DataVersion ToModel(this StorageDataVersion version) => (Models.DataVersion)version.Value;

    internal static Models.SaveResult<TId> ToModel<TId>(this StorageResults.SaveResult<TId> result) where TId : notnull => new()
    {
        IsSuccess = result.IsSuccess,
        Id = result.Id,
        Version = result.Version?.ToModel(),
        Errors = result.Errors?.Select(ToModel).ToArray()
    };

    internal static Models.GetResult<T> ToModel<T>(this StorageResults.GetResult<T> result) => new()
    {
        Found = result.Found,
        Item = result.Item,
        Version = result.Version?.ToModel()
    };

    internal static Models.StorageError ToModel(this StorageErrorResult error) =>
        new(error.Code, error.Message, error.PropertyNames);
}
