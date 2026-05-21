// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Querying;

/// <summary>
/// Specifies search/filter criteria for a query.
/// Accepts a <see cref="SearchExpression"/> via implicit conversion.
/// Use <see cref="FilterBy{T}"/> for typed filter support.
/// </summary>
public sealed class FilterBy
{
    private FilterBy()
    {
    }

    public SearchExpression? SearchExpressionValue { get; init; }

    /// <summary>
    /// Implicitly converts a <see cref="SearchExpression"/> to a <see cref="FilterBy"/>.
    /// </summary>
    public static implicit operator FilterBy(SearchExpression searchExpression) => FromSearchExpression(searchExpression);

    /// <summary>
    /// Creates a <see cref="FilterBy"/> from a <see cref="SearchExpression"/>.
    /// </summary>
    public static FilterBy FromSearchExpression(SearchExpression searchExpression) => new()
    {
        SearchExpressionValue = searchExpression
    };

    /// <summary>
    /// Creates a <see cref="FilterBy{T}"/> from a typed filter object.
    /// </summary>
    public static FilterBy<T> Filter<T>(T filterValue) => FilterBy<T>.FromFilter(filterValue);
}

/// <summary>
/// Specifies search/filter criteria for a query, supporting both
/// <see cref="SearchExpression"/> and a typed filter <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The typed filter type (e.g., <c>ClientFilter</c>, <c>RoleFilter</c>).</typeparam>
#pragma warning disable CA1000 // Do not declare static members on generic types — intentional: implicit operators require static members
public sealed class FilterBy<T>
{
    private FilterBy()
    {
    }

    internal SearchExpression? SearchExpressionValue { get; init; }

    public T? FilterValue { get; init; }

    /// <summary>
    /// Implicitly converts a <see cref="SearchExpression"/> to a <see cref="FilterBy{T}"/>.
    /// Named alternate: <see cref="FilterBy.Filter{T}"/>.
    /// </summary>
#pragma warning disable CA2225 // Named alternate is FilterBy.Filter<T>() on the non-generic class
    public static implicit operator FilterBy<T>(SearchExpression searchExpression) => new()
    {
        SearchExpressionValue = searchExpression
    };

    /// <summary>
    /// Implicitly converts a typed filter to a <see cref="FilterBy{T}"/>.
    /// Named alternate: <see cref="FilterBy.Filter{T}"/>.
    /// </summary>
    public static implicit operator FilterBy<T>(T filterValue) => new()
    {
        FilterValue = filterValue
    };
#pragma warning restore CA2225
#pragma warning restore CA1000

    /// <summary>
    /// Creates a <see cref="FilterBy{T}"/> from a typed filter value.
    /// </summary>
    internal static FilterBy<T> FromFilter(T filterValue) => new()
    {
        FilterValue = filterValue
    };
}
