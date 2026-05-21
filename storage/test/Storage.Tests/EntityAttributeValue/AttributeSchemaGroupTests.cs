// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal;

namespace Duende.Storage.EntityAttributeValue;

public sealed class AttributeSchemaGroupTests
{
    private static readonly AttributeDescription Desc = AttributeDescription.Create("test");

    [Fact]
    public void add_group_stores_code_and_display_name()
    {
        var schema = new AttributeSchema();
        var group = new AttributeGroup(
            AttributeGroupCode.Create("personal_info"),
            AttributeDisplayName.Create("Personal Information"),
            null,
            0);

        schema.AddGroup(group).ShouldBeTrue();

        schema.Groups.ShouldContainKey(group.Code);
        schema.Groups[group.Code].DisplayName.ShouldBe(group.DisplayName);
    }

    [Fact]
    public void add_group_stores_description()
    {
        var schema = new AttributeSchema();
        var group = new AttributeGroup(
            AttributeGroupCode.Create("personal_info"),
            AttributeDisplayName.Create("Personal Information"),
            AttributeDescription.Create("Personal details"),
            0);

        schema.AddGroup(group).ShouldBeTrue();

        schema.Groups[group.Code].Description.ShouldBe(group.Description);
    }

    [Fact]
    public void add_group_stores_order()
    {
        var schema = new AttributeSchema();
        var group = new AttributeGroup(
            AttributeGroupCode.Create("personal_info"),
            null,
            null,
            42);

        schema.AddGroup(group).ShouldBeTrue();

        schema.Groups[group.Code].Order.ShouldBe(42);
    }

    [Fact]
    public void add_group_with_null_display_name_and_description()
    {
        var schema = new AttributeSchema();
        var group = new AttributeGroup(
            AttributeGroupCode.Create("personal_info"),
            null,
            null,
            0);

        schema.AddGroup(group).ShouldBeTrue();

        schema.Groups[group.Code].DisplayName.ShouldBeNull();
        schema.Groups[group.Code].Description.ShouldBeNull();
    }

    [Fact]
    public void cannot_add_duplicate_group()
    {
        var schema = new AttributeSchema();
        var group = new AttributeGroup(
            AttributeGroupCode.Create("personal_info"),
            AttributeDisplayName.Create("Personal Information"),
            null,
            0);

        schema.AddGroup(group).ShouldBeTrue();
        schema.AddGroup(group).ShouldBeFalse();
    }

    [Fact]
    public void remove_group_returns_true_when_exists()
    {
        var schema = new AttributeSchema();
        var code = AttributeGroupCode.Create("personal_info");
        var group = new AttributeGroup(code, null, null, 0);
        _ = schema.AddGroup(group);

        schema.RemoveGroup(code).ShouldBeTrue();

        schema.Groups.ShouldBeEmpty();
    }

    [Fact]
    public void remove_group_returns_false_when_not_found()
    {
        var schema = new AttributeSchema();

        schema.RemoveGroup(AttributeGroupCode.Create("nonexistent")).ShouldBeFalse();
    }

    [Fact]
    public void remove_group_ungroups_member_attributes()
    {
        var schema = new AttributeSchema();
        var groupCode = AttributeGroupCode.Create("personal_info");
        var group = new AttributeGroup(groupCode, AttributeDisplayName.Create("Personal"), null, 0);
        _ = schema.AddGroup(group);

        var definition = new AttributeDefinition(
            AttributeCode.Create("first_name"),
            ScalarDataType.String,
            Desc,
            false,
            null,
            groupCode,
            0);
        _ = schema.AddAttributeDefinition(definition);

        schema.RemoveGroup(groupCode).ShouldBeTrue();

        schema.AttributeDefinitions[definition].GroupCode.ShouldBeNull();
    }

    [Fact]
    public void remove_group_preserves_ungrouped_attributes()
    {
        var schema = new AttributeSchema();
        var groupCode = AttributeGroupCode.Create("personal_info");
        var group = new AttributeGroup(groupCode, null, null, 0);
        _ = schema.AddGroup(group);

        var grouped = new AttributeDefinition(
            AttributeCode.Create("first_name"),
            ScalarDataType.String,
            Desc,
            false,
            null,
            groupCode,
            0);
        var ungrouped = new AttributeDefinition(
            AttributeCode.Create("email"),
            ScalarDataType.String,
            Desc);
        _ = schema.AddAttributeDefinition(grouped);
        _ = schema.AddAttributeDefinition(ungrouped);

        _ = schema.RemoveGroup(groupCode);

        schema.AttributeDefinitions[ungrouped].GroupCode.ShouldBeNull();
        schema.AttributeDefinitions.Count.ShouldBe(2);
    }

    [Fact]
    public void group_dso_round_trip_preserves_display_name_and_description()
    {
        var schema = new AttributeSchema();
        var group = new AttributeGroup(
            AttributeGroupCode.Create("contact"),
            AttributeDisplayName.Create("Contact Info"),
            AttributeDescription.Create("Contact details"),
            5);
        _ = schema.AddGroup(group);

        var definition = new AttributeDefinition(
            AttributeCode.Create("phone"),
            ScalarDataType.String,
            Desc,
            false,
            null,
            group.Code,
            1);
        _ = schema.AddAttributeDefinition(definition);

        // Verify the in-memory state is correct
        var loaded = schema.Groups[group.Code];
        loaded.Code.ShouldBe(group.Code);
        loaded.DisplayName.ShouldBe(group.DisplayName);
        loaded.Description.ShouldBe(group.Description);
        loaded.Order.ShouldBe(5);
    }
}
