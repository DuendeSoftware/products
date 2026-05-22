// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a field value is between two values (inclusive).
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record BetweenExpression : IQueryFilterExpression
{
    /// <summary>Gets the field to compare.</summary>
    public Field Field { get; }

    /// <summary>Gets the minimum value (inclusive).</summary>
    public object Min { get; }

    /// <summary>Gets the maximum value (inclusive).</summary>
    public object Max { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BetweenExpression"/>.
    /// </summary>
    /// <param name="field">The field to compare.</param>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (inclusive).</param>
    public BetweenExpression(Field field, object min, object max)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Min = min ?? throw new ArgumentNullException(nameof(min));
        Max = max ?? throw new ArgumentNullException(nameof(max));

        if (field.Type == FieldType.String)
        {
            throw new ArgumentException("Between expression cannot be used with string fields.", nameof(field));
        }
    }
}
