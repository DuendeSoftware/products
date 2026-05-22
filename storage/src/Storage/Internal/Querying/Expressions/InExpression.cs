// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a field value is in a specified collection.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record InExpression : IQueryFilterExpression
{
    /// <summary>Gets the field to check.</summary>
    public Field Field { get; }

    /// <summary>Gets the collection of values to match against.</summary>
    public IEnumerable Values { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="InExpression"/>.
    /// </summary>
    /// <param name="field">The field to check.</param>
    /// <param name="values">The collection of values.</param>
    public InExpression(Field field, IEnumerable values)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Values = values ?? throw new ArgumentNullException(nameof(values));
    }
}
