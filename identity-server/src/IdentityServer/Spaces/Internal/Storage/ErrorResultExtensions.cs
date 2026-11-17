// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage;
using Duende.Storage.Internal;

namespace Duende.Spaces.Internal.Storage;

/// <summary>
/// Extension members for <see cref="ErrorResult"/>.
/// </summary>
internal static class ErrorResultExtensions
{
    extension(ErrorResult)
    {
        /// <summary>
        /// Creates an error indicating that purging a pool failed in one or more storage instances.
        /// </summary>
        public static ErrorResult PurgeIncomplete(int poolId, IReadOnlyList<StorageInstanceId> failedInstances) =>
            new("purge_incomplete",
                $"Purging pool {poolId} failed in storage instance(s) {string.Join(", ", failedInstances)}. " +
                "The space record was kept. Call PurgeAsync again to retry.");
    }
}
