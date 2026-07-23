// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.MultiSpace;

/// <summary>
/// Represents a strongly-typed pool identifier for a space.
/// </summary>
[ValueOf<int>]
[JsonConverter(typeof(PoolIdJsonConverter))]
public partial record PoolId
{
    /// <summary>The default pool (pool 0) used for unresolved requests.</summary>
    public static readonly PoolId Default = 0;

    internal static bool TryValidate(int value, out IReadOnlyList<string>? errors)
    {
        errors = null;
        if (value < 0)
        {
            errors = ["Pool ID must be non-negative."];
            return false;
        }

        return true;
    }
}

internal sealed class PoolIdJsonConverter : JsonConverter<PoolId>
{
    public override PoolId? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var value = reader.GetInt32();
        return PoolId.Load(value);
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, PoolId value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}
