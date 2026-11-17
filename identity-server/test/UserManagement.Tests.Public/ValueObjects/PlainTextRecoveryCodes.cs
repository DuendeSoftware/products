// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.RecoveryCodes;

namespace Duende.Platform.UserManagement.ValueObjects;

public static class PlainTextRecoveryCodes
{
    [Fact]
    public static void Text_is_input()
    {
        const string input = "123";
        var instance = PlainTextRecoveryCode.Create(input);

        var text = instance.Text;

        text.ShouldBe(input);
    }

    [Theory]
    [InlineData("1234", "1234")]
    [InlineData("12345", "12345")]
    [InlineData("123456", "123-456")]
    [InlineData("1234567", "1234-567")]
    [InlineData("12345678", "1234-5678")]
    [InlineData("123456789", "123-456-789")]
    [InlineData("1234567890", "12345-67890")]
    [InlineData("12345678901", "1234-5678-901")]
    [InlineData("123456789012", "1234-5678-9012")]
    [InlineData("1234567890123", "12345-67890-123")]
    [InlineData("12345678901234", "12345-67890-1234")]
    [InlineData("123456789012345", "12345-67890-12345")]
    [InlineData("1234567890123456", "1234-5678-9012-3456")]
    public static void TextGroupsAreGroupedInput(string input, string expectedTextGroupsString)
    {
        var instance = PlainTextRecoveryCode.Create(input);

        var actualTextGroups = instance.ToTextGroups();

        var actualTextGroupsString = string.Join('-', actualTextGroups);
        actualTextGroupsString.ShouldBe(expectedTextGroupsString);
    }

    [Fact]
    public static void Input_is_normalised_to_upper_case()
    {
        const string input = "abc";
        var expectedText = input.ToUpperInvariant();
        var instance = PlainTextRecoveryCode.Create(input);

        var actualText = instance.Text;

        actualText.ShouldBe(expectedText);
    }

    [Theory]
    [InlineData('I', '1')]
    [InlineData('L', '1')]
    [InlineData('O', '0')]
    public static void AmbiguousCharactersAreMapped(char ambiguousChar, char mappedChar)
    {
        var input = $"123{ambiguousChar}";
        var expectedText = $"123{mappedChar}";
        var instance = PlainTextRecoveryCode.Create(input);

        var actualText = instance.Text;

        actualText.ShouldBe(expectedText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("123456789012345678901234567890123456789012345678901234567890123456789012345678901")]
    [InlineData("U")]
    public static void CannotParseInvalidInputs(string input)
    {
        var ex = Record.Exception(() => _ = PlainTextRecoveryCode.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_type_string()
    {
        const string input = "123";
        var instance = PlainTextRecoveryCode.Create(input);

        var @string = instance.ToString();

        @string.ShouldBe(instance.GetType().ToString());
    }
}
