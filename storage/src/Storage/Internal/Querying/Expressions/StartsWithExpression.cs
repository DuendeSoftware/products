// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a string field starts with a specified value.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record StartsWithExpression : IQueryFilterExpression
{
    /// <summary>Gets the string field to check.</summary>
    public StringField Field { get; }

    /// <summary>Gets the prefix value.</summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="StartsWithExpression"/>.
    /// </summary>
    /// <param name="field">The string field to check.</param>
    /// <param name="value">The prefix to match.</param>
    public StartsWithExpression(StringField field, string value)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}
