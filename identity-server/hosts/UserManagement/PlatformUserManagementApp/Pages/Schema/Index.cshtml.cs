// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PlatformUserManagementApp.Pages.Schema;

[Authorize]
public sealed class IndexModel(ISchemaAdmin schemaAdmin) : PageModel
{
    public IReadOnlyDictionary<AttributeCode, AttributeDefinition> Attributes { get; set; } =
        new Dictionary<AttributeCode, AttributeDefinition>();
    public IReadOnlyDictionary<AttributeGroupCode, AttributeGroup> Groups { get; set; } =
        new Dictionary<AttributeGroupCode, AttributeGroup>();
    public List<string> Messages { get; set; } = [];
    public List<string> ErrorMessages { get; set; } = [];

    public async Task OnGetAsync() => await LoadSchemaAsync();

    public async Task<IActionResult> OnPostAddGroupAsync(
        [FromForm] string groupName,
        [FromForm] string displayHeader,
        [FromForm] string? displayDescription)
    {
        await LoadSchemaAsync();

        if (string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(displayHeader))
        {
            ErrorMessages.Add("Group name and display header are required.");
            return Page();
        }

        if (!AttributeGroupCode.TryCreate(groupName, out var name))
        {
            ErrorMessages.Add($"Invalid group name: '{groupName}'.");
            return Page();
        }

        if (!AttributeDisplayName.TryCreate(displayHeader, out var header))
        {
            ErrorMessages.Add($"Invalid display header: '{displayHeader}'.");
            return Page();
        }

        AttributeDescription? desc = null;
        if (!string.IsNullOrWhiteSpace(displayDescription) && AttributeDescription.TryCreate(displayDescription.Trim(), out var parsedDesc))
        {
            desc = parsedDesc;
        }

        var order = Groups.Count;
        var group = new AttributeGroup(name, header, desc, order);

        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, HttpContext.RequestAborted);
        var schema = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

        schema.Groups.Add(group);

        var saveResult = getResult.Found
            ? await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, HttpContext.RequestAborted)
            : await schemaAdmin.CreateAsync(schema, HttpContext.RequestAborted);

        if (!saveResult.IsSuccess)
        {
            ErrorMessages.Add($"Failed to add group '{groupName}'. It may already exist.");
            return Page();
        }

        Messages.Add($"Group '{groupName}' added.");
        await LoadSchemaAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveGroupAsync([FromForm] string groupName)
    {
        await LoadSchemaAsync();

        if (!AttributeGroupCode.TryCreate(groupName, out var name))
        {
            ErrorMessages.Add($"Invalid group name: '{groupName}'.");
            return Page();
        }

        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, HttpContext.RequestAborted);
        if (!getResult.Found)
        {
            ErrorMessages.Add($"Failed to remove group '{groupName}'. Schema not found.");
            return Page();
        }

        var schema = getResult.Item!;
        var groupToRemove = schema.Groups.FirstOrDefault(g => g.Code == name);
        if (groupToRemove is null || !schema.Groups.Remove(groupToRemove))
        {
            ErrorMessages.Add($"Failed to remove group '{groupName}'.");
            return Page();
        }

        // Ungroup attributes that referenced the removed group
        var toUngroup = schema.AttributeDefinitions.Where(a => a.GroupCode == name).ToList();
        foreach (var attr in toUngroup)
        {
            _ = schema.AttributeDefinitions.Remove(attr);
            schema.AttributeDefinitions.Add(attr with { GroupCode = null });
        }

        var saveResult = await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, HttpContext.RequestAborted);
        if (!saveResult.IsSuccess)
        {
            ErrorMessages.Add($"Failed to remove group '{groupName}'. It may have attributes assigned to it.");
            return Page();
        }

        Messages.Add($"Group '{groupName}' removed.");
        await LoadSchemaAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAttributeAsync(
        [FromForm] string attrName,
        [FromForm] string attrType,
        [FromForm] string? description,
        [FromForm] string? groupName,
        [FromForm] bool isUnique)
    {
        await LoadSchemaAsync();

        if (string.IsNullOrWhiteSpace(attrName) || string.IsNullOrWhiteSpace(attrType))
        {
            ErrorMessages.Add("Attribute name and type are required.");
            return Page();
        }

        if (!AttributeCode.TryCreate(attrName, out var name))
        {
            ErrorMessages.Add($"Invalid attribute name: '{attrName}'.");
            return Page();
        }

        if (!AttributeDescription.TryCreate(description ?? attrName, out var desc))
        {
            ErrorMessages.Add($"Invalid description.");
            return Page();
        }

        AttributeGroupCode? group = null;
        if (!string.IsNullOrWhiteSpace(groupName) && AttributeGroupCode.TryCreate(groupName, out var parsedGroup))
        {
            group = parsedGroup;
        }

        var order = Attributes.Values.Count(a => a.GroupCode?.Value == group?.Value);

        if (!TryParseAttributeType(attrType, out var attributeType))
        {
            ErrorMessages.Add($"Unknown attribute type: '{attrType}'.");
            return Page();
        }

        var definition = new AttributeDefinition
        {
            Code = name,
            AttributeType = attributeType,
            Description = desc,
            IsUnique = isUnique,
            GroupCode = group,
            Order = order
        };

        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, HttpContext.RequestAborted);
        var schema = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

        schema.AttributeDefinitions.Add(definition);

        var saveResult = getResult.Found
            ? await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, HttpContext.RequestAborted)
            : await schemaAdmin.CreateAsync(schema, HttpContext.RequestAborted);

        if (!saveResult.IsSuccess)
        {
            ErrorMessages.Add($"Failed to add attribute '{attrName}'. It may already exist.");
            return Page();
        }

        Messages.Add($"Attribute '{attrName}' added.");
        await LoadSchemaAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAttributeAsync([FromForm] string attrName)
    {
        await LoadSchemaAsync();

        if (!AttributeCode.TryCreate(attrName, out var name))
        {
            ErrorMessages.Add($"Invalid attribute name: '{attrName}'.");
            return Page();
        }

        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, HttpContext.RequestAborted);
        if (!getResult.Found)
        {
            ErrorMessages.Add($"Failed to remove attribute '{attrName}'.");
            return Page();
        }

        var schema = getResult.Item!;
        var attrToRemove = schema.AttributeDefinitions.FirstOrDefault(d => d.Code == name);
        if (attrToRemove is null || !schema.AttributeDefinitions.Remove(attrToRemove))
        {
            ErrorMessages.Add($"Failed to remove attribute '{attrName}'.");
            return Page();
        }

        var saveResult = await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, HttpContext.RequestAborted);
        if (!saveResult.IsSuccess)
        {
            ErrorMessages.Add($"Failed to remove attribute '{attrName}'.");
            return Page();
        }

        Messages.Add($"Attribute '{attrName}' removed.");
        await LoadSchemaAsync();
        return Page();
    }

    private async Task LoadSchemaAsync()
    {
        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, HttpContext.RequestAborted);
        if (getResult.Found)
        {
            Attributes = getResult.Item!.AttributeDefinitions.ToDictionary(d => d.Code);
            Groups = getResult.Item!.Groups.ToDictionary(g => g.Code);
        }
        else
        {
            Attributes = new Dictionary<AttributeCode, AttributeDefinition>();
            Groups = new Dictionary<AttributeGroupCode, AttributeGroup>();
        }
    }

    private static bool TryParseAttributeType(string typeStr, out AttributeType attributeType)
    {
        if (Enum.TryParse<ScalarDataType>(typeStr, ignoreCase: true, out var scalarType))
        {
            attributeType = new ScalarAttributeType(scalarType);
            return true;
        }

        attributeType = null!;
        return false;
    }
}
