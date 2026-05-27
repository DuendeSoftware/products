// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Profiles.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserProfileSchemaAdmin(AttributeSchemaRepository repo, ILogger<UserProfileSchemaAdmin> logger, UserManagementLicenseValidator licenseValidator) : IUserProfileSchemaAdmin
{
    public async Task<IReadOnlyDictionary<AttributeCode, AttributeDefinition>> GetAllAttributeDefinitionsAsync(Ct ct) =>
        (await repo.TryReadAsync(UserProfileSchemaId.Value, ct))?.AttributeSchema.AttributeDefinitions ?? new Dictionary<AttributeCode, AttributeDefinition>();

    public async Task<IReadOnlyDictionary<AttributeGroupCode, AttributeGroup>> GetAllGroupsAsync(Ct ct) =>
        (await repo.TryReadAsync(UserProfileSchemaId.Value, ct))?.AttributeSchema.Groups ?? new Dictionary<AttributeGroupCode, AttributeGroup>();

    public async Task<bool> TryAddAttributeDefinitionAsync(AttributeDefinition definition, Ct ct)
    {
        licenseValidator.ValidateProfiles();
        bool result;
        if (await repo.TryReadAsync(UserProfileSchemaId.Value, ct) is not ({ } schema, var version))
        {
            schema = AttributeSchema.Load([], []);
            result = schema.AddAttributeDefinition(definition) && await repo.CreateAsync(UserProfileSchemaId.Value, schema, ct) is CreateResult.Success;
        }
        else
        {
            result = schema.AddAttributeDefinition(definition) && await repo.UpdateAsync(UserProfileSchemaId.Value, schema, version, ct) is UpdateResult.Success;
        }

        if (result)
        {
            logger.SchemaAttributeAdded(LogLevel.Debug, definition.Code.Value);
        }
        else
        {
            logger.SchemaAttributeAddFailed(LogLevel.Debug, definition.Code.Value);
        }

        return result;
    }

    public async Task<bool> TryRemoveAttributeDefinitionAsync(AttributeCode code, Ct ct)
    {
        licenseValidator.ValidateProfiles();
        if (await repo.TryReadAsync(UserProfileSchemaId.Value, ct) is not ({ } schema, var version))
        {
            return true;
        }

        schema.RemoveAttributeDefinition(code);
        var result = await repo.UpdateAsync(UserProfileSchemaId.Value, schema, version, ct) is UpdateResult.Success;

        if (result)
        {
            logger.SchemaAttributeRemoved(LogLevel.Debug, code.Value);
        }
        else
        {
            logger.SchemaAttributeRemoveFailed(LogLevel.Debug, code.Value);
        }

        return result;
    }

    public async Task<bool> TryAddGroupAsync(AttributeGroup group, Ct ct)
    {
        licenseValidator.ValidateProfiles();
        bool result;
        if (await repo.TryReadAsync(UserProfileSchemaId.Value, ct) is not ({ } schema, var version))
        {
            schema = AttributeSchema.Load([], []);
            result = schema.AddGroup(group) && await repo.CreateAsync(UserProfileSchemaId.Value, schema, ct) is CreateResult.Success;
        }
        else
        {
            result = schema.AddGroup(group) && await repo.UpdateAsync(UserProfileSchemaId.Value, schema, version, ct) is UpdateResult.Success;
        }

        if (result)
        {
            logger.SchemaGroupAdded(LogLevel.Debug, group.Code.Value);
        }
        else
        {
            logger.SchemaGroupAddFailed(LogLevel.Debug, group.Code.Value);
        }

        return result;
    }

    public async Task<bool> TryRemoveGroupAsync(AttributeGroupCode name, Ct ct)
    {
        licenseValidator.ValidateProfiles();
        if (await repo.TryReadAsync(UserProfileSchemaId.Value, ct) is not ({ } schema, var version))
        {
            return true;
        }

        _ = schema.RemoveGroup(name);
        var result = await repo.UpdateAsync(UserProfileSchemaId.Value, schema, version, ct) is UpdateResult.Success;

        if (result)
        {
            logger.SchemaGroupRemoved(LogLevel.Debug, name.Value);
        }
        else
        {
            logger.SchemaGroupRemoveFailed(LogLevel.Debug, name.Value);
        }

        return result;
    }

    public async Task<bool> ReorderAttributesAsync(AttributeGroupCode? group, IReadOnlyList<AttributeCode> orderedCodes, Ct ct)
    {
        licenseValidator.ValidateProfiles();
        if (await repo.TryReadAsync(UserProfileSchemaId.Value, ct) is not ({ } schema, var version))
        {
            logger.SchemaAttributesReorderFailedSchemaNotFound(LogLevel.Debug);
            return false;
        }

        if (group is not null && !schema.Groups.ContainsKey(group))
        {
            logger.SchemaAttributesReorderFailedGroupNotFound(LogLevel.Debug, group.Value);
            return false;
        }

        // Assign Order = index for listed codes, then append unlisted ones after
        var listed = orderedCodes
            .Where(n => schema.AttributeDefinitions.TryGetValue(n, out var d) && d.GroupCode == group)
            .Distinct()
            .ToList();

        var unlisted = schema.AttributeDefinitions.Values
            .Where(d => d.GroupCode == group && !listed.Contains(d))
            .OrderBy(d => d.Order)
            .ThenBy(d => d.Code.Value, StringComparer.OrdinalIgnoreCase)
            .Select(d => d.Code)
            .ToList();

        var allOrdered = listed.Concat(unlisted).ToList();

        foreach (var (code, index) in allOrdered.Select((n, i) => (n, i)))
        {
            if (!schema.AttributeDefinitions.TryGetValue(code, out var def))
            {
                continue;
            }

            var updated = AttributeDefinition.Load(
                def, def.AttributeType, def.Description, def.DisplayName, def.IsUnique, def.IsQueryable,
                def.IsRequired, def.Tags, def.GroupCode, index);
            schema.RemoveAttributeDefinition(def);
            _ = schema.AddAttributeDefinition(updated);
        }

        var result = await repo.UpdateAsync(UserProfileSchemaId.Value, schema, version, ct) is UpdateResult.Success;

        if (result)
        {
            logger.SchemaAttributesReordered(LogLevel.Debug, group?.Value);
        }

        return result;
    }

    public async Task<bool> ReorderGroupsAsync(IReadOnlyList<AttributeGroupCode> orderedGroups, Ct ct)
    {
        licenseValidator.ValidateProfiles();
        if (await repo.TryReadAsync(UserProfileSchemaId.Value, ct) is not ({ } schema, var version))
        {
            logger.SchemaGroupsReorderFailedSchemaNotFound(LogLevel.Debug);
            return false;
        }

        var listed = orderedGroups
            .Where(n => schema.Groups.ContainsKey(n))
            .Distinct()
            .ToList();

        var unlisted = schema.Groups.Values
            .Where(g => !listed.Contains(g.Code))
            .OrderBy(g => g.Order)
            .ThenBy(g => g.Code.Value, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Code)
            .ToList();

        var allOrdered = listed.Concat(unlisted).ToList();

        foreach (var (name, index) in allOrdered.Select((n, i) => (n, i)))
        {
            if (!schema.Groups.TryGetValue(name, out var grp))
            {
                continue;
            }

            var updated = grp with { Order = index };
            _ = schema.UpdateGroup(updated);
        }

        var result = await repo.UpdateAsync(UserProfileSchemaId.Value, schema, version, ct) is UpdateResult.Success;

        if (result)
        {
            logger.SchemaGroupsReordered(LogLevel.Debug);
        }

        return result;
    }
}
