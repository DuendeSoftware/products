// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a named group of attributes with display metadata and sort ordering.
/// </summary>
public sealed record AttributeGroup
{
    /// <summary>
    ///     Creates a new attribute group with the specified metadata.
    /// </summary>
    /// <param name="Code">The unique code identifying this group.</param>
    /// <param name="DisplayName">An optional human-readable display name.</param>
    /// <param name="Description">An optional description of the group.</param>
    /// <param name="Order">The sort order for display purposes.</param>
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

    /// <summary>
    ///     The unique code identifying this group.
    /// </summary>
    public AttributeGroupCode Code { get; init; }

    /// <summary>
    ///     An optional human-readable display name for the group.
    /// </summary>
    public AttributeDisplayName? DisplayName { get; init; }

    /// <summary>
    ///     An optional description of the group.
    /// </summary>
    public AttributeDescription? Description { get; init; }

    /// <summary>
    ///     The sort order for display purposes.
    /// </summary>
    public int Order { get; init; }
}
