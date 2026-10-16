// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public static class SchemaConfigurationTests
{
    [Fact]
    public static void Attribute_definitions_assigned_as_an_array_can_still_be_added_to()
    {
        AttributeDefinition[] definitions = [Attribute("email")];
        var schema = new SchemaConfiguration
        {
            SchemaId = SchemaId.Create("user-profile"),
            AttributeDefinitions = definitions
        };

        schema.AttributeDefinitions.Add(Attribute("department"));

        schema.AttributeDefinitions.Select(a => a.Code.Value).ShouldBe(["email", "department"]);
    }

    [Fact]
    public static void Groups_assigned_as_an_array_can_still_be_added_to()
    {
        AttributeGroup[] groups = [Group("general")];
        var schema = new SchemaConfiguration
        {
            SchemaId = SchemaId.Create("user-profile"),
            Groups = groups
        };

        schema.Groups.Add(Group("personal_info"));

        schema.Groups.Select(g => g.Code.Value).ShouldBe(["general", "personal_info"]);
    }

    [Fact]
    public static void A_new_schema_starts_with_empty_mutable_collections()
    {
        var schema = new SchemaConfiguration { SchemaId = SchemaId.Create("user-profile") };

        schema.AttributeDefinitions.Add(Attribute("email"));
        schema.Groups.Add(Group("general"));

        schema.AttributeDefinitions.Count.ShouldBe(1);
        schema.Groups.Count.ShouldBe(1);
    }

    private static AttributeDefinition Attribute(string code) =>
        new()
        {
            Code = AttributeCode.Create(code),
            AttributeType = new ScalarAttributeType(ScalarDataType.String)
        };

    private static AttributeGroup Group(string code) =>
        new(AttributeGroupCode.Create(code), null, null, 0);
}
