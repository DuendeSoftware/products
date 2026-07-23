// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public static class TypedAttributeDefinitionTests
{
    [Fact]
    public static void constructor_sets_code_and_attribute_type()
    {
        var code = AttributeCode.Create("department");
        var attrType = new ScalarAttributeType(ScalarDataType.String);

        var def = new TypedAttributeDefinition<string>(code, attrType);

        def.Code.ShouldBe(code);
        def.AttributeType.ShouldBe(attrType);
    }

    [Fact]
    public static void constructor_throws_for_null_code() =>
        _ = Should.Throw<ArgumentNullException>(() =>
            new TypedAttributeDefinition<string>(null!, new ScalarAttributeType(ScalarDataType.String)));

    [Fact]
    public static void constructor_throws_for_null_attribute_type() =>
        _ = Should.Throw<ArgumentNullException>(() =>
            new TypedAttributeDefinition<string>(AttributeCode.Create("x"), null!));

    [Fact]
    public static void implicit_cast_to_attribute_definition_preserves_code()
    {
        var code = AttributeCode.Create("dept");
        var typed = new TypedAttributeDefinition<string>(code, new ScalarAttributeType(ScalarDataType.String));

        AttributeDefinition def = typed;

        def.Code.ShouldBe(code);
    }

    [Fact]
    public static void implicit_cast_to_attribute_definition_preserves_type()
    {
        var attrType = new ScalarAttributeType(ScalarDataType.Integer);
        var typed = new TypedAttributeDefinition<int>(AttributeCode.Create("count"), attrType);

        AttributeDefinition def = typed;

        def.AttributeType.ShouldBe(attrType);
    }

    [Fact]
    public static void implicit_cast_throws_for_null()
    {
        TypedAttributeDefinition<string>? nullDef = null;
        _ = Should.Throw<ArgumentNullException>(() =>
        {
            AttributeDefinition _ = nullDef!;
        });
    }

    [Fact]
    public static void set_with_typed_definition_adds_string_attribute()
    {
        var def = new TypedAttributeDefinition<string>(
            AttributeCode.Create("department"),
            new ScalarAttributeType(ScalarDataType.String));

        var collection = new AttributeValueCollection();
        collection.Set(def, "Engineering");

        collection.Count.ShouldBe(1);
        collection[def.Code].UntypedValue.ShouldBe("Engineering");
    }

    [Fact]
    public static void set_with_typed_definition_throws_for_null_definition()
    {
        var collection = new AttributeValueCollection();
        _ = Should.Throw<ArgumentNullException>(() => collection.Set<string>(null!, "value"));
    }

    [Fact]
    public static void set_with_typed_definition_replaces_existing()
    {
        var def = new TypedAttributeDefinition<string>(
            AttributeCode.Create("env"),
            new ScalarAttributeType(ScalarDataType.String));

        var collection = new AttributeValueCollection();
        collection.Set(def, "staging");
        collection.Set(def, "production");

        collection.Count.ShouldBe(1);
        collection[def.Code].UntypedValue.ShouldBe("production");
    }

    [Fact]
    public static void typed_definition_used_in_schema_configuration()
    {
        var dept = new TypedAttributeDefinition<string>(
            AttributeCode.Create("department"),
            new ScalarAttributeType(ScalarDataType.String));

        var config = new SchemaConfiguration
        {
            SchemaId = SchemaId.Create("test"),
            AttributeDefinitions = [dept]
        };

        _ = config.AttributeDefinitions.ShouldHaveSingleItem();
        config.AttributeDefinitions.First().Code.ShouldBe(dept.Code);
    }
}
