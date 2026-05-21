// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public sealed class AttributeGroupNames
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
        "hello world",
        "name@group",
        new string('x', 101)
    ];

    public static TheoryData<string> ValidInputs { get; } =
    [
        "a",
        "abc",
        "personal_info",
        "my-group",
        "123",
        "1a",
        "a_",
        "-a",
        "a-",
        "A_B-C"
    ];

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void cannot_parse_invalid_inputs(string input)
    {
        var ex = Record.Exception(() => _ = AttributeGroupCode.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Theory]
    [MemberData(nameof(ValidInputs))]
    public void can_parse_valid_inputs(string input)
    {
        var instance = AttributeGroupCode.Create(input);

        instance.Value.ShouldBe(input);
    }

    [Fact]
    public void string_is_preserved()
    {
        const string Input = "personal_info";
        var instance = AttributeGroupCode.Create(Input);

        instance.Value.ShouldBe(Input);
    }

    [Fact]
    public void equality_is_case_insensitive()
    {
        var a = AttributeGroupCode.Create("PersonalInfo");
        var b = AttributeGroupCode.Create("personalinfo");

        a.ShouldBe(b);
    }

    [Fact]
    public void hash_code_is_case_insensitive()
    {
        var a = AttributeGroupCode.Create("PersonalInfo");
        var b = AttributeGroupCode.Create("personalinfo");

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void max_length_is_accepted()
    {
        var input = new string('x', 100);
        var instance = AttributeGroupCode.Create(input);

        instance.Value.ShouldBe(input);
    }

    [Fact]
    public void try_parse_returns_false_for_invalid()
    {
        var result = AttributeGroupCode.TryCreate("not valid!", out var parsed);

        result.ShouldBeFalse();
        parsed.ShouldBe(default(AttributeGroupCode));
    }

    [Fact]
    public void to_string_returns_value()
    {
        var instance = AttributeGroupCode.Create("my_group");

        instance.ToString().ShouldBe("my_group");
    }
}
