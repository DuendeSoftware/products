// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public static class SchemaConfigurationExtendTests
{
    [Fact]
    public static void Extending_a_schema_adds_the_new_attribute_and_keeps_the_existing_ones()
    {
        var schema = MakeSchema("email");

        var extended = schema.Extend(Attribute("department"));

        extended.AttributeDefinitions.Select(a => a.Code).ShouldBe(
            [AttributeCode.Create("email"), AttributeCode.Create("department")],
            ignoreOrder: true);
    }

    [Fact]
    public static void Extending_a_schema_leaves_the_original_schema_unchanged()
    {
        var schema = MakeSchema("email");

        _ = schema.Extend(Attribute("department"));

        schema.AttributeDefinitions.Select(a => a.Code).ShouldBe([AttributeCode.Create("email")]);
    }

    [Fact]
    public static void Extending_a_schema_with_an_attribute_it_already_has_fails_and_names_the_attribute()
    {
        var schema = MakeSchema("email");

        var ex = Should.Throw<InvalidOperationException>(() => schema.Extend(Attribute("email")));

        ex.Message.ShouldContain("email");
    }

    [Fact]
    public static void Extending_a_schema_with_the_same_new_attribute_twice_fails_and_names_the_attribute()
    {
        var schema = MakeSchema("email");

        var ex = Should.Throw<InvalidOperationException>(() => schema.Extend(Attribute("department"), Attribute("department")));

        ex.Message.ShouldContain("department");
    }

    [Fact]
    public static void Extending_a_schema_with_a_new_group_adds_the_group()
    {
        var schema = MakeSchema("email");
        var group = new AttributeGroup(AttributeGroupCode.Create("personal_info"), null, null, 0);

        var extended = schema.Extend([Attribute("department")], [group]);

        extended.Groups.ShouldContain(group);
    }

    [Fact]
    public static void Extending_a_schema_with_a_group_it_already_has_fails_and_names_the_group()
    {
        var group = AttributeGroupCode.Create("personal_info");
        var schema = new SchemaConfiguration
        {
            SchemaId = SchemaId.Create("user-profile"),
            Groups = [new AttributeGroup(group, null, null, 0)]
        };

        var ex = Should.Throw<InvalidOperationException>(() => schema.Extend([], [new AttributeGroup(group, null, null, 1)]));

        ex.Message.ShouldContain("personal_info");
    }

    private static AttributeDefinition Attribute(string code) =>
        new()
        {
            Code = AttributeCode.Create(code),
            AttributeType = new ScalarAttributeType(ScalarDataType.String)
        };

    private static SchemaConfiguration MakeSchema(params string[] attributeCodes) =>
        new()
        {
            SchemaId = SchemaId.Create("user-profile"),
            DisplayName = "User Profile",
            AttributeDefinitions = attributeCodes.Select(Attribute).ToList()
        };
}
