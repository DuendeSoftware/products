// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Totp;

namespace Duende.Platform.UserManagement.ValueObjects;

public static class TotpDeviceNames
{
    public static TheoryData<string> InvalidInputs { get; } = ["", " ", new string('x', 256)];

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public static void CannotParseInvalidInputs(string input)
    {
        var ex = Record.Exception(() => _ = TotpDeviceName.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_input()
    {
        const string input = $"{nameof(TotpDeviceName)}1";
        var instance = TotpDeviceName.Create(input);

        var @string = instance.ToString();

        @string.ShouldBe(input);
    }
}
