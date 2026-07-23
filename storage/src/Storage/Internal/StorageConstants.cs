// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

internal static class StorageConstants
{
    /// <summary>
    /// Maximum number of expired entities that can be purged in a single batch.
    /// </summary>
    internal const int TtlCleanupMaxBatchSize = 1000;

    /// <summary>
    /// Default batch size for pool purge operations.
    /// </summary>
    internal const int PurgePoolDefaultBatchSize = 10_000;
}
