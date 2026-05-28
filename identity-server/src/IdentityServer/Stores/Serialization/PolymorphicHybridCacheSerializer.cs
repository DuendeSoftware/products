// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.IdentityServer.Stores.Serialization;

/// <summary>
/// Generic <see cref="IHybridCacheSerializer{T}"/> that uses the
/// <see cref="PolymorphicJsonTypeResolver"/> to correctly serialize and
/// deserialize types with runtime-registered polymorphic subtypes.
/// </summary>
internal class PolymorphicHybridCacheSerializer<T> : IHybridCacheSerializer<T>
{
    private readonly JsonSerializerOptions _jsonOptions;

    public PolymorphicHybridCacheSerializer(PolymorphicJsonTypeResolver resolver) =>
        _jsonOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver
        };

    public T Deserialize(ReadOnlySequence<byte> source)
    {
        var reader = new Utf8JsonReader(source);
        return JsonSerializer.Deserialize<T>(ref reader, _jsonOptions)!;
    }

    public void Serialize(T value, IBufferWriter<byte> target)
    {
        using var writer = new Utf8JsonWriter(target);
        JsonSerializer.Serialize(writer, value, _jsonOptions);
    }
}
