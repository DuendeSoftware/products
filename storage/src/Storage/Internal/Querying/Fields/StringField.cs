// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying.Fields;

/// <summary>
/// Represents a string-valued field with string-specific operations.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record StringField : Field
{
    /// <summary>
    /// Initializes a new instance of <see cref="StringField"/>.
    /// </summary>
    /// <param name="path">The field path.</param>
    /// <param name="isMultiValued">Whether the field is multi-valued.</param>
    public StringField(string path, bool isMultiValued = false) : base(path, FieldType.String, isMultiValued)
    {
    }

    /// <summary>
    /// Creates an expression that checks if the field equals the specified value.
    /// Uses the guid_value column via a deterministic hash for faster exact-match lookup.
    /// </summary>
    public IQueryFilterExpression Equals(string value) =>
        new EqualExpression(this, DeterministicGuidGenerator.Create(value.ToUpperInvariant()));

    /// <summary>
    /// Creates an expression that checks if the field contains the specified substring.
    /// </summary>
    public IQueryFilterExpression Contains(string value) => new ContainsExpression(this, value.ToUpperInvariant());

    /// <summary>
    /// Creates an expression that checks if the field starts with the specified value.
    /// </summary>
    public IQueryFilterExpression StartsWith(string value) => new StartsWithExpression(this, value.ToUpperInvariant());

    /// <summary>
    /// Creates an expression that checks if the field ends with the specified suffix.
    /// </summary>
    public IQueryFilterExpression EndsWith(string value) => new EndsWithExpression(this, value.ToUpperInvariant());

    /// <summary>
    /// Creates an expression that checks if the field value is in the specified collection.
    /// Uses the guid_value column via deterministic hashes for faster exact-match lookup.
    /// </summary>
    public IQueryFilterExpression In(IReadOnlyCollection<string> values) =>
        new InExpression(this, values.Select(v => DeterministicGuidGenerator.Create(v.ToUpperInvariant())).ToList());
}
