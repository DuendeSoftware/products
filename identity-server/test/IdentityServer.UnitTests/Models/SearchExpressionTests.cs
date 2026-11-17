// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Models = Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.ModelsTests;

/// <summary>
/// Tests construction, validation, and implicit conversions for
/// <see cref="Models.SearchExpression"/>, which recreates the API surface that Duende.Storage
/// exposed before the merge. Behaviour that matters: the required value and
/// <see cref="Models.SearchExpression.MaxLength"/> validation rules, and the implicit conversion
/// from <see cref="string"/>.
/// </summary>
public class SearchExpressionTests
{
    [Fact]
    public void Create_with_a_valid_expression_succeeds()
    {
        var expr = Models.SearchExpression.Create("displayName eq \"Engineers\"");
        expr.Value.ShouldBe("displayName eq \"Engineers\"");
    }

    [Fact]
    public void Create_with_null_or_whitespace_throws_FormatException()
    {
        var ex = Should.Throw<FormatException>(() => Models.SearchExpression.Create(" "));
        ex.Message.ShouldBe("The value ' ' is not a valid SearchExpression. A value is required.");
    }

    [Fact]
    public void Create_above_MaxLength_throws_FormatException()
    {
        var tooLong = new string('a', Models.SearchExpression.MaxLength + 1);
        var ex = Should.Throw<FormatException>(() => Models.SearchExpression.Create(tooLong));
        ex.Message.ShouldBe($"The value '{tooLong}' is not a valid SearchExpression. Must not exceed {Models.SearchExpression.MaxLength} characters.");
    }

    [Fact]
    public void Create_at_exactly_MaxLength_succeeds()
    {
        var atLimit = new string('a', Models.SearchExpression.MaxLength);
        var expr = Models.SearchExpression.Create(atLimit);
        expr.Value.Length.ShouldBe(Models.SearchExpression.MaxLength);
    }

    [Fact]
    public void CreateOrDefault_with_null_or_empty_returns_null()
    {
        Models.SearchExpression.CreateOrDefault(null).ShouldBeNull();
        Models.SearchExpression.CreateOrDefault("").ShouldBeNull();
    }

    [Fact]
    public void TryCreate_with_valid_value_returns_true()
    {
        Models.SearchExpression.TryCreate("age eq 42", out var result).ShouldBeTrue();
        result!.Value.ShouldBe("age eq 42");
    }

    [Fact]
    public void TryCreate_with_too_long_value_returns_false_with_errors()
    {
        var tooLong = new string('a', Models.SearchExpression.MaxLength + 1);
        Models.SearchExpression.TryCreate(tooLong, out var result, out var errors).ShouldBeFalse();
        result.ShouldBeNull();
        errors.ShouldNotBeNull();
        errors!.ShouldContain($"Must not exceed {Models.SearchExpression.MaxLength} characters.");
    }

    [Fact]
    public void Implicit_conversion_from_string_creates_the_expression()
    {
        Models.SearchExpression expr = "name eq \"Alice\"";
        expr.Value.ShouldBe("name eq \"Alice\"");
    }

    [Fact]
    public void Implicit_conversion_from_invalid_string_throws_FormatException() =>
        Should.Throw<FormatException>(() =>
        {
            Models.SearchExpression expr = "";
            _ = expr;
        });
}
