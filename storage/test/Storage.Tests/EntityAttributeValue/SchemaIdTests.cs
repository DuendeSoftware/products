// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public static class SchemaIdTests
{
    [Theory]
    [InlineData("client")]
    [InlineData("identity_provider")]
    [InlineData("api-resource")]
    [InlineData("api:scope")]
    [InlineData("ABC123")]
    [InlineData("a")]
    public static void create_succeeds_for_valid_values(string value) =>
        Should.NotThrow(() => SchemaId.Create(value));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public static void create_throws_for_empty_or_whitespace(string value) =>
        _ = Should.Throw<FormatException>(() => SchemaId.Create(value));

    [Fact]
    public static void create_throws_for_null() =>
        _ = Should.Throw<FormatException>(() => SchemaId.Create(null!));

    [Fact]
    public static void create_throws_for_value_exceeding_max_length()
    {
        var tooLong = new string('a', 51);
        _ = Should.Throw<FormatException>(() => SchemaId.Create(tooLong));
    }

    [Fact]
    public static void create_accepts_value_at_max_length() =>
        Should.NotThrow(() => SchemaId.Create(new string('a', 50)));

    [Theory]
    [InlineData("_starts_with_underscore")]
    [InlineData("has space")]
    [InlineData("has.dot")]
    public static void create_throws_for_invalid_characters(string value) =>
        _ = Should.Throw<FormatException>(() => SchemaId.Create(value));

    [Fact]
    public static void equality_is_case_insensitive()
    {
        var lower = SchemaId.Create("client");
        var upper = SchemaId.Create("CLIENT");

        lower.ShouldBe(upper);
    }

    [Fact]
    public static void inequality_detects_different_values()
    {
        var a = SchemaId.Create("a");
        var b = SchemaId.Create("b");

        a.ShouldNotBe(b);
    }

    [Fact]
    public static void value_property_preserves_original_casing()
    {
        var id = SchemaId.Create("MySchema");
        id.Value.ShouldBe("MySchema");
    }

    [Fact]
    public static void to_string_returns_value()
    {
        var id = SchemaId.Create("client");
        id.ToString().ShouldBe("client");
    }

    [Fact]
    public static void case_insensitive_lookup_in_dictionary()
    {
        var dict = new Dictionary<SchemaId, string>
        {
            [SchemaId.Create("client")] = "found"
        };

        dict.TryGetValue(SchemaId.Create("CLIENT"), out var result).ShouldBeTrue();
        result.ShouldBe("found");
    }
}
