// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Duende.IdentityServer.Infrastructure;

namespace IdentityServer.UnitTests.Infrastructure;

public class RemovePropertyModifierTests
{
    [Fact]
    public void ShouldRemoveSpecifiedProperties()
    {
        var testObject = new TestClass { Property1 = "Value1", Property2 = "Value2", Property3 = "Value3" };
        var modifier = new RemovePropertyModifier<TestClass>([nameof(TestClass.Property1)]);
        var serializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { modifier.ModifyTypeInfo }
            }
        };

        var result = JsonSerializer.Serialize(testObject, serializerOptions);

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("Property1", out _).ShouldBeFalse();
        json.RootElement.TryGetProperty("Property2", out var property2).ShouldBeTrue();
        property2.GetString().ShouldBe(testObject.Property2);
        json.RootElement.TryGetProperty("Property3", out var property3).ShouldBeTrue();
        property3.GetString().ShouldBe(testObject.Property3);
    }

    private class TestClass
    {
        public string Property1 { get; init; }
        public string Property2 { get; init; }
        public string Property3 { get; init; }
    }
}
