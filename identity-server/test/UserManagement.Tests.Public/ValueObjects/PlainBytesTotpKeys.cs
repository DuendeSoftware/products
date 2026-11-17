// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Totp;

namespace Duende.Platform.UserManagement.ValueObjects;

public static class PlainBytesTotpKeys
{
    // https://datatracker.ietf.org/doc/html/rfc4648#section-10
    [Theory]
    [InlineData("", "")]
    [InlineData("MY======", "MY==-====")]
    [InlineData("MZXQ====", "MZXQ-====")]
    [InlineData("MZXW6===", "MZXW-6===")]
    [InlineData("MZXW6YQ=", "MZXW-6YQ=")]
    [InlineData("MZXW6YTB", "MZXW-6YTB")]
    [InlineData("MZXW6YTBOI======", "MZXW-6YTB-OI==-====")]
    public static void CanRoundTripBase32(string base32, string expectedBase32GroupsString)
    {
        var key = PlainBytesTotpKey.DecodeFromBase32(base32);

        var roundTrippedBase32 = key.EncodeToBase32Groups();

        var actualBase32GroupsString = string.Join('-', roundTrippedBase32);
        actualBase32GroupsString.ShouldBe(expectedBase32GroupsString);
    }

    [Theory]
    [InlineData(' ')]
    [InlineData('/')] // ASCII 0x2F
    [InlineData('0')] // ASCII 0x30
    [InlineData('1')] // ASCII 0x31
    [InlineData('8')] // ASCII 0x38
    [InlineData('9')] // ASCII 0x39
    [InlineData(':')] // ASCII 0x3A
    [InlineData('@')] // ASCII 0x40 (one before 'A' 0x41)
    [InlineData('[')] // ASCII 0x5B (one after 'Z' 0x5A)
    [InlineData('`')] // ASCII 0x60 (one before 'a' 0x61)
    [InlineData('{')] // ASCII 0x7B (one after 'z' 0x7A)
    public static void CannotDecodeInvalidInputsFromBase32(char input)
    {
        var ex = Record.Exception(() => _ = PlainBytesTotpKey.DecodeFromBase32($"{input}"));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_type_string()
    {
        var instance = PlainBytesTotpKey.DecodeFromBase32("ABC");

        var @string = instance.ToString();

        @string.ShouldBe(instance.GetType().ToString());
    }
}
