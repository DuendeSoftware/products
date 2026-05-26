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
    private IUserProfileSchemaAdmin _admin = null!;
    private ServiceProvider _serviceProvider = null!;

    public static TheoryData<SerializableDefinition> AttributeDefinitions { get; } =
        [.. TestData.CreateAttributeDefinitions().Concat(TestData.CreateNonScalarAttributeDefinitions()).Select(SerializableDefinition.From)];

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _admin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Theory]
    [MemberData(nameof(AttributeDefinitions))]
    public async Task can_add_attribute_definitions(SerializableDefinition definition)
    {
        var added = await _admin.TryAddAttributeDefinitionAsync(definition.Definition, _ct);

        added.ShouldBeTrue();
        var actual = (await _admin.GetAllAttributeDefinitionsAsync(_ct)).ShouldHaveSingleItem().Value;
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
        (await _admin.TryAddAttributeDefinitionAsync(definition, _ct)).ShouldBeTrue();

        var added = await _admin.TryAddAttributeDefinitionAsync(definition, _ct);

        added.ShouldBeFalse();
    }

    [Fact]
    public async Task can_remove_attribute_definitions()
    {
        var definition = TestData.CreateAttributeDefinitions().First();
        (await _admin.TryAddAttributeDefinitionAsync(definition, _ct)).ShouldBeTrue();

        var removed = await _admin.TryRemoveAttributeDefinitionAsync(definition, _ct);

        removed.ShouldBeTrue();
        (await _admin.GetAllAttributeDefinitionsAsync(_ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task can_remove_attributes_when_no_schema_exists()
    {
        var name = TestData.CreateAttributeDefinitions().First().Code;

        var removed = await _admin.TryRemoveAttributeDefinitionAsync(name, _ct);

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

        var added = await _admin.TryAddGroupAsync(group, _ct);

        added.ShouldBeTrue();
        var groups = await _admin.GetAllGroupsAsync(_ct);
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
        (await _admin.TryAddGroupAsync(group, _ct)).ShouldBeTrue();

        var added = await _admin.TryAddGroupAsync(group, _ct);

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
        (await _admin.TryAddGroupAsync(group, _ct)).ShouldBeTrue();

        var removed = await _admin.TryRemoveGroupAsync(group.Code, _ct);

        removed.ShouldBeTrue();
        (await _admin.GetAllGroupsAsync(_ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task removing_group_ungroups_its_attributes()
    {
        var groupName = AttributeGroupCode.Create("personal_info");
        var group = new AttributeGroup(groupName, AttributeDisplayName.Create("Personal Information"), null, 0);
        (await _admin.TryAddGroupAsync(group, _ct)).ShouldBeTrue();

        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("first_name"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("First name"),
            GroupCode = groupName,
            Order = 0
        };
        (await _admin.TryAddAttributeDefinitionAsync(definition, _ct)).ShouldBeTrue();

        (await _admin.TryRemoveGroupAsync(groupName, _ct)).ShouldBeTrue();

        var attrs = await _admin.GetAllAttributeDefinitionsAsync(_ct);
        attrs[definition].GroupCode.ShouldBeNull();
    }

    [Fact]
    public async Task can_reorder_attributes_within_group()
    {
        var groupName = AttributeGroupCode.Create("personal_info");
        var group = new AttributeGroup(groupName, AttributeDisplayName.Create("Personal Information"), null, 0);
        (await _admin.TryAddGroupAsync(group, _ct)).ShouldBeTrue();

        var first = new AttributeDefinition { Code = AttributeCode.Create("first_name"), AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("First"), GroupCode = groupName, Order = 0 };
        var last = new AttributeDefinition { Code = AttributeCode.Create("last_name"), AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("Last"), GroupCode = groupName, Order = 1 };
        (await _admin.TryAddAttributeDefinitionAsync(first, _ct)).ShouldBeTrue();
        (await _admin.TryAddAttributeDefinitionAsync(last, _ct)).ShouldBeTrue();

        // Reverse order
        (await _admin.ReorderAttributesAsync(groupName, [last, first], _ct)).ShouldBeTrue();

        var attrs = await _admin.GetAllAttributeDefinitionsAsync(_ct);
        attrs[last].Order.ShouldBe(0);
        attrs[first].Order.ShouldBe(1);
    }

    [Fact]
    public async Task reorder_preserves_indexed_flag()
    {
        var groupName = AttributeGroupCode.Create("settings");
        var group = new AttributeGroup(groupName, AttributeDisplayName.Create("Settings"), null, 0);
        (await _admin.TryAddGroupAsync(group, _ct)).ShouldBeTrue();

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

        (await _admin.TryAddAttributeDefinitionAsync(indexed, _ct)).ShouldBeTrue();
        (await _admin.TryAddAttributeDefinitionAsync(nonIndexed, _ct)).ShouldBeTrue();

        // Reorder: this should not change the IsQueryable flag
        (await _admin.ReorderAttributesAsync(groupName, [nonIndexed, indexed], _ct)).ShouldBeTrue();

        var attrs = await _admin.GetAllAttributeDefinitionsAsync(_ct);
        attrs[indexed].IsQueryable.ShouldBeTrue();
        attrs[nonIndexed].IsQueryable.ShouldBeFalse();
    }

    [Fact]
    public async Task can_reorder_groups()
    {
        var nameA = AttributeGroupCode.Create("group_a");
        var nameB = AttributeGroupCode.Create("group_b");
        var groupA = new AttributeGroup(nameA, AttributeDisplayName.Create("Group A"), null, 0);
        var groupB = new AttributeGroup(nameB, AttributeDisplayName.Create("Group B"), null, 1);
        (await _admin.TryAddGroupAsync(groupA, _ct)).ShouldBeTrue();
        (await _admin.TryAddGroupAsync(groupB, _ct)).ShouldBeTrue();

        // Reverse order
        (await _admin.ReorderGroupsAsync([nameB, nameA], _ct)).ShouldBeTrue();

        var groups = await _admin.GetAllGroupsAsync(_ct);
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
