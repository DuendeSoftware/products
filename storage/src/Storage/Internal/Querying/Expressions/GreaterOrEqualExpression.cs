// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a field is greater than or equal to a specified value.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record GreaterOrEqualExpression : IQueryFilterExpression
{
    /// <summary>Gets the field to compare.</summary>
    public Field Field { get; }

    /// <summary>Gets the value to compare against.</summary>
    public object Value { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="GreaterOrEqualExpression"/>.
    /// </summary>
    /// <param name="field">The field to compare.</param>
    /// <param name="value">The value to compare against.</param>
    public GreaterOrEqualExpression(Field field, object value)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Value = value ?? throw new ArgumentNullException(nameof(value));

        if (field.Type == FieldType.String)
        {
            throw new ArgumentException("GreaterOrEqual expression cannot be used with string fields.", nameof(field));
        }
    }
}
