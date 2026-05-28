// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Common;

namespace UnitTests.Saml;

public sealed class DateTimeUtcTests
{
    private static readonly DateTimeUtc Earlier = new(2025, 1, 1, 0, 0, 0);
    private static readonly DateTimeUtc Later = new(2025, 6, 1, 0, 0, 0);

    [Fact]
    public void LessThanReturnsTrueWhenDateTimeOffsetIsEarlier()
    {
        DateTimeOffset dto = Earlier;
        (dto < Later).ShouldBeTrue();
    }

    [Fact]
    public void LessThanReturnsFalseWhenDateTimeOffsetIsLater()
    {
        DateTimeOffset dto = Later;
        (dto < Earlier).ShouldBeFalse();
    }

    [Fact]
    public void GreaterThanReturnsTrueWhenDateTimeOffsetIsLater()
    {
        DateTimeOffset dto = Later;
        (dto > Earlier).ShouldBeTrue();
    }

    [Fact]
    public void GreaterThanReturnsFalseWhenDateTimeOffsetIsEarlier()
    {
        DateTimeOffset dto = Earlier;
        (dto > Later).ShouldBeFalse();
    }

    [Fact]
    public void GreaterOrEqualReturnsTrueWhenEqual()
    {
        DateTimeOffset dto = Earlier;
        (dto >= Earlier).ShouldBeTrue();
    }

    [Fact]
    public void GreaterOrEqualReturnsTrueWhenLater()
    {
        DateTimeOffset dto = Later;
        (dto >= Earlier).ShouldBeTrue();
    }

    [Fact]
    public void GreaterOrEqualReturnsFalseWhenEarlier()
    {
        DateTimeOffset dto = Earlier;
        (dto >= Later).ShouldBeFalse();
    }

    [Fact]
    public void LessOrEqualReturnsTrueWhenEqual()
    {
        DateTimeOffset dto = Earlier;
        (dto <= Earlier).ShouldBeTrue();
    }

    [Fact]
    public void LessOrEqualReturnsTrueWhenEarlier()
    {
        DateTimeOffset dto = Earlier;
        (dto <= Later).ShouldBeTrue();
    }

    [Fact]
    public void LessOrEqualReturnsFalseWhenLater()
    {
        DateTimeOffset dto = Later;
        (dto <= Earlier).ShouldBeFalse();
    }
}
