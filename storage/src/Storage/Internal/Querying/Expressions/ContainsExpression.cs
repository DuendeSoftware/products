// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a string field contains a specified substring.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record ContainsExpression : IQueryFilterExpression
{
    /// <summary>Gets the string field to search.</summary>
    public StringField Field { get; }

    /// <summary>Gets the substring to search for.</summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ContainsExpression"/>.
    /// </summary>
    /// <param name="field">The string field to search.</param>
    /// <param name="value">The substring to search for.</param>
    public ContainsExpression(StringField field, string value)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}
