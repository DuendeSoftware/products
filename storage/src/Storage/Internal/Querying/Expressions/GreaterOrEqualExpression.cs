// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a field is greater than or equal to a specified value.
/// </summary>
public sealed record GreaterOrEqualExpression : IQueryFilterExpression
{
    public Field Field { get; }
    public object Value { get; }

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
