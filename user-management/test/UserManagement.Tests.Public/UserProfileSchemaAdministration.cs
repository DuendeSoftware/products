// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace Duende.Platform.UserManagement;

public sealed class UserProfileSchemaAdministration : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private ISchemaAdmin _admin = null!;
    private ServiceProvider _serviceProvider = null!;

    public static TheoryData<SerializableDefinition> AttributeDefinitions { get; } =
        [.. TestData.CreateAttributeDefinitions().Concat(TestData.CreateNonScalarAttributeDefinitions()).Select(SerializableDefinition.From)];

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _admin = _serviceProvider.GetRequiredService<ISchemaAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private async Task<(SchemaConfiguration Schema, int Version)> GetOrCreateSchemaAsync()
    {
        var getResult = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        if (getResult.Found)
        {
            return (getResult.Item!, getResult.Version!.Value);
        }

        var schema = new SchemaConfiguration { SchemaId = SchemaId.UserProfile };
        var createResult = await _admin.CreateAsync(schema, _ct);
        createResult.IsSuccess.ShouldBeTrue();
        var freshGet = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        return (freshGet.Item!, freshGet.Version!.Value);
    }

    private async Task<bool> TryAddDefinitionAsync(AttributeDefinition definition)
    {
        var getResult = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        var schema = getResult.Found ? getResult.Item! : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };
        if (schema.AttributeDefinitions.Any(d => d.Code == definition.Code))
        {
            return false;
        }

        schema.AttributeDefinitions.Add(definition);
        var saveResult = getResult.Found
            ? await _admin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, _ct)
            : await _admin.CreateAsync(schema, _ct);
        return saveResult.IsSuccess;
    }

    private async Task<bool> TryRemoveDefinitionAsync(AttributeCode code)
    {
        var getResult = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        if (!getResult.Found)
        {
            return true;
        }

        var schema = getResult.Item!;
        var toRemove = schema.AttributeDefinitions.FirstOrDefault(d => d.Code == code);
        if (toRemove is not null)
        {
            _ = schema.AttributeDefinitions.Remove(toRemove);
        }

        var saveResult = await _admin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, _ct);
        return saveResult.IsSuccess;
    }

    private async Task<IReadOnlyDictionary<AttributeCode, AttributeDefinition>> GetAllDefinitionsAsync()
    {
        var getResult = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        if (!getResult.Found)
        {
            return new Dictionary<AttributeCode, AttributeDefinition>();
        }

        return getResult.Item!.AttributeDefinitions.ToDictionary(d => d.Code, d => d);
    }

    private async Task<bool> TryAddGroupAsync(AttributeGroup group)
    {
        var getResult = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        var schema = getResult.Found ? getResult.Item! : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };
        if (schema.Groups.Any(g => g.Code == group.Code))
        {
            return false;
        }

        schema.Groups.Add(group);
        var saveResult = getResult.Found
            ? await _admin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, _ct)
            : await _admin.CreateAsync(schema, _ct);
        return saveResult.IsSuccess;
    }

    private async Task<bool> TryRemoveGroupAsync(AttributeGroupCode code)
    {
        var getResult = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        if (!getResult.Found)
        {
            return true;
        }

        var schema = getResult.Item!;
        var toRemove = schema.Groups.FirstOrDefault(g => g.Code == code);
        if (toRemove is not null)
        {
            _ = schema.Groups.Remove(toRemove);
        }

        // Ungroup any attributes that belonged to this group - replace with new instances
        var toUpdate = schema.AttributeDefinitions.Where(a => a.GroupCode == code).ToList();
        foreach (var attr in toUpdate)
        {
            _ = schema.AttributeDefinitions.Remove(attr);
            schema.AttributeDefinitions.Add(attr with { GroupCode = null });
        }

        var saveResult = await _admin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, _ct);
        return saveResult.IsSuccess;
    }

    private async Task<IReadOnlyDictionary<AttributeGroupCode, AttributeGroup>> GetAllGroupsAsync()
    {
        var getResult = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        if (!getResult.Found)
        {
            return new Dictionary<AttributeGroupCode, AttributeGroup>();
        }

        return getResult.Item!.Groups.ToDictionary(g => g.Code, g => g);
    }

    private async Task<bool> ReorderAttributesAsync(AttributeGroupCode groupCode, IReadOnlyList<AttributeDefinition> ordered)
    {
        var getResult = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        if (!getResult.Found)
        {
            return false;
        }

        var schema = getResult.Item!;
        for (var i = 0; i < ordered.Count; i++)
        {
            var existing = schema.AttributeDefinitions.First(a => a.Code == ordered[i].Code);
            _ = schema.AttributeDefinitions.Remove(existing);
            schema.AttributeDefinitions.Add(existing with { Order = i });
        }

        var saveResult = await _admin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, _ct);
        return saveResult.IsSuccess;
    }

    private async Task<bool> ReorderGroupsAsync(IReadOnlyList<AttributeGroupCode> ordered)
    {
        var getResult = await _admin.GetAsync(SchemaId.UserProfile, _ct);
        if (!getResult.Found)
        {
            return false;
        }

        var schema = getResult.Item!;
        for (var i = 0; i < ordered.Count; i++)
        {
            var group = schema.Groups.First(g => g.Code == ordered[i]);
            group = group with { Order = i };
            var existing = schema.Groups.First(g => g.Code == ordered[i]);
            _ = schema.Groups.Remove(existing);
            schema.Groups.Add(group);
        }

        var saveResult = await _admin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, _ct);
        return saveResult.IsSuccess;
    }

    [Theory]
    [MemberData(nameof(AttributeDefinitions))]
    public async Task can_add_attribute_definitions(SerializableDefinition definition)
    {
        var added = await TryAddDefinitionAsync(definition.Definition);

        added.ShouldBeTrue();
        var allDefs = await GetAllDefinitionsAsync();
        var actual = allDefs.ShouldHaveSingleItem().Value;
        actual.Code.ShouldBe(definition.Definition.Code);
        actual.AttributeType.ShouldBe(definition.Definition.AttributeType);
        actual.Description.ShouldBe(definition.Definition.Description);
        actual.IsUnique.ShouldBe(definition.Definition.IsUnique);
        actual.Tags.ShouldBe(definition.Definition.Tags);
    }

    [Fact]
    public async Task cannot_add_attribute_definitions_twice()
    {
        var definition = TestData.CreateAttributeDefinitions().First();
        (await TryAddDefinitionAsync(definition)).ShouldBeTrue();

        var added = await TryAddDefinitionAsync(definition);

        added.ShouldBeFalse();
    }

    [Fact]
    public async Task can_remove_attribute_definitions()
    {
        var definition = TestData.CreateAttributeDefinitions().First();
        (await TryAddDefinitionAsync(definition)).ShouldBeTrue();

        var removed = await TryRemoveDefinitionAsync(definition.Code);

        removed.ShouldBeTrue();
        (await GetAllDefinitionsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task can_remove_attributes_when_no_schema_exists()
    {
        var name = TestData.CreateAttributeDefinitions().First().Code;

        var removed = await TryRemoveDefinitionAsync(name);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task can_add_group()
    {
        var group = new AttributeGroup(
            AttributeGroupCode.Create("personal_info"),
            AttributeDisplayName.Create("Personal Information"),
            null,
            0);

        var added = await TryAddGroupAsync(group);

        added.ShouldBeTrue();
        var groups = await GetAllGroupsAsync();
        groups.ShouldContainKey(group.Code);
        groups[group.Code].DisplayName.ShouldBe(group.DisplayName);
    }

    [Fact]
    public async Task cannot_add_group_twice()
    {
        var group = new AttributeGroup(
            AttributeGroupCode.Create("personal_info"),
            AttributeDisplayName.Create("Personal Information"),
            null,
            0);
        (await TryAddGroupAsync(group)).ShouldBeTrue();

        var added = await TryAddGroupAsync(group);

        added.ShouldBeFalse();
    }

    [Fact]
    public async Task can_remove_group()
    {
        var group = new AttributeGroup(
            AttributeGroupCode.Create("personal_info"),
            AttributeDisplayName.Create("Personal Information"),
            null,
            0);
        (await TryAddGroupAsync(group)).ShouldBeTrue();

        var removed = await TryRemoveGroupAsync(group.Code);

        removed.ShouldBeTrue();
        (await GetAllGroupsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task removing_group_ungroups_its_attributes()
    {
        var groupName = AttributeGroupCode.Create("personal_info");
        var group = new AttributeGroup(groupName, AttributeDisplayName.Create("Personal Information"), null, 0);
        (await TryAddGroupAsync(group)).ShouldBeTrue();

        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("first_name"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("First name"),
            GroupCode = groupName,
            Order = 0
        };
        (await TryAddDefinitionAsync(definition)).ShouldBeTrue();

        (await TryRemoveGroupAsync(groupName)).ShouldBeTrue();

        var attrs = await GetAllDefinitionsAsync();
        attrs[definition.Code].GroupCode.ShouldBeNull();
    }

    [Fact]
    public async Task can_reorder_attributes_within_group()
    {
        var groupName = AttributeGroupCode.Create("personal_info");
        var group = new AttributeGroup(groupName, AttributeDisplayName.Create("Personal Information"), null, 0);
        (await TryAddGroupAsync(group)).ShouldBeTrue();

        var first = new AttributeDefinition { Code = AttributeCode.Create("first_name"), AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("First"), GroupCode = groupName, Order = 0 };
        var last = new AttributeDefinition { Code = AttributeCode.Create("last_name"), AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("Last"), GroupCode = groupName, Order = 1 };
        (await TryAddDefinitionAsync(first)).ShouldBeTrue();
        (await TryAddDefinitionAsync(last)).ShouldBeTrue();

        // Reverse order
        (await ReorderAttributesAsync(groupName, [last, first])).ShouldBeTrue();

        var attrs = await GetAllDefinitionsAsync();
        attrs[last.Code].Order.ShouldBe(0);
        attrs[first.Code].Order.ShouldBe(1);
    }

    [Fact]
    public async Task reorder_preserves_indexed_flag()
    {
        var groupName = AttributeGroupCode.Create("settings");
        var group = new AttributeGroup(groupName, AttributeDisplayName.Create("Settings"), null, 0);
        (await TryAddGroupAsync(group)).ShouldBeTrue();

        var indexed = new AttributeDefinition
        {
            Code = AttributeCode.Create("visible"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("Visible attr"),
            GroupCode = groupName,
            Order = 0
        };

        var nonIndexed = new AttributeDefinition
        {
            Code = AttributeCode.Create("hidden"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("Hidden attr"),
            GroupCode = groupName,
            Order = 1,
            IsQueryable = false
        };

        (await TryAddDefinitionAsync(indexed)).ShouldBeTrue();
        (await TryAddDefinitionAsync(nonIndexed)).ShouldBeTrue();

        // Reorder: this should not change the IsQueryable flag
        (await ReorderAttributesAsync(groupName, [nonIndexed, indexed])).ShouldBeTrue();

        var attrs = await GetAllDefinitionsAsync();
        attrs[indexed.Code].IsQueryable.ShouldBeTrue();
        attrs[nonIndexed.Code].IsQueryable.ShouldBeFalse();
    }

    [Fact]
    public async Task can_reorder_groups()
    {
        var nameA = AttributeGroupCode.Create("group_a");
        var nameB = AttributeGroupCode.Create("group_b");
        var groupA = new AttributeGroup(nameA, AttributeDisplayName.Create("Group A"), null, 0);
        var groupB = new AttributeGroup(nameB, AttributeDisplayName.Create("Group B"), null, 1);
        (await TryAddGroupAsync(groupA)).ShouldBeTrue();
        (await TryAddGroupAsync(groupB)).ShouldBeTrue();

        // Reverse order
        (await ReorderGroupsAsync([nameB, nameA])).ShouldBeTrue();

        var groups = await GetAllGroupsAsync();
        groups[nameB].Order.ShouldBe(0);
        groups[nameA].Order.ShouldBe(1);
    }

    public sealed class SerializableDefinition : IXunitSerializable
    {
        public required AttributeDefinition Definition { get; set; }

        public void Serialize(IXunitSerializationInfo info)
        {
            var kind = GetAttributeTypeKind(Definition.AttributeType);
            info.AddValue(nameof(Definition.Code), Definition.Code.ToString());
            info.AddValue("TypeKind", kind);
            info.AddValue("TypeJson", SerializeAttributeType(Definition.AttributeType));
            info.AddValue(nameof(Definition.Description), Definition.Description?.ToString());
            info.AddValue(nameof(Definition.IsUnique), Definition.IsUnique);
        }

        public void Deserialize(IXunitSerializationInfo info) =>
            Definition = new AttributeDefinition
            {
                Code = AttributeCode.Create(info.GetValue<string>(nameof(Definition.Code))!),
                AttributeType = DeserializeAttributeType(
                    info.GetValue<string>("TypeKind")!,
                    info.GetValue<string>("TypeJson")!),
                Description = AttributeDescription.Create(info.GetValue<string>(nameof(Definition.Description))!),
                IsUnique = info.GetValue<bool>(nameof(Definition.IsUnique))
            };

        private static string GetAttributeTypeKind(AttributeType type) =>
            type switch
            {
                ScalarAttributeType => "Scalar",
                ComplexAttributeType => "Complex",
                ListAttributeType => "List",
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };

        private static string SerializeAttributeType(AttributeType type) =>
            type switch
            {
                ScalarAttributeType scalar => scalar.DataType.ToString(),
                ComplexAttributeType complexType =>
                    JsonSerializer.Serialize(complexType.Properties.ToDictionary(
                        p => p.Key.Value,
                        p => SerializeAttributeTypeAsObject(p.Value.Type))),
                ListAttributeType listType =>
                    JsonSerializer.Serialize(SerializeAttributeTypeAsObject(listType.ElementType)),
                _ => throw new NotSupportedException($"Serialization not supported for type: {type.GetType().Name}")
            };

        private static Dictionary<string, object?> SerializeAttributeTypeAsObject(AttributeType type) =>
            new()
            {
                ["kind"] = GetAttributeTypeKind(type),
                ["json"] = SerializeAttributeType(type)
            };

        private static AttributeType DeserializeAttributeType(string kind, string json) =>
            kind switch
            {
                "Scalar" => new ScalarAttributeType(Enum.Parse<ScalarDataType>(json)),
                "Complex" => new ComplexAttributeType(
                    JsonSerializer.Deserialize<JsonElement>(json)
                        .EnumerateObject()
                        .ToDictionary(
                            p => AttributeCode.Create(p.Name),
                            p => ComplexAttributeProperty.Of(DeserializeAttributeTypeFromObject(p.Value)))),
                "List" => new ListAttributeType(
                    DeserializeAttributeTypeFromObject(JsonSerializer.Deserialize<JsonElement>(json))),
                _ => throw new NotSupportedException($"Deserialization not supported for kind: {kind}")
            };

        private static AttributeType DeserializeAttributeTypeFromObject(JsonElement element) =>
            DeserializeAttributeType(
                element.GetProperty("kind").GetString()!,
                element.GetProperty("json").GetString()!);

        internal static SerializableDefinition From(AttributeDefinition definition) => new() { Definition = definition };
    }
}
