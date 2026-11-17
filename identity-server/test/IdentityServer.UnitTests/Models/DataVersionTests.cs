// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Models = Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.ModelsTests;

/// <summary>
/// Tests construction, parsing, and implicit conversions for <see cref="Models.DataVersion"/>,
/// which recreates the API surface that Duende.Storage exposed before the merge. Behaviour that
/// matters: parsing valid and invalid strings, and the implicit conversion from <see cref="int"/>.
/// </summary>
public class DataVersionTests
{
    [Fact]
    public void Create_parses_a_valid_integer_string()
    {
        var version = Models.DataVersion.Create("42");
        version.Value.ShouldBe(42);
    }

    [Fact]
    public void Create_with_non_numeric_string_throws_FormatException()
    {
        var ex = Should.Throw<FormatException>(() => Models.DataVersion.Create("abc"));
        ex.Message.ShouldBe("The value 'abc' is not a valid 'DataVersion'. The value could not be parsed.");
    }

    [Fact]
    public void Create_with_null_or_whitespace_throws_FormatException()
    {
        var ex = Should.Throw<FormatException>(() => Models.DataVersion.Create(" "));
        ex.Message.ShouldBe("The value ' ' is not a valid 'DataVersion'. A value is required.");
    }

    [Fact]
    public void CreateOrDefault_with_null_or_empty_returns_null()
    {
        Models.DataVersion.CreateOrDefault(null).ShouldBeNull();
        Models.DataVersion.CreateOrDefault("").ShouldBeNull();
    }

    [Fact]
    public void CreateOrDefault_with_valid_value_returns_instance()
    {
        var version = Models.DataVersion.CreateOrDefault("7");
        version.ShouldNotBeNull();
        version!.Value.ShouldBe(7);
    }

    [Fact]
    public void TryCreate_with_valid_value_returns_true()
    {
        Models.DataVersion.TryCreate("3", out var result).ShouldBeTrue();
        result!.Value.ShouldBe(3);
    }

    [Fact]
    public void TryCreate_with_invalid_value_returns_false_with_errors()
    {
        Models.DataVersion.TryCreate("not-a-number", out var result, out var errors).ShouldBeFalse();
        result.ShouldBeNull();
        errors.ShouldNotBeNull();
        errors!.ShouldContain("The value could not be parsed.");
    }

    [Fact]
    public void Implicit_conversion_from_int_wraps_the_value()
    {
        Models.DataVersion version = 5;
        version.Value.ShouldBe(5);
    }

    [Fact]
    public void ToString_returns_the_invariant_culture_representation() => ((Models.DataVersion)123).ToString().ShouldBe("123");
}
