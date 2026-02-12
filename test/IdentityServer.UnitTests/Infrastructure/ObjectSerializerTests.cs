// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using Duende.IdentityServer.Models;
using FluentAssertions;
using Xunit;

namespace UnitTests.Infrastructure;

public class ObjectSerializerTests
{
    public ObjectSerializerTests()
    {
    }

    [Fact]
    public void Can_be_deserialize_message()
    {
        Action a = () => Duende.IdentityServer.ObjectSerializer.FromString<Message<ErrorMessage>>("{\"created\":0, \"data\": {\"error\": \"error\"}}");
        a.Should().NotThrow();
    }

    [Fact]
    public void Can_serialize_jwk_with_plus_character_in_x5c()
    {
        var jwk = new Dictionary<string, object>
        {
            { "kty", "RSA" },
            { "x5c", new List<string> { "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA+test+value+with+plus" } }
        };

        var json = Duende.IdentityServer.ObjectSerializer.ToUnescapedString(jwk);

        // The '+' character should not be escaped as \u002B
        json.Should().NotContain("\\u002B");
        json.Should().Contain("+");
    }
}
