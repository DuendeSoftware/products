// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.Storage;

/// <summary>
/// Represents a pool identifier.
/// </summary>
[ValueOf<int>]
[JsonConverter(typeof(PoolIdJsonConverter))]
public partial record PoolId
{
    /// <summary>The default pool identifier (pool 0), used for single-space deployments.</summary>
    public static readonly PoolId Default = 0;
}

/// <summary>
/// JSON converter for <see cref="PoolId"/>.
/// </summary>
internal sealed class PoolIdJsonConverter : JsonConverter<PoolId>
{
    public override PoolId Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
        reader.GetInt32();

    public override void Write(System.Text.Json.Utf8JsonWriter writer, PoolId value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}
