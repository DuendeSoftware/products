// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Static entry point for building query expressions.
/// Provides a fluent API for constructing query filter expressions.
/// </summary>
public static class Query
{
    /// <summary>
    /// Creates a query expression that matches all records (no filter).
    /// </summary>
    public static IQueryExpression All() => AllExpression.Instance;

    /// <summary>
    /// Creates a query expression starting with the specified filter.
    /// </summary>
    public static IQueryFilterExpression Where(IQueryFilterExpression filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return filter;
    }

    /// <summary>
    /// Creates an array filter expression that correlates conditions within the same array item.
    /// This is used for SCIM2-style array filters like: emails[type eq "work" and value co "@example.com"]
    /// </summary>
    /// <param name="arrayFieldPath">The array field path (e.g., "emails").</param>
    /// <param name="filter">The filter expression that applies to fields within the array.</param>
    public static IQueryFilterExpression ArrayFilter(string arrayFieldPath, IQueryFilterExpression filter) =>
        new ArrayFilterExpression(arrayFieldPath, filter);

    /// <summary>
    /// Negates the specified filter expression.
    /// Used for SCIM 'not' operator and 'ne' (as Not(field.Equals("x"))).
    /// </summary>
    public static IQueryFilterExpression Not(IQueryFilterExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new NotExpression(expression);
    }
}
