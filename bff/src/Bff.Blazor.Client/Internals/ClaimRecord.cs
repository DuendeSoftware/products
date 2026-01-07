// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.Bff.Blazor.Client.Internals;

/// <summary>
/// Serialization friendly claim.
///
/// Note, this is a copy of the ClaimRecord class from Duende.Bff, but since we can't create a reference to it, we need to copy it here.
/// We also can't link to it (as we do with the extensions) because the other ClaimRecord class is public and this one is intentionally internal.
/// </summary>
internal class ClaimRecord()
{
    /// <summary>
    /// Serialization friendly claim
    /// </summary>
    /// <param name="type">The type</param>
    /// <param name="value">The Value</param>
    internal ClaimRecord(string type, object value) : this()
    {
        Type = type;
        Value = value;
    }

    /// <summary>
    /// The type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// The value
    /// </summary>
    [JsonPropertyName("value")]
    public object Value { get; init; } = string.Empty;

    /// <summary>
    /// The value type
    /// </summary>
    [JsonPropertyName("valueType")]
    public string? ValueType { get; init; }
}
