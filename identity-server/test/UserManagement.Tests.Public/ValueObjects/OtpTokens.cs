// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Otp;

namespace Duende.Platform.UserManagement.ValueObjects;

public static class OtpTokens
{
    [Fact]
    public static void Cannot_parse_UuidV4()
    {
        var input = Guid.NewGuid().ToString();

        var ex = Record.Exception(() => OtpToken.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_input()
    {
        var input = Guid.CreateVersion7().ToString();
        var instance = OtpToken.Create(input);

        var @string = instance.ToString();

        @string.ShouldBe(input);
    }
}
