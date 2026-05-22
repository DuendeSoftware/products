// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying.Fields;

/// <summary>
/// Represents a field that stores a deterministic GUID hash of a string value.
/// Only supports exact-match operations (Equals and In).
/// Queries use the guid_value column with hashed values for fast lookups.
/// No string_value is stored — only the deterministic hash in guid_value.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record ExactMatchField : Field
{
    /// <summary>
    /// Initializes a new instance of <see cref="ExactMatchField"/>.
    /// </summary>
    /// <param name="path">The field path.</param>
    public ExactMatchField(string path) : base(path, FieldType.Guid)
    {
    }

    /// <summary>
    /// Creates an expression that checks if the field equals the deterministic hash of the specified value.
    /// </summary>
    public IQueryFilterExpression Equals(string value) =>
        new EqualExpression(this, DeterministicGuidGenerator.Create(value.ToUpperInvariant()));

    /// <summary>
    /// Creates an expression that checks if the field value hash is in the specified collection.
    /// </summary>
    public IQueryFilterExpression In(IReadOnlyCollection<string> values) =>
        new InExpression(this, values.Select(v => DeterministicGuidGenerator.Create(v.ToUpperInvariant())).ToList());
}
