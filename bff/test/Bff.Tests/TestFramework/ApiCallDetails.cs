// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.Bff.Tests.TestFramework;

internal class TestClaimRecord()
{
    /// <summary>
    /// Serialization friendly claim
    /// </summary>
    /// <param name="type">The type</param>
    /// <param name="value">The Value</param>
    internal TestClaimRecord(string type, object value) : this()
    {
        Type = type;
        Value = value;
    }

    /// <summary>
    /// The type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = default!;

    /// <summary>
    /// The value
    /// </summary>
    [JsonPropertyName("value")]
    public object Value { get; init; } = default!;

    /// <summary>
    /// The value type
    /// </summary>
    [JsonPropertyName("valueType")]
    public string? ValueType { get; init; }
}


internal record ApiCallDetails(HttpMethod Method, string Path, string? Sub, string? ClientId, IEnumerable<TestClaimRecord> Claims)
{
    public required string? Body { get; init; }

    public Dictionary<string, List<string>> RequestHeaders { get; init; } = new();

    public T BodyAs<T>()
    {
        Body.ShouldNotBeNull();
        return JsonSerializer.Deserialize<T>(Body, TestSerializerOptions.Default) ?? throw new NullReferenceException($"result {Body} could not be deserialized to {typeof(T).Name}");
    }
}
