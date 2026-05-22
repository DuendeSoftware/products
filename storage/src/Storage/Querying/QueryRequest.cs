// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Pagination;

namespace Duende.Storage.Querying;

/// <summary>
/// Encapsulates filter, sort, and pagination parameters for a query operation.
/// </summary>
public sealed record QueryRequest
{
    /// <summary>A query request with no filter, sort, or range.</summary>
    public static QueryRequest Empty { get; } = new();

    /// <summary>Optional search/filter criteria.</summary>
    public FilterBy? Filter { get; init; }

    /// <summary>Optional sort criteria.</summary>
    public SortBy? Sort { get; init; }

    /// <summary>Optional pagination parameters.</summary>
    public DataRange? Range { get; init; }

    /// <summary>Creates an empty query request with no filter, sort, or range.</summary>
    public static QueryRequest Create() => Empty;

    /// <summary>Creates a query request with the specified filter.</summary>
    /// <param name="filter">The filter criteria.</param>
    public static QueryRequest Create(FilterBy filter) => new()
    {
        Filter = filter
    };

    /// <summary>Creates a query request with the specified sort.</summary>
    /// <param name="sort">The sort criteria.</param>
    public static QueryRequest Create(SortBy sort) => new()
    {
        Sort = sort
    };

    /// <summary>Creates a query request with the specified pagination range.</summary>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest Create(DataRange range) => new()
    {
        Range = range
    };

    /// <summary>Creates a query request with the specified filter and sort.</summary>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="sort">The sort criteria.</param>
    public static QueryRequest Create(FilterBy filter, SortBy sort) => new()
    {
        Filter = filter,
        Sort = sort
    };

    /// <summary>Creates a query request with the specified filter and pagination range.</summary>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest Create(FilterBy filter, DataRange range) => new()
    {
        Filter = filter,
        Range = range
    };

    /// <summary>Creates a query request with the specified sort and pagination range.</summary>
    /// <param name="sort">The sort criteria.</param>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest Create(SortBy sort, DataRange range) => new()
    {
        Sort = sort,
        Range = range
    };

    /// <summary>Creates a query request with the specified filter, sort, and pagination range.</summary>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="sort">The sort criteria.</param>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest Create(FilterBy filter, SortBy sort, DataRange range) => new()
    {
        Filter = filter,
        Sort = sort,
        Range = range
    };

    /// <summary>Creates a typed query request with no filter, sort, or range.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>()
        where TSort : struct, Enum => new();

    /// <summary>Creates a typed query request with the specified filter.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="filter">The typed filter value.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(TFilter? filter)
        where TSort : struct, Enum => new()
        {
            Filter = CreateFilter(filter)
        };

    /// <summary>Creates a typed query request with the specified filter.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="filter">The filter criteria.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(FilterBy<TFilter> filter)
        where TSort : struct, Enum => new()
        {
            Filter = filter
        };

    /// <summary>Creates a typed query request with the specified sort.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="sort">The sort criteria.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(SortBy.SortByField<TSort> sort)
        where TSort : struct, Enum => new()
        {
            Sort = sort
        };

    /// <summary>Creates a typed query request with the specified pagination range.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(DataRange range)
        where TSort : struct, Enum => new()
        {
            Range = range
        };

    /// <summary>Creates a typed query request with the specified filter and sort.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="filter">The typed filter value.</param>
    /// <param name="sort">The sort criteria.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(TFilter? filter, SortBy.SortByField<TSort>? sort)
        where TSort : struct, Enum => new()
        {
            Filter = CreateFilter(filter),
            Sort = sort
        };

    /// <summary>Creates a typed query request with the specified filter and sort.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="sort">The sort criteria.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(FilterBy<TFilter> filter, SortBy.SortByField<TSort> sort)
        where TSort : struct, Enum => new()
        {
            Filter = filter,
            Sort = sort
        };

    /// <summary>Creates a typed query request with the specified filter and pagination range.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="filter">The typed filter value.</param>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(TFilter? filter, DataRange? range)
        where TSort : struct, Enum => new()
        {
            Filter = CreateFilter(filter),
            Range = range
        };

    /// <summary>Creates a typed query request with the specified filter and pagination range.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(FilterBy<TFilter> filter, DataRange range)
        where TSort : struct, Enum => new()
        {
            Filter = filter,
            Range = range
        };

    /// <summary>Creates a typed query request with the specified sort and pagination range.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="sort">The sort criteria.</param>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(SortBy.SortByField<TSort>? sort, DataRange? range)
        where TSort : struct, Enum => new()
        {
            Sort = sort,
            Range = range
        };

    /// <summary>Creates a typed query request with the specified filter, sort, and pagination range.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="filter">The typed filter value.</param>
    /// <param name="sort">The sort criteria.</param>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(
        TFilter? filter,
        SortBy.SortByField<TSort>? sort,
        DataRange? range)
        where TSort : struct, Enum => new()
        {
            Filter = CreateFilter(filter),
            Sort = sort,
            Range = range
        };

    /// <summary>Creates a typed query request with the specified filter, sort, and pagination range.</summary>
    /// <typeparam name="TFilter">The typed filter type.</typeparam>
    /// <typeparam name="TSort">The typed sort field enum.</typeparam>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="sort">The sort criteria.</param>
    /// <param name="range">The pagination parameters.</param>
    public static QueryRequest<TFilter, TSort> Create<TFilter, TSort>(
        FilterBy<TFilter> filter,
        SortBy.SortByField<TSort> sort,
        DataRange range)
        where TSort : struct, Enum => new()
        {
            Filter = filter,
            Sort = sort,
            Range = range
        };

    private static FilterBy<TFilter>? CreateFilter<TFilter>(TFilter? filter) =>
        filter is null ? null : FilterBy.Filter(filter);
}

/// <summary>
/// Encapsulates typed filter, typed sort, and pagination parameters for a query operation.
/// </summary>
/// <typeparam name="TFilter">The typed filter type.</typeparam>
/// <typeparam name="TSort">The typed sort field enum.</typeparam>
public sealed record QueryRequest<TFilter, TSort> where TSort : struct, Enum
{
    /// <summary>Optional typed filter criteria.</summary>
    public FilterBy<TFilter>? Filter { get; init; }

    /// <summary>Optional typed sort criteria.</summary>
    public SortBy.SortByField<TSort>? Sort { get; init; }

    /// <summary>Optional pagination parameters.</summary>
    public DataRange? Range { get; init; }
}
