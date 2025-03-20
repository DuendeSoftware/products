// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace UnitTests.Infrastructure;

public class ObjectSerializerTests
{
    [Fact]
    public void Can_be_deserialize_message()
    {
        Action a = () => Duende.IdentityServer.ObjectSerializer.FromString<Message<ErrorMessage>>("{\"created\":0, \"data\": {\"error\": \"error\"}}");
        a.ShouldNotThrow();
    }

    [Fact]
    public void Can_serialize_and_deserialize_dictionary()
    {
        var jsonObject = new Dictionary<string, object>
        {
            { "key", "value" },
            { "key2", new { key = "value" } },
            { "key3", new List<string> { "value1", "value2" } },
            {
                "key4", new Dictionary<string, string>
                {
                    { "key1", "value1" },
                    { "key2", "value2" }
                }
            }
        };

        var json = Duende.IdentityServer.ObjectSerializer.ToString(jsonObject);
        var result = Duende.IdentityServer.ObjectSerializer.FromString<Dictionary<string, object>>(json);

        result.ShouldNotBeNull();
    }
}
