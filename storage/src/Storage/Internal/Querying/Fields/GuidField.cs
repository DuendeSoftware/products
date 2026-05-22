// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Expressions;

namespace Duende.Storage.Internal.Querying.Fields;

/// <summary>
/// Represents a Guid-valued field stored in the guid_value column.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record GuidField : Field
{
    /// <summary>
    /// Initializes a new instance of <see cref="GuidField"/>.
    /// </summary>
    /// <param name="path">The field path.</param>
    /// <param name="isMultiValued">Whether the field is multi-valued.</param>
    public GuidField(string path, bool isMultiValued = false) : base(path, FieldType.Guid, isMultiValued)
    {
    }

    /// <summary>
    /// Creates an expression that checks if the field equals the specified GUID value.
    /// </summary>
    public IQueryFilterExpression Equals(Guid value) => new EqualExpression(this, value);

    /// <summary>
    /// Creates an expression that checks if the field value is in the specified collection.
    /// </summary>
    public IQueryFilterExpression In(IReadOnlyCollection<Guid> values) => new InExpression(this, values.ToList());
}
