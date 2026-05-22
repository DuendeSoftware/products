// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying.Fields;

/// <summary>
/// Represents a string array field (multi-valued string).
/// This is a convenience subclass of <see cref="Field"/> with <see cref="Field.IsMultiValued"/> set to true.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record StringArrayField : Field
{
    /// <summary>
    /// Initializes a new instance of <see cref="StringArrayField"/>.
    /// </summary>
    /// <param name="path">The field path.</param>
    public StringArrayField(string path) : base(path, FieldType.String, isMultiValued: true)
    {
    }

    /// <summary>
    /// Creates an expression that checks if the array contains an element equal to the specified value.
    /// Uses the guid_value column via a deterministic hash for faster exact-match lookup.
    /// </summary>
    public IQueryFilterExpression Contains(string value) =>
        new EqualExpression(this, DeterministicGuidGenerator.Create(value.ToUpperInvariant()));
}
