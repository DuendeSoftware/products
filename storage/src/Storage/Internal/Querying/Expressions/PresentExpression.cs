// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a field has a value (is present / not null).
/// Used for the SCIM 'pr' (present) operator.
/// For scalar fields, checks that a search_values row exists with a non-null typed column.
/// For array fields, checks that at least one row with item_index >= 0 exists.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record PresentExpression : IQueryFilterExpression
{
    /// <summary>Gets the field to check for presence.</summary>
    public Field Field { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PresentExpression"/>.
    /// </summary>
    /// <param name="field">The field to check for presence.</param>
    public PresentExpression(Field field) =>
        Field = field ?? throw new ArgumentNullException(nameof(field));
}
