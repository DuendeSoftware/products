// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

/// <summary>
/// Provides administrative operations for managing the user profile attribute schema,
/// including attribute definitions and attribute groups.
/// </summary>
public interface IUserProfileSchemaAdmin
{
    /// <summary>
    /// Retrieves all attribute definitions in the current schema.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only dictionary of attribute definitions keyed by their <see cref="AttributeCode"/>.</returns>
    public Task<IReadOnlyDictionary<AttributeCode, AttributeDefinition>> GetAllAttributeDefinitionsAsync(Ct ct);

    /// <summary>
    /// Retrieves all attribute groups in the current schema.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A read-only dictionary of attribute groups keyed by their <see cref="AttributeGroupCode"/>.</returns>
    public Task<IReadOnlyDictionary<AttributeGroupCode, AttributeGroup>> GetAllGroupsAsync(Ct ct);

    /// <summary>
    /// Attempts to add a new attribute definition to the schema.
    /// Returns <c>false</c> if an attribute with the same code already exists.
    /// </summary>
    /// <param name="definition">The attribute definition to add.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the definition was added; <c>false</c> if it already exists.</returns>
    public Task<bool> TryAddAttributeDefinitionAsync(AttributeDefinition definition, Ct ct);

    /// <summary>
    /// Attempts to remove an attribute definition from the schema by its code.
    /// Returns <c>false</c> if no definition with the given code exists.
    /// </summary>
    /// <param name="code">The code of the attribute definition to remove.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the definition was removed; <c>false</c> if it was not found.</returns>
    public Task<bool> TryRemoveAttributeDefinitionAsync(AttributeCode code, Ct ct);

    /// <summary>
    /// Attempts to add a new attribute group to the schema.
    /// Returns <c>false</c> if a group with the same code already exists.
    /// </summary>
    /// <param name="group">The attribute group to add.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the group was added; <c>false</c> if it already exists.</returns>
    public Task<bool> TryAddGroupAsync(AttributeGroup group, Ct ct);

    /// <summary>
    /// Attempts to remove an attribute group from the schema by its code.
    /// Returns <c>false</c> if no group with the given code exists.
    /// </summary>
    /// <param name="name">The code of the group to remove.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the group was removed; <c>false</c> if it was not found.</returns>
    public Task<bool> TryRemoveGroupAsync(AttributeGroupCode name, Ct ct);

    /// <summary>
    ///     Reorders attributes within a group (or among ungrouped attributes when <paramref name="group"/> is <c>null</c>).
    ///     The server assigns <see cref="AttributeDefinition.Order"/> values (0, 1, 2, …) based on the supplied list.
    ///     Attributes not present in the list retain their current order, appended after the listed ones.
    ///     Returns <c>false</c> if no schema exists or the group (when non-null) is not found.
    /// </summary>
    public Task<bool> ReorderAttributesAsync(AttributeGroupCode? group, IReadOnlyList<AttributeCode> orderedCodes, Ct ct);

    /// <summary>
    ///     Reorders groups by assigning <see cref="AttributeGroup.Order"/> values (0, 1, 2, …) based on the supplied list.
    ///     Groups not present in the list retain their current order, appended after the listed ones.
    ///     Returns <c>false</c> if no schema exists.
    /// </summary>
    public Task<bool> ReorderGroupsAsync(IReadOnlyList<AttributeGroupCode> orderedGroups, Ct ct);
}
