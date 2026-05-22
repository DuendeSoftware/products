// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public static class AttributeDefinitions
{
    private static readonly AttributeCode TestName = AttributeCode.Create("test_attr");
    private static readonly AttributeDescription TestDescription = AttributeDescription.Create("A test attribute");

    [Theory]
    [InlineData(ScalarDataType.Boolean)]
    [InlineData(ScalarDataType.Date)]
    [InlineData(ScalarDataType.DateTime)]
    [InlineData(ScalarDataType.Decimal)]
    [InlineData(ScalarDataType.Integer)]
    [InlineData(ScalarDataType.String)]
    public static void object_initializer_with_scalar_type_works(ScalarDataType dataType)
    {
        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = new ScalarAttributeType(dataType),
            Description = TestDescription
        };

        _ = definition.AttributeType.ShouldBeOfType<ScalarAttributeType>();
        ((ScalarAttributeType)definition.AttributeType).DataType.ShouldBe(dataType);
    }

    [Fact]
    public static void object_initializer_with_complex_type_works()
    {
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = complexType,
            Description = TestDescription
        };

        definition.AttributeType.ShouldBe(complexType);
    }

    [Fact]
    public static void tags_default_to_empty_collection()
    {
        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = TestDescription
        };

        definition.Tags.ShouldBeEmpty();
    }

    [Fact]
    public static void scalar_attribute_can_have_group_name()
    {
        var groupName = AttributeGroupCode.Create("personal_info");
        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = TestDescription,
            GroupCode = groupName,
            Order = 0
        };

        definition.GroupCode.ShouldBe(groupName);
    }

    [Fact]
    public static void complex_attribute_can_have_group_name()
    {
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var groupName = AttributeGroupCode.Create("personal_info");

        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = complexType,
            Description = TestDescription,
            GroupCode = groupName,
            Order = 0
        };

        definition.GroupCode.ShouldBe(groupName);
    }

    [Fact]
    public static void list_attribute_can_have_group_name()
    {
        var listType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));
        var groupName = AttributeGroupCode.Create("personal_info");

        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = listType,
            Description = TestDescription,
            GroupCode = groupName,
            Order = 0
        };

        definition.GroupCode.ShouldBe(groupName);
    }

    [Fact]
    public static void group_name_defaults_to_null()
    {
        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = TestDescription
        };

        definition.GroupCode.ShouldBeNull();
    }

    [Fact]
    public static void order_defaults_to_zero()
    {
        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = TestDescription
        };

        definition.Order.ShouldBe(0);
    }

    [Fact]
    public static void order_is_preserved()
    {
        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = TestDescription,
            Order = 42
        };

        definition.Order.ShouldBe(42);
    }

    [Fact]
    public static void indexed_defaults_to_true()
    {
        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = TestDescription
        };

        definition.IsQueryable.ShouldBeTrue();
    }

    [Fact]
    public static void indexed_can_be_set_to_false()
    {
        var definition = new AttributeDefinition
        {
            Code = TestName,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = TestDescription,
            IsQueryable = false
        };

        definition.IsQueryable.ShouldBeFalse();
    }
}
