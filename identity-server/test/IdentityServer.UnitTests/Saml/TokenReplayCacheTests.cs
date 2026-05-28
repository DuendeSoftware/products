// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.Sp.Tokens;

namespace UnitTests.Saml;

public class TokenReplayCacheTests
{
    [Fact]
    public void try_add_returns_true_for_first_insert_and_false_for_duplicate()
    {
        var sut = new TokenReplayCache();
        var expiresOn = DateTime.UtcNow.AddMinutes(5);

        sut.TryAdd("token", expiresOn).ShouldBeTrue();
        sut.TryAdd("token", expiresOn).ShouldBeFalse();
    }

    [Fact]
    public void try_find_returns_true_when_token_is_cached()
    {
        var sut = new TokenReplayCache();
        var expiresOn = DateTime.UtcNow.AddMinutes(5);

        sut.TryAdd("token", expiresOn);

        sut.TryFind("token").ShouldBeTrue();
    }
}
