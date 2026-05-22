// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying.Fields;

/// <summary>
/// Represents a boolean-valued field with boolean-specific operations.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record BooleanField : Field
{
    /// <summary>
    /// Initializes a new instance of <see cref="BooleanField"/>.
    /// </summary>
    /// <param name="path">The field path.</param>
    /// <param name="isMultiValued">Whether the field is multi-valued.</param>
    public BooleanField(string path, bool isMultiValued = false) : base(path, FieldType.Boolean, isMultiValued)
    {
    }

    /// <summary>
    /// Creates an expression that checks if the field equals the specified value.
    /// </summary>
    public IQueryFilterExpression Equals(bool value) => new EqualExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field is true.
    /// </summary>
    public IQueryFilterExpression IsTrue() => new EqualExpression(this, true);

    /// <summary>
    /// Creates an expression that checks if the field is false.
    /// </summary>
    public IQueryFilterExpression IsFalse() => new EqualExpression(this, false);
}
