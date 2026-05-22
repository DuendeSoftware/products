// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering;

/// <summary>
/// Defines the logical operators supported by the filter expression parser.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum LogicalOperator
{
    /// <summary>Logical AND.</summary>
    And,

    /// <summary>Logical OR.</summary>
    Or,

    /// <summary>Logical NOT (negation).</summary>
    Not
}
