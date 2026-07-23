// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

/// <summary>Summary of a pool purge operation.</summary>
public sealed record PurgeResult(int EntitiesDeleted, int EntityLinksDeleted, int OutboxEventsDeleted)
{
    /// <summary>Gets a zeroed PurgeResult representing no rows deleted.</summary>
    public static readonly PurgeResult Empty = new(0, 0, 0);
}
