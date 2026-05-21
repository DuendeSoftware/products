// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a field is less than a specified value.
/// </summary>
public sealed record LessThanExpression : IQueryFilterExpression
{
    public Field Field { get; }
    public object Value { get; }

    public LessThanExpression(Field field, object value)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Value = value ?? throw new ArgumentNullException(nameof(value));

        if (field.Type == FieldType.String)
        {
            throw new ArgumentException("LessThan expression cannot be used with string fields.", nameof(field));
        }
    }
}
