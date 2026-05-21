// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying.Fields;

/// <summary>
/// Represents a DateTimeOffset-valued field with temporal comparison operations.
/// </summary>
public sealed record DateTimeField : Field
{
    public DateTimeField(string path, bool isMultiValued = false) : base(path, FieldType.DateTime, isMultiValued)
    {
    }

    /// <summary>
    /// Creates an expression that checks if the field equals the specified value.
    /// </summary>
    public IQueryFilterExpression Equals(DateTimeOffset value) => new EqualExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field is greater than the specified value.
    /// </summary>
    public IQueryFilterExpression GreaterThan(DateTimeOffset value) => new GreaterThanExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field is less than the specified value.
    /// </summary>
    public IQueryFilterExpression LessThan(DateTimeOffset value) => new LessThanExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field is greater than or equal to the specified value.
    /// </summary>
    public IQueryFilterExpression GreaterOrEqual(DateTimeOffset value) => new GreaterOrEqualExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field is less than or equal to the specified value.
    /// </summary>
    public IQueryFilterExpression LessOrEqual(DateTimeOffset value) => new LessOrEqualExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field value is between the specified range (inclusive).
    /// </summary>
    public IQueryFilterExpression Between(DateTimeOffset min, DateTimeOffset max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), min, "Minimum value must not exceed maximum value.");
        }

        return new BetweenExpression(this, min, max);
    }
}
