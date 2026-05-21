// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Querying.Expressions;

/// <summary>
/// Expression that checks if a string array field contains an element equal to the specified value.
/// Generates an EXISTS subquery with item_index >= 0 to match any array element.
/// </summary>
public sealed record ArrayContainsExpression(StringArrayField Field, string Value) : IQueryExpression, IQueryFilterExpression;
