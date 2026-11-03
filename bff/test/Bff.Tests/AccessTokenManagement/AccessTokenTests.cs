// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;

namespace Duende.Bff.Tests.AccessTokenManagement;

public class AccessTokenTests
{
    [Fact]
    public void Parse_accepts_values_longer_than_the_former_32kb_limit()
    {
        var value = new string('a', 32 * 1024 + 1);

        var token = AccessToken.Parse(value);

        token.ToString().ShouldBe(value);
    }

    [Fact]
    public void TryParse_accepts_values_longer_than_the_former_32kb_limit()
    {
        var value = new string('a', 32 * 1024 + 1);

        var success = AccessToken.TryParse(value, out var parsed, out var errors);

        success.ShouldBeTrue();
        _ = parsed.ShouldNotBeNull();
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_still_rejects_whitespace_without_a_length_rule()
    {
        Action action = () => AccessToken.Parse(" ");

        _ = action.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void TryParse_still_rejects_whitespace_without_a_length_rule()
    {
        var success = AccessToken.TryParse(" ", out var parsed, out var errors);

        success.ShouldBeFalse();
        parsed.ShouldBeNull();
        errors.ShouldNotBeEmpty();
    }
}
