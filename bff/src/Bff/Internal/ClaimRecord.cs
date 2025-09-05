// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.Bff.Internal;

/// <summary>
/// Serialization friendly claim
/// </summary>
internal class ClaimRecord()
{
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

}
