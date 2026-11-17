// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Otp;

namespace Duende.Platform.UserManagement.ValueObjects;

public static class OtpChannels
{
    public static TheoryData<string> InvalidInputs { get; } = ["", " ", new string('x', 256)];

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public static void CannotParseInvalidInputs(string input)
    {
        var ex = Record.Exception(() => _ = OtpChannel.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_input()
    {
        const string input = $"{nameof(OtpChannel)}1";
        var instance = OtpChannel.Create(input);

        var @string = instance.ToString();

        @string.ShouldBe(input);
    }
}
