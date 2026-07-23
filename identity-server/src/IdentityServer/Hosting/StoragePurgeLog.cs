// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

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
        Message = "Exception purging expired entities from storage")]
    internal static partial void PurgeException(this ILogger logger, LogLevel logLevel, Exception exception);
}

internal static class LogParameters
{
    public const string Count = "Count";
    public const string ExceptionMessage = "ExceptionMessage";
}
