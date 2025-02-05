// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using Duende.IdentityServer.Models;
using Shouldly;
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
        a.ShouldNotThrow();
    }
}
