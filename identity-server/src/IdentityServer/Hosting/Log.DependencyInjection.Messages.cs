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
}
