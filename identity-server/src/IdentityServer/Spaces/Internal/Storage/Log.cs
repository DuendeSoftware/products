// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.Spaces.Internal.Storage;

internal static partial class Log
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Purging pool {PoolId} failed in storage instance {StorageInstanceId}")]
    public static partial void PurgePoolFailed(this ILogger logger, Exception exception, int poolId, StorageInstanceId storageInstanceId);
}
