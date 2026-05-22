// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Singleton expression representing 'match all records' - no filter applied.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record AllExpression : IQueryFilterExpression
{
    /// <summary>
    /// Singleton instance of AllExpression.
    /// </summary>
    public static readonly AllExpression Instance = new();

    // Private constructor to enforce singleton pattern
    private AllExpression()
    {
    }
}
