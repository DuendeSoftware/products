// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal;

namespace Duende.Platform.UserManagement;

public static class Base32CrockfordEncoding
{
    [Theory]
    [InlineData("0123456789", "0123456789")]
    [InlineData("ABCDEFGHJKMNPQRSTVWXYZ", "ABCDEFGHJKMNPQRSTVWXYZ")]
    [InlineData("abcdefghjkmnpqrstvwxyz", "ABCDEFGHJKMNPQRSTVWXYZ")]
    [InlineData("i", "1")]
    [InlineData("I", "1")]
    [InlineData("l", "1")]
    [InlineData("L", "1")]
    [InlineData("o", "0")]
    [InlineData("O", "0")]
    public static void Normalizes_valid_characters(string input, string expected)
    {
        var normalized = Base32Crockford.Normalize(input);

        normalized.ShouldBe(expected);
    }

    [Theory]
    [InlineData("A-B-C", "ABC")]
    [InlineData("01-23", "0123")]
    public static void Strips_hyphens(string input, string expected)
    {
        var normalized = Base32Crockford.Normalize(input);

        normalized.ShouldBe(expected);
    }

    [Theory]
    [InlineData("0123456789", 10)]
    [InlineData("ABCDEFGHJKMNPQRSTVWXYZ", 22)]
    [InlineData("ABC", 3)]
    [InlineData("ABC", 10)]
    [InlineData("", 10)]
    [InlineData("", 0)]
    public static void IsValid_accepts_valid_input(string input, byte maxLength) =>
        Base32Crockford.IsValid(input, maxLength).ShouldBeTrue();

    [Theory]
    [InlineData("u")]
    [InlineData("U")]
    public static void IsValid_rejects_excluded_character_U(string input)
    {
        var normalized = Base32Crockford.Normalize(input);

        Base32Crockford.IsValid(normalized, 1).ShouldBeFalse();
    }

    [Theory]
    [InlineData("!")]
    [InlineData("@")]
    [InlineData(" ")]
    [InlineData("\t")]
    public static void IsValid_rejects_invalid_characters(string input)
    {
        var normalized = Base32Crockford.Normalize(input);

        Base32Crockford.IsValid(normalized, 1).ShouldBeFalse();
    }

    [Fact]
    public static void IsValid_rejects_input_exceeding_max_length() =>
        Base32Crockford.IsValid("ABCDE", 4).ShouldBeFalse();

    [Fact]
    public static void Normalize_empty_input_returns_empty_string() =>
        Base32Crockford.Normalize("").ShouldBe(string.Empty);

    [Fact]
    public static void Normalize_hyphens_only_returns_empty_string() =>
        Base32Crockford.Normalize("-").ShouldBe(string.Empty);

    [Fact]
    public static void Random_generates_correct_length()
    {
        var result = Base32Crockford.Random(10, false);

        result.Length.ShouldBe(10);
    }

    [Fact]
    public static void Random_numeric_only_contains_only_digits()
    {
        var result = Base32Crockford.Random(100, true);

        result.ShouldAllBe(c => c >= '0' && c <= '9');
    }

    [Fact]
    public static void Random_all_chars_contains_only_valid_Crockford_characters()
    {
        var result = Base32Crockford.Random(100, false);

        result.ShouldAllBe(c => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(c));
    }
}
