// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal;

namespace Duende.Storage.EntityAttributeValue;

public sealed class AttributeSchemaValidationErrorTests
{
    private static readonly AttributeDescription Desc = AttributeDescription.Create("test");

    private static AttributeSchema SchemaWith(AttributeDefinition definition)
    {
        var schema = new AttributeSchema();
        _ = schema.AddAttributeDefinition(definition);
        return schema;
    }

    // --- Scalar ---

    [Fact]
    public void undefined_attribute_code_returns_not_defined_error()
    {
        var schema = SchemaWith(new AttributeDefinition(
            AttributeCode.Create("active"), new ScalarAttributeType(ScalarDataType.Boolean), Desc));

        var result = schema.TryCreateAttribute(AttributeCode.Create("missing"), true, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("is not defined in the schema"));
    }

    [Fact]
    public void type_mismatch_returns_defined_as_error()
    {
        var name = AttributeCode.Create("active");
        var schema = SchemaWith(new AttributeDefinition(name, new ScalarAttributeType(ScalarDataType.Boolean), Desc));

        var result = schema.TryCreateAttribute(name, "true", out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("is defined as"));
    }

    [Fact]
    public void success_returns_null_errors()
    {
        var name = AttributeCode.Create("active");
        var schema = SchemaWith(new AttributeDefinition(name, new ScalarAttributeType(ScalarDataType.Boolean), Desc));

        var result = schema.TryCreateAttribute(name, true, out _, out var errors);

        result.ShouldBeTrue();
        errors.ShouldBeNull();
    }

    // --- Complex ---

    [Fact]
    public void unknown_property_returns_not_defined_error()
    {
        var name = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var schema = SchemaWith(new AttributeDefinition(name, complexType, Desc));

        var value = new Dictionary<string, object> { ["city"] = "Seattle", ["country"] = "US" };
        var result = schema.TryCreateAttribute(name, (IReadOnlyDictionary<string, object>)value, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("is not defined in complex attribute"));
    }

    [Fact]
    public void property_type_mismatch_returns_expects_type_error()
    {
        var name = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.Integer)
        });
        var schema = SchemaWith(new AttributeDefinition(name, complexType, Desc));

        var value = new Dictionary<string, object> { ["zip"] = "not-an-int" };
        var result = schema.TryCreateAttribute(name, (IReadOnlyDictionary<string, object>)value, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("expects type"));
    }

    [Fact]
    public void multiple_errors_accumulated()
    {
        var name = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var schema = SchemaWith(new AttributeDefinition(name, complexType, Desc));

        // Two unknown properties → two errors
        var value = new Dictionary<string, object> { ["country"] = "US", ["region"] = "WA" };
        var result = schema.TryCreateAttribute(name, (IReadOnlyDictionary<string, object>)value, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.Count.ShouldBe(2);
    }

    [Fact]
    public void not_a_complex_type_returns_error()
    {
        var name = AttributeCode.Create("active");
        var schema = SchemaWith(new AttributeDefinition(name, new ScalarAttributeType(ScalarDataType.Boolean), Desc));

        var value = new Dictionary<string, object> { ["key"] = "value" };
        var result = schema.TryCreateAttribute(name, (IReadOnlyDictionary<string, object>)value, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("is not a complex type"));
    }

    // --- List ---

    [Fact]
    public void element_type_mismatch_returns_index_error()
    {
        var name = AttributeCode.Create("tags");
        var schema = SchemaWith(new AttributeDefinition(
            name, new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)), Desc));

        var value = new List<object> { "valid", 42 };
        var result = schema.TryCreateAttribute(name, (IReadOnlyList<object>)value, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("Element at index"));
    }

    [Fact]
    public void multiple_list_errors_accumulated()
    {
        var name = AttributeCode.Create("tags");
        var schema = SchemaWith(new AttributeDefinition(
            name, new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)), Desc));

        // Two wrong-type elements → two errors
        var value = new List<object> { 1, 2 };
        var result = schema.TryCreateAttribute(name, (IReadOnlyList<object>)value, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.Count.ShouldBe(2);
    }

    [Fact]
    public void not_a_list_type_returns_error()
    {
        var name = AttributeCode.Create("active");
        var schema = SchemaWith(new AttributeDefinition(name, new ScalarAttributeType(ScalarDataType.Boolean), Desc));

        var value = new List<object> { "a", "b" };
        var result = schema.TryCreateAttribute(name, (IReadOnlyList<object>)value, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("is not a list type"));
    }

    // --- List of Complex (SCIM emails) ---

    private static AttributeSchema EmailsSchema()
    {
        var emailsType = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("value")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean)
        }));
        return SchemaWith(new AttributeDefinition(AttributeCode.Create("emails"), emailsType, Desc));
    }

    [Fact]
    public void list_of_complex_property_type_mismatch_returns_error()
    {
        var schema = EmailsSchema();

        // "primary" should be a bool, not a string
        var value = new List<object>
        {
            new Dictionary<string, object> { ["value"] = "a@b.com", ["type"] = "work", ["primary"] = "yes" }
        };
        var result = schema.TryCreateAttribute(AttributeCode.Create("emails"), (IReadOnlyList<object>)value, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("Element at index 0") && e.Contains("primary") && e.Contains("expects type"));
    }

    [Fact]
    public void list_of_complex_unknown_property_returns_error()
    {
        var schema = EmailsSchema();

        // "display" is not a defined property in the emails complex type
        var value = new List<object>
        {
            new Dictionary<string, object> { ["value"] = "a@b.com", ["display"] = "Work Email" }
        };
        var result = schema.TryCreateAttribute(AttributeCode.Create("emails"), (IReadOnlyList<object>)value, out _, out var errors);

        result.ShouldBeFalse();
        _ = errors.ShouldNotBeNull();
        errors.ShouldContain(e => e.Contains("Element at index 0") && e.Contains("display") && e.Contains("is not defined in complex attribute"));
    }
}
