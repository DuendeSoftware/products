// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying.Fields;

/// <summary>
/// Represents a number-valued field with numeric comparison operations.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record NumberField : Field
{
    /// <summary>
    /// Initializes a new instance of <see cref="NumberField"/>.
    /// </summary>
    /// <param name="path">The field path.</param>
    /// <param name="isMultiValued">Whether the field is multi-valued.</param>
    public NumberField(string path, bool isMultiValued = false) : base(path, FieldType.Number, isMultiValued)
    {
    }

    /// <summary>
    /// Creates an expression that checks if the field equals the specified value.
    /// </summary>
    public IQueryFilterExpression Equals(decimal value) => new EqualExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field is greater than the specified value.
    /// </summary>
    public IQueryFilterExpression GreaterThan(decimal value) => new GreaterThanExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field is less than the specified value.
    /// </summary>
    public IQueryFilterExpression LessThan(decimal value) => new LessThanExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field is greater than or equal to the specified value.
    /// </summary>
    public IQueryFilterExpression GreaterOrEqual(decimal value) => new GreaterOrEqualExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field is less than or equal to the specified value.
    /// </summary>
    public IQueryFilterExpression LessOrEqual(decimal value) => new LessOrEqualExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field value is between the specified range (inclusive).
    /// </summary>
    public IQueryFilterExpression Between(decimal min, decimal max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), min, "Minimum value must not exceed maximum value.");
        }

        return new BetweenExpression(this, min, max);
    }

    /// <summary>
    /// Creates an expression that checks if the field value is in the specified collection.
    /// </summary>
    public IQueryFilterExpression In(IReadOnlyCollection<decimal> values) => new InExpression(this, values);
}
