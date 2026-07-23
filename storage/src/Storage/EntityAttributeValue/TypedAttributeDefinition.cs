// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Binds an <see cref="AttributeCode"/> to its expected CLR value type <typeparamref name="T"/>,
///     enabling compile-time type safety when setting attribute values on an
///     <see cref="AttributeValueCollection"/>.
/// </summary>
/// <typeparam name="T">The CLR type of the attribute value (e.g., <c>string</c>, <c>bool</c>, <c>int</c>).</typeparam>
public sealed class TypedAttributeDefinition<T>
{
    /// <summary>
    ///     Creates a new typed attribute definition.
    /// </summary>
    /// <param name="code">The attribute code.</param>
    /// <param name="attributeType">The attribute type descriptor.</param>
    public TypedAttributeDefinition(AttributeCode code, AttributeType attributeType)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(attributeType);
        Code = code;
        AttributeType = attributeType;
    }

    /// <summary>
    ///     The attribute code identifying this attribute.
    /// </summary>
    public AttributeCode Code { get; }

    /// <summary>
    ///     The attribute type descriptor.
    /// </summary>
    public AttributeType AttributeType { get; }

    /// <summary>
    ///     Converts this typed definition to an <see cref="AttributeDefinition"/>
    ///     with the same code and attribute type.
    /// </summary>
    /// <param name="definition">The typed definition to convert.</param>
#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator AttributeDefinition(TypedAttributeDefinition<T> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new AttributeDefinition
        {
            Code = definition.Code,
            AttributeType = definition.AttributeType
        };
    }
#pragma warning restore CA2225
}
