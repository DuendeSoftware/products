// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public static class AttributeDescriptions
{
    public static TheoryData<string> InvalidInputs { get; } = ["", " ", new string('x', 201)];

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public static void CannotParseInvalidInputs(string input)
    {
        var ex = Record.Exception(() => _ = AttributeDescription.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_input()
    {
        const string Input = $"{nameof(AttributeDescription)}1";
        var instance = AttributeDescription.Create(Input);

        var @string = instance.ToString();

        @string.ShouldBe(Input);
    }
}
