// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.EntityAttributeValue.Internal.Storage;

namespace Duende.Storage.EntityAttributeValue.Storage;

public static class EavMapperTests
{
    private static AttributeSchema SchemaWith(params AttributeDefinition[] definitions) =>
        AttributeSchema.Load(definitions);

    private static AttributeDefinition ScalarDef(string code, ScalarDataType dataType) => new()
    {
        Code = AttributeCode.Create(code),
        AttributeType = new ScalarAttributeType(dataType),
        Description = AttributeDescription.Create("test")
    };

    private static AttributeCode Code(string code) => AttributeCode.Create(code);

    [Fact]
    public static void to_dso_list_returns_empty_for_empty_collection()
    {
        var result = EavMapper.ToDsoList(new AttributeValueCollection());
        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_dso_list_maps_string_value()
    {
        var collection = new AttributeValueCollection();
        collection.Set(Code("name"), "Alice");

        var result = EavMapper.ToDsoList(collection);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("name");
        result[0].Value.ShouldBe("Alice");
    }

    [Fact]
    public static void to_dso_list_maps_boolean_value()
    {
        var collection = new AttributeValueCollection();
        collection.Set(Code("active"), true);

        var result = EavMapper.ToDsoList(collection);

        result[0].Name.ShouldBe("active");
        result[0].Value.ShouldBe(true);
    }

    [Fact]
    public static void to_dso_list_maps_integer_value()
    {
        var collection = new AttributeValueCollection();
        collection.Set(Code("count"), 42);

        var result = EavMapper.ToDsoList(collection);

        result[0].Value.ShouldBe(42);
    }

    [Fact]
    public static void to_dso_list_maps_decimal_value()
    {
        var collection = new AttributeValueCollection();
        collection.Set(Code("price"), 9.99m);

        var result = EavMapper.ToDsoList(collection);

        result[0].Value.ShouldBe(9.99m);
    }

    [Fact]
    public static void to_dso_list_maps_date_as_iso_string()
    {
        var collection = new AttributeValueCollection();
        collection.Set(Code("dob"), new DateOnly(2000, 6, 15));

        var result = EavMapper.ToDsoList(collection);

        result[0].Value.ShouldBe("2000-06-15");
    }

    [Fact]
    public static void to_dso_list_maps_datetimeoffset_as_iso_string()
    {
        var collection = new AttributeValueCollection();
        var dto = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        collection.Set(Code("created"), dto);

        var result = EavMapper.ToDsoList(collection);

        result[0].Value.ShouldBe("2024-01-01T12:00:00.0000000+00:00");
    }

    [Fact]
    public static void to_dso_list_maps_complex_value_as_dictionary()
    {
        var collection = new AttributeValueCollection();
        var dict = new Dictionary<string, object> { ["city"] = "Berlin" }.AsReadOnly();
        collection.Set(Code("address"), (IReadOnlyDictionary<string, object>)dict);

        var result = EavMapper.ToDsoList(collection);

        _ = result[0].Value.ShouldBeAssignableTo<IReadOnlyDictionary<string, object>>();
    }

    [Fact]
    public static void to_dso_list_maps_list_value_as_list()
    {
        var collection = new AttributeValueCollection();
        var list = new List<object> { "a", "b" }.AsReadOnly();
        collection.Set(Code("tags"), (IReadOnlyList<object>)list);

        var result = EavMapper.ToDsoList(collection);

        _ = result[0].Value.ShouldBeAssignableTo<IReadOnlyList<object>>();
    }

    [Fact]
    public static void to_dso_list_from_sequence_returns_empty_for_empty_input()
    {
        var result = EavMapper.ToDsoList([]);
        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_dso_list_from_sequence_maps_attribute_values()
    {
        var values = new[] { AttributeValue.Load(Code("x"), "hello") };
        var result = EavMapper.ToDsoList(values);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("x");
        result[0].Value.ShouldBe("hello");
    }

    [Fact]
    public static void to_attribute_values_returns_empty_when_schema_is_null()
    {
        var dsos = new List<AttributeValueDso.V1> { new("name", "Alice") };
        var result = EavMapper.ToAttributeValues(dsos, null).ToList();
        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_returns_empty_for_empty_dso_list()
    {
        var schema = SchemaWith(ScalarDef("name", ScalarDataType.String));
        var result = EavMapper.ToAttributeValues([], schema).ToList();
        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_silently_skips_attributes_not_in_schema()
    {
        var schema = SchemaWith(ScalarDef("name", ScalarDataType.String));
        var dsos = new List<AttributeValueDso.V1> { new("unknown_attr", "value") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_deserializes_string()
    {
        var schema = SchemaWith(ScalarDef("name", ScalarDataType.String));
        var dsos = new List<AttributeValueDso.V1> { new("name", "Alice") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.Count.ShouldBe(1);
        result[0].Code.Value.ShouldBe("name");
        result[0].UntypedValue.ShouldBe("Alice");
    }

    [Fact]
    public static void to_attribute_values_deserializes_boolean_from_string()
    {
        var schema = SchemaWith(ScalarDef("active", ScalarDataType.Boolean));
        var dsos = new List<AttributeValueDso.V1> { new("active", "True") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result[0].UntypedValue.ShouldBe(true);
    }

    [Fact]
    public static void to_attribute_values_deserializes_integer_from_string()
    {
        var schema = SchemaWith(ScalarDef("count", ScalarDataType.Integer));
        var dsos = new List<AttributeValueDso.V1> { new("count", "42") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result[0].UntypedValue.ShouldBe(42);
    }

    [Fact]
    public static void to_attribute_values_deserializes_decimal_from_string()
    {
        var schema = SchemaWith(ScalarDef("price", ScalarDataType.Decimal));
        var dsos = new List<AttributeValueDso.V1> { new("price", "9.99") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result[0].UntypedValue.ShouldBe(9.99m);
    }

    [Fact]
    public static void to_attribute_values_deserializes_date_from_string()
    {
        var schema = SchemaWith(ScalarDef("dob", ScalarDataType.Date));
        var dsos = new List<AttributeValueDso.V1> { new("dob", "2000-06-15") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result[0].UntypedValue.ShouldBe(new DateOnly(2000, 6, 15));
    }

    [Fact]
    public static void to_attribute_values_deserializes_datetime_from_string()
    {
        var schema = SchemaWith(ScalarDef("created", ScalarDataType.DateTime));
        var dsos = new List<AttributeValueDso.V1> { new("created", "2024-01-01T12:00:00.0000000+00:00") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result[0].UntypedValue.ShouldBe(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public static void to_attribute_values_deserializes_native_boolean()
    {
        var schema = SchemaWith(ScalarDef("flag", ScalarDataType.Boolean));
        var dsos = new List<AttributeValueDso.V1> { new("flag", true) };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result[0].UntypedValue.ShouldBe(true);
    }

    [Fact]
    public static void to_attribute_values_deserializes_native_integer()
    {
        var schema = SchemaWith(ScalarDef("count", ScalarDataType.Integer));
        var dsos = new List<AttributeValueDso.V1> { new("count", 42) };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result[0].UntypedValue.ShouldBe(42);
    }

    [Fact]
    public static void to_attribute_values_deserializes_native_decimal()
    {
        var schema = SchemaWith(ScalarDef("price", ScalarDataType.Decimal));
        var dsos = new List<AttributeValueDso.V1> { new("price", 9.99m) };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result[0].UntypedValue.ShouldBe(9.99m);
    }

    [Fact]
    public static void to_attribute_values_deserializes_complex_from_dictionary()
    {
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("address"),
            AttributeType = complexType,
            Description = AttributeDescription.Create("test")
        });

        var dict = new Dictionary<string, object> { ["city"] = "Berlin" }.AsReadOnly();
        var dsos = new List<AttributeValueDso.V1> { new("address", dict) };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.Count.ShouldBe(1);
        var value = result[0].UntypedValue.ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>();
        value!["city"].ShouldBe("Berlin");
    }

    [Fact]
    public static void to_attribute_values_deserializes_complex_from_json_element()
    {
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("address"),
            AttributeType = complexType,
            Description = AttributeDescription.Create("test")
        });

        var json = JsonDocument.Parse("""{"city":"Paris"}""").RootElement;
        var dsos = new List<AttributeValueDso.V1> { new("address", json) };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.Count.ShouldBe(1);
        var value = result[0].UntypedValue.ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>();
        value!["city"].ShouldBe("Paris");
    }

    [Fact]
    public static void to_attribute_values_deserializes_list_from_list()
    {
        var listType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("tags"),
            AttributeType = listType,
            Description = AttributeDescription.Create("test")
        });

        var list = new List<object> { "a", "b" }.AsReadOnly();
        var dsos = new List<AttributeValueDso.V1> { new("tags", list) };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.Count.ShouldBe(1);
        var value = result[0].UntypedValue.ShouldBeAssignableTo<IReadOnlyList<object>>();
        value!.Count.ShouldBe(2);
    }

    [Fact]
    public static void to_attribute_values_skips_null_value_for_string()
    {
        var schema = SchemaWith(ScalarDef("name", ScalarDataType.String));
        var dsos = new List<AttributeValueDso.V1> { new("name", null) };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_skips_unparseable_boolean()
    {
        var schema = SchemaWith(ScalarDef("flag", ScalarDataType.Boolean));
        var dsos = new List<AttributeValueDso.V1> { new("flag", "not_a_bool") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_skips_unparseable_integer()
    {
        var schema = SchemaWith(ScalarDef("count", ScalarDataType.Integer));
        var dsos = new List<AttributeValueDso.V1> { new("count", "not_a_number") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_skips_unparseable_decimal()
    {
        var schema = SchemaWith(ScalarDef("price", ScalarDataType.Decimal));
        var dsos = new List<AttributeValueDso.V1> { new("price", "bad") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_skips_unparseable_date()
    {
        var schema = SchemaWith(ScalarDef("dob", ScalarDataType.Date));
        var dsos = new List<AttributeValueDso.V1> { new("dob", "not_a_date") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_skips_unparseable_datetime()
    {
        var schema = SchemaWith(ScalarDef("ts", ScalarDataType.DateTime));
        var dsos = new List<AttributeValueDso.V1> { new("ts", "not_a_datetime") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_skips_complex_attribute_with_scalar_value()
    {
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("address"),
            AttributeType = complexType,
            Description = AttributeDescription.Create("test")
        });

        var dsos = new List<AttributeValueDso.V1> { new("address", "not_a_dict") };

        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_attribute_values_round_trips_all_scalar_types()
    {
        var schema = SchemaWith(
            ScalarDef("str", ScalarDataType.String),
            ScalarDef("num", ScalarDataType.Integer),
            ScalarDef("flag", ScalarDataType.Boolean),
            ScalarDef("amt", ScalarDataType.Decimal),
            ScalarDef("day", ScalarDataType.Date),
            ScalarDef("ts", ScalarDataType.DateTime));

        var collection = new AttributeValueCollection();
        collection.Set(Code("str"), "hello");
        collection.Set(Code("num"), 7);
        collection.Set(Code("flag"), false);
        collection.Set(Code("amt"), 3.14m);
        collection.Set(Code("day"), new DateOnly(1990, 3, 21));
        collection.Set(Code("ts"), new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var dsos = EavMapper.ToDsoList(collection);
        var result = EavMapper.ToAttributeValues(dsos, schema).ToList();

        result.Count.ShouldBe(6);
        result.First(a => a.Code.Value == "str").UntypedValue.ShouldBe("hello");
        result.First(a => a.Code.Value == "num").UntypedValue.ShouldBe(7);
        result.First(a => a.Code.Value == "flag").UntypedValue.ShouldBe(false);
        result.First(a => a.Code.Value == "amt").UntypedValue.ShouldBe(3.14m);
        result.First(a => a.Code.Value == "day").UntypedValue.ShouldBe(new DateOnly(1990, 3, 21));
        result.First(a => a.Code.Value == "ts").UntypedValue.ShouldBe(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public static void to_mutable_collection_returns_empty_for_empty_input()
    {
        var result = EavMapper.ToMutableCollection([]);
        result.ShouldBeEmpty();
    }

    [Fact]
    public static void to_mutable_collection_converts_readonly_to_mutable()
    {
        var values = new List<AttributeValue>
        {
            AttributeValue.Load(Code("x"), "hello"),
            AttributeValue.Load(Code("y"), 42)
        };

        var result = EavMapper.ToMutableCollection(values);

        result.Count.ShouldBe(2);
        result[Code("x")].UntypedValue.ShouldBe("hello");
        result[Code("y")].UntypedValue.ShouldBe(42);
    }

    [Fact]
    public static void to_mutable_collection_allows_modification()
    {
        var values = new List<AttributeValue> { AttributeValue.Load(Code("x"), "old") };
        var result = EavMapper.ToMutableCollection(values);

        result.Set(Code("x"), "new");

        result[Code("x")].UntypedValue.ShouldBe("new");
    }

    [Fact]
    public static void normalize_json_element_handles_string()
    {
        var je = JsonDocument.Parse("\"hello\"").RootElement;
        EavMapper.NormalizeJsonElement(je).ShouldBe("hello");
    }

    [Fact]
    public static void normalize_json_element_handles_true()
    {
        var je = JsonDocument.Parse("true").RootElement;
        EavMapper.NormalizeJsonElement(je).ShouldBe(true);
    }

    [Fact]
    public static void normalize_json_element_handles_false()
    {
        var je = JsonDocument.Parse("false").RootElement;
        EavMapper.NormalizeJsonElement(je).ShouldBe(false);
    }

    [Fact]
    public static void normalize_json_element_handles_null()
    {
        var je = JsonDocument.Parse("null").RootElement;
        EavMapper.NormalizeJsonElement(je).ShouldBeNull();
    }

    [Fact]
    public static void normalize_json_element_handles_number_as_decimal()
    {
        var je = JsonDocument.Parse("123").RootElement;
        EavMapper.NormalizeJsonElement(je).ShouldBe(123m);
    }

    [Fact]
    public static void normalize_json_element_handles_object_as_readonly_dictionary()
    {
        var je = JsonDocument.Parse("""{"a":"1","b":"2"}""").RootElement;
        var result = EavMapper.NormalizeJsonElement(je).ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>();
        result!["a"].ShouldBe("1");
        result["b"].ShouldBe("2");
    }

    [Fact]
    public static void normalize_json_element_handles_array_as_readonly_list()
    {
        var je = JsonDocument.Parse("""["x","y"]""").RootElement;
        var result = EavMapper.NormalizeJsonElement(je).ShouldBeAssignableTo<IReadOnlyList<object?>>();
        result!.Count.ShouldBe(2);
        result[0].ShouldBe("x");
    }

    [Fact]
    public static void normalize_json_element_handles_nested_objects()
    {
        var je = JsonDocument.Parse("""{"outer":{"inner":"value"}}""").RootElement;
        var result = EavMapper.NormalizeJsonElement(je).ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>();
        var inner = result!["outer"].ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>();
        inner!["inner"].ShouldBe("value");
    }

    [Fact]
    public static void json_round_trip_all_scalar_types()
    {
        var schema = SchemaWith(
            ScalarDef("name", ScalarDataType.String),
            ScalarDef("count", ScalarDataType.Integer),
            ScalarDef("active", ScalarDataType.Boolean),
            ScalarDef("price", ScalarDataType.Decimal),
            ScalarDef("dob", ScalarDataType.Date),
            ScalarDef("created", ScalarDataType.DateTime));

        var collection = new AttributeValueCollection();
        collection.Set(Code("name"), "Alice");
        collection.Set(Code("count"), 42);
        collection.Set(Code("active"), true);
        collection.Set(Code("price"), 9.99m);
        collection.Set(Code("dob"), new DateOnly(1990, 3, 21));
        collection.Set(Code("created"), new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var dsos = EavMapper.ToDsoList(collection);
        var json = JsonSerializer.Serialize(dsos);
        var deserialized = JsonSerializer.Deserialize<List<AttributeValueDso.V1>>(json)!;

        var result = EavMapper.ToAttributeValues(deserialized, schema).ToList();

        result.Count.ShouldBe(6);
        result.First(a => a.Code.Value == "name").UntypedValue.ShouldBe("Alice");
        result.First(a => a.Code.Value == "count").UntypedValue.ShouldBe(42);
        result.First(a => a.Code.Value == "active").UntypedValue.ShouldBe(true);
        result.First(a => a.Code.Value == "price").UntypedValue.ShouldBe(9.99m);
        result.First(a => a.Code.Value == "dob").UntypedValue.ShouldBe(new DateOnly(1990, 3, 21));
        result.First(a => a.Code.Value == "created").UntypedValue.ShouldBe(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public static void json_round_trip_complex_attribute()
    {
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("address"),
            AttributeType = complexType,
            Description = AttributeDescription.Create("test")
        });

        var collection = new AttributeValueCollection();
        var dict = new Dictionary<string, object> { ["city"] = "Berlin" }.AsReadOnly();
        collection.Set(Code("address"), (IReadOnlyDictionary<string, object>)dict);

        var dsos = EavMapper.ToDsoList(collection);
        var json = JsonSerializer.Serialize(dsos);
        var deserialized = JsonSerializer.Deserialize<List<AttributeValueDso.V1>>(json)!;

        var result = EavMapper.ToAttributeValues(deserialized, schema).ToList();

        result.Count.ShouldBe(1);
        var value = result[0].UntypedValue.ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>();
        value!["city"].ShouldBe("Berlin");
    }

    [Fact]
    public static void json_round_trip_list_attribute()
    {
        var listType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));
        var schema = SchemaWith(new AttributeDefinition
        {
            Code = AttributeCode.Create("tags"),
            AttributeType = listType,
            Description = AttributeDescription.Create("test")
        });

        var collection = new AttributeValueCollection();
        var list = new List<object> { "a", "b" }.AsReadOnly();
        collection.Set(Code("tags"), (IReadOnlyList<object>)list);

        var dsos = EavMapper.ToDsoList(collection);
        var json = JsonSerializer.Serialize(dsos);
        var deserialized = JsonSerializer.Deserialize<List<AttributeValueDso.V1>>(json)!;

        var result = EavMapper.ToAttributeValues(deserialized, schema).ToList();

        result.Count.ShouldBe(1);
        var value = result[0].UntypedValue.ShouldBeAssignableTo<IReadOnlyList<object>>();
        value!.Count.ShouldBe(2);
    }
}
