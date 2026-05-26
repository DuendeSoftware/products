// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

public interface IUserProfileSchemaAdmin
{
    public Task<IReadOnlyDictionary<AttributeCode, AttributeDefinition>> GetAllAttributeDefinitionsAsync(Ct ct);

    public Task<IReadOnlyDictionary<AttributeGroupCode, AttributeGroup>> GetAllGroupsAsync(Ct ct);

    public Task<bool> TryAddAttributeDefinitionAsync(AttributeDefinition definition, Ct ct);

    public Task<bool> TryRemoveAttributeDefinitionAsync(AttributeCode code, Ct ct);

    public Task<bool> TryAddGroupAsync(AttributeGroup group, Ct ct);

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
