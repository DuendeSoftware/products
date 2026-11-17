// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting;

internal static partial class StoragePurgeLog
{
    [LoggerMessage(
        EventName = nameof(StartingPurge),
        Message = "Starting storage expiration purge")]
    internal static partial void StartingPurge(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(StoppingPurge),
        Message = "Stopping storage expiration purge")]
    internal static partial void StoppingPurge(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(PurgedBatch),
        Message = $"Purged {{{LogParameters.Count}}} expired entities from storage")]
    internal static partial void PurgedBatch(this ILogger logger, LogLevel logLevel, int count);

    [LoggerMessage(
        EventName = nameof(PurgeCancelled),
        Message = "Purge cancelled")]
    internal static partial void PurgeCancelled(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(CancellationRequested),
        Message = "Cancellation requested during delay. Exiting.")]
    internal static partial void CancellationRequested(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(DelayException),
        Message = $"Task.Delay exception: {{{LogParameters.ExceptionMessage}}}. Exiting.")]
    internal static partial void DelayException(this ILogger logger, LogLevel logLevel, string exceptionMessage);

    [LoggerMessage(
        EventName = nameof(PurgeException),
        Message = $"Exception purging expired entities from storage instance {{{LogParameters.StorageInstanceId}}}")]
    internal static partial void PurgeException(this ILogger logger, LogLevel logLevel, Exception exception, StorageInstanceId storageInstanceId);

    [LoggerMessage(
        EventName = nameof(SkippedNotLeaseHolder),
        Message = "Skipping storage purge cycle: another node holds the lease")]
    internal static partial void SkippedNotLeaseHolder(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(LeaseLost),
        Message = "Storage purge lease was lost while a cycle was running")]
    internal static partial void LeaseLost(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(LockUnavailable),
        Message = "Storage purge could not reach the distributed lock; skipping this cycle")]
    internal static partial void LockUnavailable(this ILogger logger, LogLevel logLevel);
}

internal static class LogParameters
{
    public const string Count = "Count";
    public const string ExceptionMessage = "ExceptionMessage";
    public const string StorageInstanceId = "StorageInstanceId";
}
