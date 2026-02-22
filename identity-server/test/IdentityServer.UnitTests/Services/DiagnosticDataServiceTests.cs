// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Services;

namespace IdentityServer.UnitTests.Services;

public class DiagnosticDataServiceTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetJsonBytesAsync_WithNoEntries_ShouldReturnEmptyJsonObject()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>();
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonBytesAsync(_ct);

        var json = Encoding.UTF8.GetString(result.Span);
        json.ShouldBe("{}");
    }

    [Fact]
    public async Task GetJsonBytesAsync_WithSingleEntry_ShouldReturnValidJson()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>
        {
            new TestDiagnosticEntry("TestProperty", "TestValue")
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonBytesAsync(_ct);

        var json = Encoding.UTF8.GetString(result.Span);
        var jsonDoc = JsonDocument.Parse(json);
        jsonDoc.RootElement.GetProperty("TestProperty").GetString().ShouldBe("TestValue");
    }

    [Fact]
    public async Task GetJsonBytesAsync_WithMultipleEntries_ShouldIncludeAllEntries()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>
        {
            new TestDiagnosticEntry("Property1", "Value1"),
            new TestDiagnosticEntry("Property2", "Value2"),
            new TestDiagnosticEntry("Property3", "Value3")
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonBytesAsync(_ct);

        var json = Encoding.UTF8.GetString(result.Span);
        var jsonDoc = JsonDocument.Parse(json);
        jsonDoc.RootElement.GetProperty("Property1").GetString().ShouldBe("Value1");
        jsonDoc.RootElement.GetProperty("Property2").GetString().ShouldBe("Value2");
        jsonDoc.RootElement.GetProperty("Property3").GetString().ShouldBe("Value3");
    }

    [Fact]
    public async Task GetJsonBytesAsync_ShouldPassCorrectDiagnosticContext()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var capturedContext = new TestDiagnosticEntry.ContextCapture();
        var entries = new List<IDiagnosticEntry>
        {
            new TestDiagnosticEntry("TestProperty", "TestValue", capturedContext)
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        await service.GetJsonBytesAsync(_ct);

        capturedContext.Context.ShouldNotBeNull();
        capturedContext.Context.ServerStartTime.ShouldBe(serverStartTime);
        capturedContext.Context.CurrentSeverTime.ShouldBeGreaterThanOrEqualTo(serverStartTime);
    }

    [Fact]
    public async Task GetJsonBytesAsync_ShouldProduceCompactJson()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>
        {
            new TestDiagnosticEntry("Property1", "Value1"),
            new TestDiagnosticEntry("Property2", "Value2")
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonBytesAsync(_ct);

        var json = Encoding.UTF8.GetString(result.Span);
        json.ShouldNotContain("\n");
        json.ShouldNotContain("\r");
        json.ShouldNotContain("  ");
    }

    [Fact]
    public async Task GetJsonStringAsync_WithNoEntries_ShouldReturnEmptyJsonObject()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>();
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonStringAsync(_ct);

        result.ShouldBe("{}");
    }

    [Fact]
    public async Task GetJsonStringAsync_WithSingleEntry_ShouldReturnValidJson()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>
        {
            new TestDiagnosticEntry("TestProperty", "TestValue")
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonStringAsync(_ct);

        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("TestProperty").GetString().ShouldBe("TestValue");
    }

    [Fact]
    public async Task GetJsonStringAsync_WithMultipleEntries_ShouldIncludeAllEntries()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>
        {
            new TestDiagnosticEntry("Property1", "Value1"),
            new TestDiagnosticEntry("Property2", "Value2"),
            new TestDiagnosticEntry("Property3", "Value3")
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonStringAsync(_ct);

        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("Property1").GetString().ShouldBe("Value1");
        jsonDoc.RootElement.GetProperty("Property2").GetString().ShouldBe("Value2");
        jsonDoc.RootElement.GetProperty("Property3").GetString().ShouldBe("Value3");
    }

    [Fact]
    public async Task GetJsonStringAsync_ShouldReturnUtf8EncodedString()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>
        {
            new TestDiagnosticEntry("Property", "Value with émojis 🎉")
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonStringAsync(_ct);

        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.GetProperty("Property").GetString().ShouldBe("Value with émojis 🎉");
    }

    [Fact]
    public async Task GetJsonStringAsync_ShouldMatchGetJsonBytesAsync()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>
        {
            new TestDiagnosticEntry("Property1", "Value1"),
            new TestDiagnosticEntry("Property2", "Value2")
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        var stringResult = await service.GetJsonStringAsync(_ct);
        var bytesResult = await service.GetJsonBytesAsync(_ct);
        var stringFromBytes = Encoding.UTF8.GetString(bytesResult.Span);

        stringResult.ShouldBe(stringFromBytes);
    }

    [Fact]
    public async Task GetJsonBytesAsync_WithComplexEntry_ShouldWriteNestedObjects()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>
        {
            new ComplexTestDiagnosticEntry()
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonBytesAsync(_ct);

        var json = Encoding.UTF8.GetString(result.Span);
        var jsonDoc = JsonDocument.Parse(json);
        var complex = jsonDoc.RootElement.GetProperty("ComplexData");
        complex.GetProperty("StringValue").GetString().ShouldBe("test");
        complex.GetProperty("NumberValue").GetInt32().ShouldBe(42);
        complex.GetProperty("BoolValue").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task GetJsonBytesAsync_WithAsyncEntry_ShouldHandleAsyncWrites()
    {
        var serverStartTime = DateTime.UtcNow.AddMinutes(-5);
        var entries = new List<IDiagnosticEntry>
        {
            new AsyncTestDiagnosticEntry()
        };
        var service = new DiagnosticDataService(serverStartTime, entries);

        var result = await service.GetJsonBytesAsync(_ct);

        var json = Encoding.UTF8.GetString(result.Span);
        var jsonDoc = JsonDocument.Parse(json);
        jsonDoc.RootElement.GetProperty("AsyncData").GetString().ShouldBe("async value");
    }

    // Test helper classes
    private class TestDiagnosticEntry : IDiagnosticEntry
    {
        private readonly string _propertyName;
        private readonly string _propertyValue;
        private readonly ContextCapture _contextCapture;

        public TestDiagnosticEntry(string propertyName, string propertyValue, ContextCapture contextCapture = null)
        {
            _propertyName = propertyName;
            _propertyValue = propertyValue;
            _contextCapture = contextCapture;
        }

        public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
        {
            if (_contextCapture != null)
            {
                _contextCapture.Context = context;
            }
            writer.WriteString(_propertyName, _propertyValue);
            return Task.CompletedTask;
        }

        public class ContextCapture
        {
            public DiagnosticContext Context { get; set; }
        }
    }

    private class ComplexTestDiagnosticEntry : IDiagnosticEntry
    {
        public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
        {
            writer.WritePropertyName("ComplexData");
            writer.WriteStartObject();
            writer.WriteString("StringValue", "test");
            writer.WriteNumber("NumberValue", 42);
            writer.WriteBoolean("BoolValue", true);
            writer.WriteEndObject();
            return Task.CompletedTask;
        }
    }

    private class AsyncTestDiagnosticEntry : IDiagnosticEntry
    {
        public async Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
        {
            await Task.Delay(1);
            writer.WriteString("AsyncData", "async value");
        }
    }
}
