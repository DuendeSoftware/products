// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a string field starts with a specified value.
/// </summary>
public sealed record StartsWithExpression : IQueryFilterExpression
{
    public StringField Field { get; }
    public string Value { get; }

    public StartsWithExpression(StringField field, string value)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}
