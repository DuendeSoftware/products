// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;

namespace Duende.Platform.UserManagement.ValueObjects;

public static class PhoneNumbers
{
    [Theory]
    [InlineData("")]
    [InlineData("1234567890123456")]
    [InlineData("a")]
    public static void CannotParseInvalidInputs(string input)
    {
        var ex = Record.Exception(() => _ = PhoneNumber.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_input()
    {
        const string input = "1234567890";
        var instance = PhoneNumber.Create(input);

        var @string = instance.ToString();

        @string.ShouldBe(input);
    }
}
