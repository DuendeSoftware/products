// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
///     Persisted representation of an <see cref="AttributeType"/>.
///     <c>EnumValues</c> and <c>ConstrainedValues</c> are reserved for future enum/constrained-string
///     attribute types and are currently always <c>null</c>.
/// </summary>
public sealed record AttributeTypeDso(
    string Kind,
    string? ScalarDataType,
    IReadOnlyList<EnumValueDso>? EnumValues,
    IReadOnlyList<string>? ConstrainedValues,
    Dictionary<string, ComplexPropertyDso>? Properties,
    AttributeTypeDso? ElementType);

/// <summary>
///     Persisted representation of a sub-property within a complex attribute type.
/// </summary>
public sealed record ComplexPropertyDso(
    AttributeTypeDso Type,
    string? DisplayName,
    string? Description);
