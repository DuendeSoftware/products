// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a field equals a specified value.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record EqualExpression : IQueryFilterExpression
{
    /// <summary>Gets the field to compare.</summary>
    public Field Field { get; }

    /// <summary>Gets the value to compare against.</summary>
    public object Value { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="EqualExpression"/>.
    /// </summary>
    /// <param name="field">The field to compare.</param>
    /// <param name="value">The value to compare against.</param>
    public EqualExpression(Field field, object value)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}
