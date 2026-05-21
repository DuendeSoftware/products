// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Wraps a query result item with entity metadata: id, version, and timestamps.
/// </summary>
/// <typeparam name="TValue">The type of the wrapped value.</typeparam>
public sealed record MetadataEnvelope<TValue>(
    TValue Value,
    Guid Id,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUpdatedAt);
