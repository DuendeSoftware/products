// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering;

/// <summary>
/// Defines the comparison operators supported by the filter expression parser.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum ComparisonOperator
{
    /// <summary>Equality comparison.</summary>
    Equal,

    /// <summary>Inequality comparison.</summary>
    NotEqual,

    /// <summary>Substring containment.</summary>
    Contains,

    /// <summary>Prefix match.</summary>
    StartsWith,

    /// <summary>Suffix match.</summary>
    EndsWith,

    /// <summary>Greater than comparison.</summary>
    GreaterThan,

    /// <summary>Greater than or equal comparison.</summary>
    GreaterThanOrEqual,

    /// <summary>Less than comparison.</summary>
    LessThan,

    /// <summary>Less than or equal comparison.</summary>
    LessThanOrEqual,

    /// <summary>Presence check (field has a value).</summary>
    Present
}
