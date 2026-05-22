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
        var schema = AttributeSchema.Load([], []);
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
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = complexType, Description = Desc });

        var value = new Dictionary<string, object> { ["city"] = "Seattle", ["zip"] = "98101" };
        var collection = new AttributeValueCollection(schema);
        collection.Set(name, (IReadOnlyDictionary<string, object>)value);

        _ = collection[name].UntypedValue.ShouldBeOfType<ReadOnlyDictionary<string, object>>();
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
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = complexType, Description = Desc });

        var value = new Dictionary<string, object> { ["city"] = "Seattle" }; // zip omitted
        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, (IReadOnlyDictionary<string, object>)value, out _);

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
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = complexType, Description = Desc });

        var value = new Dictionary<string, object> { ["city"] = "Seattle", ["country"] = "US" };
        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, (IReadOnlyDictionary<string, object>)value, out _);

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
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = complexType, Description = Desc });

        var value = new Dictionary<string, object> { ["zip"] = "not-an-int" }; // string instead of int
        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, (IReadOnlyDictionary<string, object>)value, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void list_of_strings_succeeds()
    {
        var name = AttributeCode.Create("tags");
        var listType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = listType, Description = Desc });

        var value = new List<object> { "admin", "power" };
        var collection = new AttributeValueCollection(schema);
        collection.Set(name, (IReadOnlyList<object>)value);

        _ = collection[name].UntypedValue.ShouldBeOfType<ReadOnlyCollection<object>>();
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
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = listType, Description = Desc });

        var value = new List<object>
        {
            new Dictionary<string, object> { ["number"] = "555-1234", ["type"] = "mobile" },
            new Dictionary<string, object> { ["number"] = "555-9999", ["type"] = "home"},
        };
        var collection = new AttributeValueCollection(schema);
        collection.Set(name, (IReadOnlyList<object>)value);

        var attr = (AttributeValue<IReadOnlyList<object>>)collection[name];
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
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = listType, Description = Desc });

        var value = new List<object>();
        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, (IReadOnlyList<object>)value, out _);

        result.ShouldBeTrue();
    }

    // --- Negative / edge-case tests ---

    [Fact]
    public void unknown_attribute_name_fails_for_bool()
    {
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("active"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
            Description = Desc
        });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(AttributeCode.Create("missing"), true, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_string()
    {
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("color"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = Desc
        });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(AttributeCode.Create("missing"), "value", out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_int()
    {
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("age"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
            Description = Desc
        });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(AttributeCode.Create("missing"), 42, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_decimal()
    {
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("balance"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Decimal),
            Description = Desc
        });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(AttributeCode.Create("missing"), 3.14m, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_date()
    {
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("birthdate"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Date),
            Description = Desc
        });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(AttributeCode.Create("missing"), new DateOnly(2000, 1, 1), out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_date_time()
    {
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("recordedat"),
            AttributeType = new ScalarAttributeType(ScalarDataType.DateTime),
            Description = Desc
        });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(AttributeCode.Create("missing"), DateTimeOffset.UtcNow, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_complex()
    {
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("address"),
            AttributeType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            }),
            Description = Desc
        });

        var value = new Dictionary<string, object> { ["city"] = "Seattle" };
        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(AttributeCode.Create("missing"), (IReadOnlyDictionary<string, object>)value, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void unknown_attribute_name_fails_for_list()
    {
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("tags"),
            AttributeType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)),
            Description = Desc
        });

        var value = new List<object> { "a", "b" };
        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(AttributeCode.Create("missing"), (IReadOnlyList<object>)value, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_string_instead_of_bool_fails()
    {
        var name = AttributeCode.Create("active");
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = new ScalarAttributeType(ScalarDataType.Boolean), Description = Desc });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, "true", out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_bool_instead_of_string_fails()
    {
        var name = AttributeCode.Create("color");
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = Desc });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, true, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_int_instead_of_decimal_fails()
    {
        var name = AttributeCode.Create("balance");
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = new ScalarAttributeType(ScalarDataType.Decimal), Description = Desc });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, 42, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_scalar_instead_of_list_fails()
    {
        var name = AttributeCode.Create("tags");
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = name,
            AttributeType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)),
            Description = Desc
        });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, "single", out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void wrong_type_scalar_instead_of_complex_fails()
    {
        var name = AttributeCode.Create("address");
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = name,
            AttributeType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            }),
            Description = Desc
        });

        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, "not-a-complex", out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void list_with_wrong_element_type_fails()
    {
        var name = AttributeCode.Create("tags");
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = name,
            AttributeType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)),
            Description = Desc
        });

        // Provide ints instead of strings as list elements
        var value = new List<object> { 1, 2, 3 };
        var collection = new AttributeValueCollection(schema);
        var result = collection.TrySet(name, (IReadOnlyList<object>)value, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void set_throws_for_invalid_value()
    {
        var name = AttributeCode.Create("active");
        var schema = SchemaWith(new AttributeDefinition { Code = name, AttributeType = new ScalarAttributeType(ScalarDataType.Boolean), Description = Desc });

        var collection = new AttributeValueCollection(schema);
        // Set (non-Try) should throw for wrong type
        var ex = Record.Exception(() => collection.Set(name, "not-a-bool"));

        _ = ex.ShouldBeOfType<ArgumentException>();
    }
}
