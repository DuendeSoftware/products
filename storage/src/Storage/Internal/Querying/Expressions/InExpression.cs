// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a field value is in a specified collection.
/// </summary>
public sealed record InExpression : IQueryFilterExpression
{
    public Field Field { get; }
    public IEnumerable Values { get; }

    public InExpression(Field field, IEnumerable values)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Values = values ?? throw new ArgumentNullException(nameof(values));
    }
}
