// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.Bff.Tests.TestFramework
{
    public record JsonRecord(string Type, JsonElement Value)
    {
        [JsonPropertyName("type")]
        public string Type { get; } = Type;

        [JsonPropertyName("value")]
        public JsonElement Value { get; } = Value;
    }
}
