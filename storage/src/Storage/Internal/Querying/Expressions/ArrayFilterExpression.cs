// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that filters array items where all conditions must match within the same array element.
/// This correlates conditions within the same array item using the item_index column.
/// 
/// Example: emails[type eq "work" and value co "@example.com"]
/// This ensures both conditions match the same email in the emails array.
/// </summary>
public sealed record ArrayFilterExpression : IQueryFilterExpression
{
    /// <summary>
    /// The array field path (e.g., "emails").
    /// </summary>
    public string ArrayFieldPath { get; }

    /// <summary>
    /// The filter expression that applies to fields within the array.
    /// Field paths in this expression are relative to the array (e.g., "type", "value").
    /// </summary>
    public IQueryFilterExpression Filter { get; }

    public ArrayFilterExpression(string arrayFieldPath, IQueryFilterExpression filter)
    {
        if (string.IsNullOrWhiteSpace(arrayFieldPath))
        {
            throw new ArgumentException("Array field path cannot be null or whitespace.", nameof(arrayFieldPath));
        }

        ArrayFieldPath = arrayFieldPath.ToUpperInvariant();
        Filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }
}
