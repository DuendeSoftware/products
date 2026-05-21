// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using Duende.Storage.EntityAttributeValue.Internal;

namespace Duende.Storage.EntityAttributeValue;

public sealed class AttributeSchemaCreationTests
{
    private static readonly AttributeDescription Desc = AttributeDescription.Create("test");

    private static AttributeSchema SchemaWith(AttributeDefinition definition)
    {
        var schema = new AttributeSchema();
        _ = schema.AddAttributeDefinition(definition);
        return schema;
    }

    [Fact]
    public void complex_matching_properties_succeeds()
    {
        var name = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var schema = SchemaWith(new AttributeDefinition(name, complexType, Desc));

        var value = new Dictionary<string, object> { ["city"] = "Seattle", ["zip"] = "98101" };
        var attr = schema.CreateAttribute(name, value);

        _ = attr.UntypedValue.ShouldBeOfType<ReadOnlyDictionary<string, object>>();
    }

    [Fact]
    public void complex_partial_object_is_allowed()
    {
        // Properties are optional — partial objects are valid
        var name = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var schema = SchemaWith(new AttributeDefinition(name, complexType, Desc));

        var value = new Dictionary<string, object> { ["city"] = "Seattle" }; // zip omitted
        var result = schema.TryCreateAttribute(name, value, out _);

        result.ShouldBeTrue();
    }

    [Fact]
    public void complex_extra_unknown_property_fails()
    {
        var name = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var schema = SchemaWith(new AttributeDefinition(name, complexType, Desc));

        var value = new Dictionary<string, object> { ["city"] = "Seattle", ["country"] = "US" };
        var result = schema.TryCreateAttribute(name, value, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void complex_wrong_sub_property_type_fails()
    {
        var name = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.Integer)
        });
        var schema = SchemaWith(new AttributeDefinition(name, complexType, Desc));

        var value = new Dictionary<string, object> { ["zip"] = "not-an-int" }; // string instead of int
        var result = schema.TryCreateAttribute(name, value, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void list_of_strings_succeeds()
    {
        var name = AttributeCode.Create("tags");
        var listType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));
        var schema = SchemaWith(new AttributeDefinition(name, listType, Desc));

        var value = new List<object> { "admin", "power" };
        var attr = schema.CreateAttribute(name, value);

        _ = attr.UntypedValue.ShouldBeOfType<ReadOnlyCollection<object>>();
    }

    [Fact]
    public void list_of_complex_succeeds()
    {
        var name = AttributeCode.Create("phones");
        var listType = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("number")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        }));
        var schema = SchemaWith(new AttributeDefinition(name, listType, Desc));

        var value = new List<object>
        {
            new Dictionary<string, object> { ["number"] = "555-1234", ["type"] = "mobile" },
            new Dictionary<string, object> { ["number"] = "555-9999", ["type"] = "home"},
        };
        var attr = schema.CreateAttribute(name, value);

        _ = attr.UntypedValue.ShouldBeOfType<ReadOnlyCollection<object>>();

        attr.TypedValue.Count.ShouldBe(2);
        ((Dictionary<string, object>)attr.TypedValue[0])["number"].ShouldBe("555-1234");
        ((Dictionary<string, object>)attr.TypedValue[^1])["type"].ShouldBe("home");
    }

    [Fact]
    public void empty_list_succeeds()
    {
        var name = AttributeCode.Create("tags");
        var listType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));
        var schema = SchemaWith(new AttributeDefinition(name, listType, Desc));

        var value = new List<object>();
        var result = schema.TryCreateAttribute(name, value, out _);

        result.ShouldBeTrue();
    }

    // --- Negative / edge-case tests ---

    [Fact]
    public void unknown_attribute_name_fails_for_bool()
    {
        var schema = SchemaWith(new AttributeDefinition(
            AttributeCode.Create("active"), new ScalarAttributeType(ScalarDataType.Boolean), Desc));

        var result = schema.TryCreateAttribute(AttributeCode.Create("missing"), true, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_string()
    {
        var schema = SchemaWith(new AttributeDefinition(
            AttributeCode.Create("color"), new ScalarAttributeType(ScalarDataType.String), Desc));

        var result = schema.TryCreateAttribute(AttributeCode.Create("missing"), "value", out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_int()
    {
        var schema = SchemaWith(new AttributeDefinition(
            AttributeCode.Create("age"), new ScalarAttributeType(ScalarDataType.Integer), Desc));

        var result = schema.TryCreateAttribute(AttributeCode.Create("missing"), 42, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_decimal()
    {
        var schema = SchemaWith(new AttributeDefinition(
            AttributeCode.Create("balance"), new ScalarAttributeType(ScalarDataType.Decimal), Desc));

        var result = schema.TryCreateAttribute(AttributeCode.Create("missing"), 3.14m, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_date()
    {
        var schema = SchemaWith(new AttributeDefinition(
            AttributeCode.Create("birthdate"), new ScalarAttributeType(ScalarDataType.Date), Desc));

        var result = schema.TryCreateAttribute(AttributeCode.Create("missing"), new DateOnly(2000, 1, 1), out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_date_time()
    {
        var schema = SchemaWith(new AttributeDefinition(
            AttributeCode.Create("recordedat"), new ScalarAttributeType(ScalarDataType.DateTime), Desc));

        var result = schema.TryCreateAttribute(AttributeCode.Create("missing"), DateTimeOffset.UtcNow, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_complex()
    {
        var schema = SchemaWith(new AttributeDefinition(
            AttributeCode.Create("address"),
            new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            }),
            Desc));

        var value = new Dictionary<string, object> { ["city"] = "Seattle" };
        var result = schema.TryCreateAttribute(AttributeCode.Create("missing"), value, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_list()
    {
        var schema = SchemaWith(new AttributeDefinition(
            AttributeCode.Create("tags"),
            new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)),
            Desc));

        var value = new List<object> { "a", "b" };
        var result = schema.TryCreateAttribute(AttributeCode.Create("missing"), value, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_string_instead_of_bool_fails()
    {
        var name = AttributeCode.Create("active");
        var schema = SchemaWith(new AttributeDefinition(name, new ScalarAttributeType(ScalarDataType.Boolean), Desc));

        // Attempt to create a string attribute for a boolean definition
        var result = schema.TryCreateAttribute(name, "true", out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_bool_instead_of_string_fails()
    {
        var name = AttributeCode.Create("color");
        var schema = SchemaWith(new AttributeDefinition(name, new ScalarAttributeType(ScalarDataType.String), Desc));

        // Attempt to create a bool attribute for a string definition
        var result = schema.TryCreateAttribute(name, true, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_int_instead_of_decimal_fails()
    {
        var name = AttributeCode.Create("balance");
        var schema = SchemaWith(new AttributeDefinition(name, new ScalarAttributeType(ScalarDataType.Decimal), Desc));

        // Attempt to create an int attribute for a decimal definition
        var result = schema.TryCreateAttribute(name, 42, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_scalar_instead_of_list_fails()
    {
        var name = AttributeCode.Create("tags");
        var schema = SchemaWith(new AttributeDefinition(
            name, new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)), Desc));

        // Attempt to create a string attribute for a list definition
        var result = schema.TryCreateAttribute(name, "single", out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_scalar_instead_of_complex_fails()
    {
        var name = AttributeCode.Create("address");
        var schema = SchemaWith(new AttributeDefinition(
            name,
            new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            }),
            Desc));

        // Attempt to create a string attribute for a complex definition
        var result = schema.TryCreateAttribute(name, "not-a-complex", out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void list_with_wrong_element_type_fails()
    {
        var name = AttributeCode.Create("tags");
        var schema = SchemaWith(new AttributeDefinition(
            name, new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)), Desc));

        // Provide ints instead of strings as list elements
        var value = new List<object> { 1, 2, 3 };
        var result = schema.TryCreateAttribute(name, value, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void create_attribute_throws_for_invalid_value()
    {
        var name = AttributeCode.Create("active");
        var schema = SchemaWith(new AttributeDefinition(name, new ScalarAttributeType(ScalarDataType.Boolean), Desc));

        // CreateAttribute (non-Try) should throw for wrong type
        var ex = Record.Exception(() => schema.CreateAttribute(name, "not-a-bool"));

        _ = ex.ShouldBeOfType<ArgumentException>();
    }
}
