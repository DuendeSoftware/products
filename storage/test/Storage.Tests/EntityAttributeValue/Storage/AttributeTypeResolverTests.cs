// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.EntityAttributeValue.Storage;

public static class AttributeTypeResolverTests
{
    private static readonly AttributeDescription Desc = AttributeDescription.Create("test");

    private static AttributeTypeResolver ResolverWith(params AttributeDefinition[] definitions)
    {
        var dict = definitions.ToDictionary(d => d.Code, d => d);
        return new AttributeTypeResolver(dict);
    }

    // --- Scalar paths ---

    [Fact]
    public static void scalar_string_resolves_to_string_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("displayname"),
                new ScalarAttributeType(ScalarDataType.String),
                Desc));

        var field = resolver.ResolveField("displayname");

        _ = field.ShouldBeOfType<StringField>();
        field.Path.ShouldBe("DISPLAYNAME");
    }

    [Fact]
    public static void scalar_boolean_resolves_to_boolean_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("active"),
                new ScalarAttributeType(ScalarDataType.Boolean),
                Desc));

        var field = resolver.ResolveField("active");

        _ = field.ShouldBeOfType<BooleanField>();
    }

    [Fact]
    public static void scalar_date_resolves_to_date_time_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("birthdate"),
                new ScalarAttributeType(ScalarDataType.Date),
                Desc));

        var field = resolver.ResolveField("birthdate");

        _ = field.ShouldBeOfType<DateTimeField>();
    }

    [Fact]
    public static void scalar_date_time_resolves_to_date_time_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("recordedat"),
                new ScalarAttributeType(ScalarDataType.DateTime),
                Desc));

        var field = resolver.ResolveField("recordedat");

        _ = field.ShouldBeOfType<DateTimeField>();
    }

    [Fact]
    public static void scalar_decimal_resolves_to_number_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("balance"),
                new ScalarAttributeType(ScalarDataType.Decimal),
                Desc));

        var field = resolver.ResolveField("balance");

        _ = field.ShouldBeOfType<NumberField>();
    }

    [Fact]
    public static void scalar_integer_resolves_to_number_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("age"),
                new ScalarAttributeType(ScalarDataType.Integer),
                Desc));

        var field = resolver.ResolveField("age");

        _ = field.ShouldBeOfType<NumberField>();
    }

    // --- Built-in username ---

    [Fact]
    public static void username_resolves_to_string_field()
    {
        var resolver = ResolverWith(); // no schema definitions needed

        var field = resolver.ResolveField("userName");

        _ = field.ShouldBeOfType<StringField>();
        field.Path.ShouldBe("USERNAME");
    }

    [Fact]
    public static void username_case_insensitive()
    {
        var resolver = ResolverWith();

        var field = resolver.ResolveField("USERNAME");

        _ = field.ShouldBeOfType<StringField>();
        field.Path.ShouldBe("USERNAME");
    }

    // --- Case normalization ---

    [Fact]
    public static void mixed_case_attribute_is_normalized()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("displayname"),
                new ScalarAttributeType(ScalarDataType.String),
                Desc));

        var field = resolver.ResolveField("DisplayName");

        _ = field.ShouldBeOfType<StringField>();
        field.Path.ShouldBe("DISPLAYNAME");
    }

    // --- Dotted complex paths ---

    [Fact]
    public static void complex_sub_property_resolves_to_correct_field_type()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("address"),
                new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                {
                    [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                    [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.Integer)
                }),
                Desc));

        var cityField = resolver.ResolveField("address.city");
        var zipField = resolver.ResolveField("address.zip");

        _ = cityField.ShouldBeOfType<StringField>();
        _ = zipField.ShouldBeOfType<NumberField>();
    }

    // --- List paths ---

    [Fact]
    public static void list_of_scalar_resolves_to_multi_valued_string_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("tags"),
                new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)),
                Desc));

        var field = resolver.ResolveField("tags");

        _ = field.ShouldBeOfType<StringField>();
        field.IsMultiValued.ShouldBeTrue();
    }

    [Fact]
    public static void list_of_boolean_resolves_to_multi_valued_boolean_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("flags"),
                new ListAttributeType(new ScalarAttributeType(ScalarDataType.Boolean)),
                Desc));

        var field = resolver.ResolveField("flags");

        _ = field.ShouldBeOfType<BooleanField>();
        field.IsMultiValued.ShouldBeTrue();
    }

    // --- List-of-complex dotted paths ---

    [Fact]
    public static void list_of_complex_sub_property_resolves_to_multi_valued_string_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("phones"),
                new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                {
                    [AttributeCode.Create("number")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                    [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String)
                })),
                Desc));

        var field = resolver.ResolveField("phones.number");

        // Inside a list, scalars resolve to their native type with IsMultiValued = true
        _ = field.ShouldBeOfType<StringField>();
        field.IsMultiValued.ShouldBeTrue();
    }

    [Fact]
    public static void list_of_complex_integer_sub_property_resolves_to_multi_valued_number_field()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("entries"),
                new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                {
                    [AttributeCode.Create("rank")] = ComplexAttributeProperty.Of(ScalarDataType.Integer)
                })),
                Desc));

        var field = resolver.ResolveField("entries.rank");

        // Array context: integer becomes multi-valued NumberField
        _ = field.ShouldBeOfType<NumberField>();
        field.IsMultiValued.ShouldBeTrue();
    }

    // --- Error cases ---

    [Fact]
    public static void unknown_attribute_throws_not_supported()
    {
        var resolver = ResolverWith();

        var ex = Record.Exception(() => resolver.ResolveField("nosuchattr"));

        _ = ex.ShouldBeOfType<NotSupportedException>();
        ex.Message.ShouldContain("nosuchattr");
    }

    [Fact]
    public static void unknown_sub_property_throws_not_supported()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("address"),
                new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                {
                    [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
                }),
                Desc));

        var ex = Record.Exception(() => resolver.ResolveField("address.country"));

        _ = ex.ShouldBeOfType<NotSupportedException>();
        ex.Message.ShouldContain("country");
    }

    [Fact]
    public static void direct_complex_query_throws_not_supported()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("address"),
                new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                {
                    [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
                }),
                Desc));

        var ex = Record.Exception(() => resolver.ResolveField("address"));

        _ = ex.ShouldBeOfType<NotSupportedException>();
        ex.Message.ShouldContain("complex");
    }

    [Fact]
    public static void navigating_into_scalar_throws_not_supported()
    {
        var resolver = ResolverWith(
            new AttributeDefinition(
                AttributeCode.Create("displayname"),
                new ScalarAttributeType(ScalarDataType.String),
                Desc));

        var ex = Record.Exception(() => resolver.ResolveField("displayname.sub"));

        _ = ex.ShouldBeOfType<NotSupportedException>();
        ex.Message.ShouldContain("sub");
    }

    [Fact]
    public static void undefined_attribute_throws_not_supported()
    {
        var resolver = ResolverWith();

        // The field is treated as unknown because it is not defined in the schema.
        var ex = Record.Exception(() => resolver.ResolveField("INVALID_NAME"));

        _ = ex.ShouldBeOfType<NotSupportedException>();
    }
}
