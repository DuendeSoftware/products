// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public sealed class AttributeDisplayNames
{
    [Fact]
    public void valid_input_is_accepted()
    {
        var instance = AttributeDisplayName.Create("Personal Information");

        instance.Value.ShouldBe("Personal Information");
    }

    [Fact]
    public void empty_string_is_rejected()
    {
        var ex = Record.Exception(() => _ = AttributeDisplayName.Create(""));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public void whitespace_only_is_rejected()
    {
        var ex = Record.Exception(() => _ = AttributeDisplayName.Create("   "));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public void over_max_length_is_rejected()
    {
        var input = new string('x', 201);

        var ex = Record.Exception(() => _ = AttributeDisplayName.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public void max_length_is_accepted()
    {
        var input = new string('x', 200);
        var instance = AttributeDisplayName.Create(input);

        instance.Value.ShouldBe(input);
    }

    [Fact]
    public void input_is_trimmed()
    {
        var instance = AttributeDisplayName.Create("  Personal Info  ");

        instance.Value.ShouldBe("Personal Info");
    }

    [Fact]
    public void try_parse_returns_false_for_empty()
    {
        var result = AttributeDisplayName.TryCreate("", out var parsed);

        result.ShouldBeFalse();
        parsed.ShouldBe(default(AttributeDisplayName));
    }

    [Fact]
    public void to_string_returns_value()
    {
        var instance = AttributeDisplayName.Create("Contact Details");

        instance.ToString().ShouldBe("Contact Details");
    }
}
