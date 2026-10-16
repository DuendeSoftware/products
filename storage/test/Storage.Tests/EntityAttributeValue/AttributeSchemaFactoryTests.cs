// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal;

namespace Duende.Storage.EntityAttributeValue;

public sealed class AttributeSchemaFactoryTests
{
    private static readonly AttributeDescription Desc = AttributeDescription.Create("test");

    private static readonly AttributeDefinition Color = new()
    {
        Code = AttributeCode.Create("color"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = Desc
    };

    [Fact]
    public void empty_has_no_definitions_or_groups()
    {
        var schema = AttributeSchemaFactory.Empty;

        schema.AttributeDefinitions.ShouldBeEmpty();
        schema.Groups.ShouldBeEmpty();
    }

    [Fact]
    public void create_with_definitions_exposes_them_without_groups()
    {
        var schema = AttributeSchemaFactory.Create([Color]);

        schema.AttributeDefinitions.Keys.ShouldBe([Color.Code]);
        schema.Groups.ShouldBeEmpty();
    }

    [Fact]
    public void create_with_definitions_and_groups_exposes_both()
    {
        var group = new AttributeGroup(
            AttributeGroupCode.Create("personal_info"),
            AttributeDisplayName.Create("Personal Information"),
            null,
            0);

        var schema = AttributeSchemaFactory.Create([Color], [group]);

        schema.AttributeDefinitions.Keys.ShouldBe([Color.Code]);
        schema.Groups.Keys.ShouldBe([group.Code]);
    }

    [Fact]
    public void created_schema_dictionaries_cannot_be_mutated_through_a_cast()
    {
        var schema = AttributeSchemaFactory.Create([Color]);

        _ = Should.Throw<NotSupportedException>(() =>
            ((IDictionary<AttributeCode, AttributeDefinition>)schema.AttributeDefinitions).Remove(Color.Code));
        schema.AttributeDefinitions.Keys.ShouldBe([Color.Code]);
    }

    [Fact]
    public void empty_schema_cannot_be_mutated_through_a_cast()
    {
        _ = Should.Throw<NotSupportedException>(() =>
            ((IDictionary<AttributeCode, AttributeDefinition>)AttributeSchemaFactory.Empty.AttributeDefinitions).Add(Color.Code, Color));

        AttributeSchemaFactory.Empty.AttributeDefinitions.ShouldBeEmpty();
    }

    [Fact]
    public void created_schema_supports_validation()
    {
        var schema = AttributeSchemaFactory.Create([Color]);
        var collection = new AttributeValueCollection(schema);
        collection.Set(Color.Code, "blue");

        collection.TryValidate(out var validated, out var errors).ShouldBeTrue(string.Join("; ", errors ?? []));
        _ = validated.ShouldNotBeNull();
    }
}
