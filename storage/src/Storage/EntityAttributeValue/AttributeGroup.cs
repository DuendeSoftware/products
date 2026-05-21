// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a named group of attributes with display metadata and sort ordering.
/// </summary>
public sealed record AttributeGroup
{
    public AttributeGroup(
        AttributeGroupCode Code,
        AttributeDisplayName? DisplayName,
        AttributeDescription? Description,
        int Order)
    {
        this.Code = Code;
        this.DisplayName = DisplayName;
        this.Description = Description;
        this.Order = Order;
    }

    public AttributeGroupCode Code { get; init; }
    public AttributeDisplayName? DisplayName { get; init; }
    public AttributeDescription? Description { get; init; }
    public int Order { get; init; }
}
