// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Totp;

namespace Duende.Platform.UserManagement.ValueObjects;

public static class PlainTextTotps
{
    [Theory]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("U")]
    public static void CannotParseInvalidInputs(string input)
    {
        var ex = Record.Exception(() => _ = PlainTextTotp.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_type_string()
    {
        const string input = "123";
        var instance = PlainTextTotp.Create(input);

        var @string = instance.ToString();

        @string.ShouldBe(instance.GetType().ToString());
    }
}
