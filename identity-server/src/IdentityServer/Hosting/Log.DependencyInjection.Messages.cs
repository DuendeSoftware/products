// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(StartingServerSideSessionRemoval),
        Message = "Starting server-side session removal")]
    internal static partial void StartingServerSideSessionRemoval(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(TaskCanceledExceptionExiting),
        Message = "TaskCanceledException. Exiting.")]
    internal static partial void TaskCanceledExceptionExiting(this ILogger logger);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(TaskDelayExceptionExceptionMessageExiting),
        Message = "Task.Delay exception: {ExceptionMessage}. Exiting.")]
    internal static partial void TaskDelayExceptionExceptionMessageExiting(this ILogger logger, object exceptionMessage);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(StoppingServerSideSessionRemoval),
        Message = "Stopping server-side session removal")]
    internal static partial void StoppingServerSideSessionRemoval(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ProcessingExpirationForCountExpiredServerSideSessions),
        Message = "Processing expiration for {Count} expired server-side sessions.")]
    internal static partial void ProcessingExpirationForCountExpiredServerSideSessions(this ILogger logger, object count);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ExceptionRemovingExpiredSessions),
        Message = "Exception removing expired sessions")]
    internal static partial void ExceptionRemovingExpiredSessions(this ILogger logger, System.Exception exception);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(SkippedNotLeaseHolder),
        Message = "Skipping server-side session cleanup cycle: another node holds the lease")]
    internal static partial void SkippedNotLeaseHolder(this ILogger logger);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(LeaseLost),
        Message = "Server-side session cleanup lease was lost while a cycle was running")]
    internal static partial void LeaseLost(this ILogger logger);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(LockUnavailable),
        Message = "Server-side session cleanup could not reach the distributed lock; skipping this cycle")]
    internal static partial void LockUnavailable(this ILogger logger);
}
