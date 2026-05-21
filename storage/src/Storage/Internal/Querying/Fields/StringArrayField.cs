// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying.Fields;

/// <summary>
/// Represents a string array field (multi-valued string).
/// This is a convenience subclass of <see cref="Field"/> with <see cref="Field.IsMultiValued"/> set to true.
/// </summary>
public sealed record StringArrayField : Field
{
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
