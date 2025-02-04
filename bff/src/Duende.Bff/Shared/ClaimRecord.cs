// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.Bff;

/// <summary>
/// Serialization friendly claim
/// </summary>
public class ClaimRecord()
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <param name="value"></param>
    public ClaimRecord(string type, object value) : this()
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