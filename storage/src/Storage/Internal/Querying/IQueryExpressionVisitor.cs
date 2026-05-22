// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Visitor interface for processing query expression trees.
/// Implementations can translate expressions to different formats (e.g., SQL, in-memory evaluation).
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <typeparam name="TResult">The type returned by visiting an expression.</typeparam>
public interface IQueryExpressionVisitor<TResult>
{
    /// <summary>
    /// Visits an AllExpression that matches all records.
    /// </summary>
    TResult Visit(AllExpression expression);

    /// <summary>
    /// Visits an EqualExpression that checks field equality.
    /// </summary>
    TResult Visit(EqualExpression expression);

    /// <summary>
    /// Visits a ContainsExpression that checks if a string field contains a substring.
    /// </summary>
    TResult Visit(ContainsExpression expression);

    /// <summary>
    /// Visits a StartsWithExpression that checks if a string field starts with a prefix.
    /// </summary>
    TResult Visit(StartsWithExpression expression);

    /// <summary>
    /// Visits a GreaterThanExpression that checks if a field is greater than a value.
    /// </summary>
    TResult Visit(GreaterThanExpression expression);

    /// <summary>
    /// Visits a LessThanExpression that checks if a field is less than a value.
    /// </summary>
    TResult Visit(LessThanExpression expression);

    /// <summary>
    /// Visits a GreaterOrEqualExpression that checks if a field is greater than or equal to a value.
    /// </summary>
    TResult Visit(GreaterOrEqualExpression expression);

    /// <summary>
    /// Visits a LessOrEqualExpression that checks if a field is less than or equal to a value.
    /// </summary>
    TResult Visit(LessOrEqualExpression expression);

    /// <summary>
    /// Visits a BetweenExpression that checks if a field is between two values (inclusive).
    /// </summary>
    TResult Visit(BetweenExpression expression);

    /// <summary>
    /// Visits an InExpression that checks if a field value is in a collection.
    /// </summary>
    TResult Visit(InExpression expression);

    /// <summary>
    /// Visits an AndExpression that combines multiple expressions with AND logic.
    /// </summary>
    TResult Visit(AndExpression expression);

    /// <summary>
    /// Visits an OrExpression that combines multiple expressions with OR logic.
    /// </summary>
    TResult Visit(OrExpression expression);

    /// <summary>
    /// Visits an ArrayFilterExpression that filters array items where conditions match within the same element.
    /// </summary>
    TResult Visit(ArrayFilterExpression expression);

    /// <summary>
    /// Visits a NotExpression that negates an inner expression.
    /// </summary>
    TResult Visit(NotExpression expression);

    /// <summary>
    /// Visits an EndsWithExpression that checks if a string field ends with a suffix.
    /// </summary>
    TResult Visit(EndsWithExpression expression);

    /// <summary>
    /// Visits a PresentExpression that checks if a field has a value.
    /// </summary>
    TResult Visit(PresentExpression expression);

    /// <summary>
    /// Visits an ArrayContainsExpression that checks if a string array contains a specific value.
    /// </summary>
    TResult Visit(ArrayContainsExpression expression);
}
