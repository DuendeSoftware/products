// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Base type for schema attribute type descriptors.
/// </summary>
public abstract record AttributeType
{
    internal AttributeType() { }

    /// <summary>
    ///     Validates that no list is nested inside another list at any depth.
    /// </summary>
    internal void ValidateNesting() => ValidateNesting(insideList: false);

    private void ValidateNesting(bool insideList)
    {
        switch (this)
        {
            case ScalarAttributeType:
                // leaf types — always valid
                break;

            case ComplexAttributeType complex:
                foreach (var (_, prop) in complex.Properties)
                {
                    prop.Type.ValidateNesting(insideList);
                }
                break;

            case ListAttributeType list:
                if (insideList)
                {
                    throw new ArgumentException("List types cannot be nested inside another list type.");
                }
                list.ElementType.ValidateNesting(insideList: true);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported {nameof(AttributeType)} subtype encountered in {nameof(ValidateNesting)}: {GetType().FullName}");
        }
    }
}
