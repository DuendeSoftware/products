// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a field value is between two values (inclusive).
/// </summary>
public sealed record BetweenExpression : IQueryFilterExpression
{
    public Field Field { get; }
    public object Min { get; }
    public object Max { get; }

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
