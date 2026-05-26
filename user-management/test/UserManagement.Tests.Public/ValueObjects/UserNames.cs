// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;

namespace Duende.Platform.UserManagement.ValueObjects;

public static class UserNames
{
    public static TheoryData<string> InvalidInputs { get; } = ["", " ", new string('x', 321)];

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public static void CannotParseInvalidInputs(string input)
    {
        var ex = Record.Exception(() => _ = UserName.Create(input));

        _ = ex.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public static void String_is_input()
    {
        const string input = $"{nameof(UserName)}1";
        var instance = UserName.Create(input);

        var @string = instance.ToString();

        @string.ShouldBe(input);
    }
}
