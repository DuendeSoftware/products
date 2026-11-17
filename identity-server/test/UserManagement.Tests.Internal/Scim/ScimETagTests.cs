// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Scim.Internal;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimETagTests
{
    [Fact]
    public void ImplicitConversionProducesCorrectValue()
    {
        ScimETag etag = 1;
        etag.Value.ShouldBe(1);
    }

    [Fact]
    public void ToHeaderValueProducesWeakETagFormat()
    {
        ScimETag etag = 1;
        etag.ToHeaderValue().ShouldBe("W/\"1\"");
    }

    [Fact]
    public void ToHeaderValueWithLargerNumberProducesCorrectFormat()
    {
        ScimETag etag = 42;
        etag.ToHeaderValue().ShouldBe("W/\"42\"");
    }

    [Fact]
    public void ParseSucceedsForWeakETag()
    {
        var etag = ScimETag.Create("W/\"42\"");
        etag.Value.ShouldBe(42);
    }

    [Fact]
    public void ParseSucceedsForStrongETag()
    {
        var etag = ScimETag.Create("\"42\"");
        etag.Value.ShouldBe(42);
    }

    [Fact]
    public void ParseThrowsForInvalidString() =>
        _ = Should.Throw<FormatException>(() =>
            ScimETag.Create("invalid"));

    [Fact]
    public void ParseThrowsForNonNumericWeakETag() =>
        _ = Should.Throw<FormatException>(() =>
            ScimETag.Create("W/\"abc\""));

    [Fact]
    public void TryParseFailsForEmptyString() =>
        ScimETag.TryCreate(string.Empty, out _).ShouldBeFalse();

    [Fact]
    public void TryParseFailsForNull() =>
        ScimETag.TryCreate(null, out _).ShouldBeFalse();

    [Fact]
    public void RoundTripVersionPreservesValue()
    {
        var original = 123;
        ScimETag etag = original;
        etag.Value.ShouldBe(original);
        etag.ToHeaderValue().ShouldBe("W/\"123\"");
    }

    [Fact]
    public void RoundTripThroughParsePreservesValue()
    {
        ScimETag original = 42;
        var headerValue = original.ToHeaderValue();
        var parsed = ScimETag.Create(headerValue);
        parsed.Value.ShouldBe(original.Value);
    }

    [Fact]
    public void ParseSucceedsForWildcard()
    {
        var etag = ScimETag.Create("*");
        etag.IsAny.ShouldBeTrue();
    }

    [Fact]
    public void TryParseSucceedsForWildcard()
    {
        ScimETag.TryCreate("*", out var etag).ShouldBeTrue();
        etag.IsAny.ShouldBeTrue();
    }

    [Fact]
    public void WildcardMatchesAnyVersion()
    {
        var etag = ScimETag.Any;
        etag.Matches(1).ShouldBeTrue();
        etag.Matches(42).ShouldBeTrue();
        etag.Matches(0).ShouldBeTrue();
    }

    [Fact]
    public void SpecificETagMatchesOnlySameVersion()
    {
        ScimETag etag = 42;
        etag.Matches(42).ShouldBeTrue();
        etag.Matches(1).ShouldBeFalse();
        etag.Matches(0).ShouldBeFalse();
    }

    [Fact]
    public void SpecificETagIsNotAny()
    {
        ScimETag etag = 42;
        etag.IsAny.ShouldBeFalse();
    }

    [Fact]
    public void AnyHasSentinelValue() =>
        ScimETag.Any.Value.ShouldBe(-1);
}
