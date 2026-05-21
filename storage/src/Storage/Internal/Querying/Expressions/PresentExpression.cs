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
public sealed record PresentExpression : IQueryFilterExpression
{
    public Field Field { get; }

    public PresentExpression(Field field) =>
        Field = field ?? throw new ArgumentNullException(nameof(field));
}
