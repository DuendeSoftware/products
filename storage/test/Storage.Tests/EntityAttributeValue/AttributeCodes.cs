// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public static class AttributeCodes
{
    public static TheoryData<string> InvalidInputs { get; } =
    [
        "",
        " ",
        "a b",
        "*",
        "*a",
        "a*",
        "a*b",
        "-",
        "-a",
        "a-",
        "1",
        "1a",
        "a_",
        new string('x', 101)
    ];

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public static void CannotParseInvalidInputs(string input)
    {
        var ex = Record.Exception(() => _ = AttributeCode.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_input()
    {
        const string Input = "schema_attribute_name";
        var instance = AttributeCode.Create(Input);

        var @string = instance.ToString();

        @string.ShouldBe(Input);
    }

    [Theory]
    [InlineData("givenName")]
    [InlineData("FamilyName")]
    [InlineData("A")]
    [InlineData("Aa")]
    [InlineData("aA")]
    [InlineData("aAb")]
    public static void TryParse_accepts_mixed_case(string input)
    {
        var result = AttributeCode.TryCreate(input, out var name);

        result.ShouldBeTrue();
        name!.Value.ShouldBe(input, "Original casing should be preserved");
    }

    [Fact]
    public static void Preserves_original_casing()
    {
        var name = AttributeCode.Create("givenName");

        name.Value.ShouldBe("givenName");
        name.ToString().ShouldBe("givenName");
    }

    [Theory]
    [InlineData("givenName", "givenname")]
    [InlineData("givenName", "GIVENNAME")]
    [InlineData("givenName", "GivenName")]
    [InlineData("name", "NAME")]
    public static void Equals_is_case_insensitive(string left, string right)
    {
        var a = AttributeCode.Create(left);
        var b = AttributeCode.Create(right);

        a.ShouldBe(b);
        b.ShouldBe(a);
    }

    [Fact]
    public static void GetHashCode_is_case_insensitive()
    {
        var a = AttributeCode.Create("givenName");
        var b = AttributeCode.Create("GIVENNAME");
        var c = AttributeCode.Create("givenname");

        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.GetHashCode().ShouldBe(c.GetHashCode());
    }

    [Fact]
    public static void Works_as_case_insensitive_dictionary_key()
    {
        var dict = new Dictionary<AttributeCode, string>
        {
            [AttributeCode.Create("givenName")] = "Alice"
        };

        dict.TryGetValue(AttributeCode.Create("GIVENNAME"), out var value).ShouldBeTrue();
        value.ShouldBe("Alice");

        dict.TryGetValue(AttributeCode.Create("givenname"), out value).ShouldBeTrue();
        value.ShouldBe("Alice");
    }

    [Fact]
    public static void Dictionary_rejects_duplicate_casing_variants()
    {
        var dict = new Dictionary<AttributeCode, string>
        {
            [AttributeCode.Create("name")] = "first"
        };

        var ex = Record.Exception(() => dict.Add(AttributeCode.Create("NAME"), "second"));

        _ = ex.ShouldNotBeNull();
    }
}
