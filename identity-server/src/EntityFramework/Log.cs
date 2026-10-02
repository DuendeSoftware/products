// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(StartingGrantRemoval),
        Level = LogLevel.Debug,
        Message = "Starting grant removal")]
    internal static partial void StartingGrantRemoval(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StoppingGrantRemoval),
        Level = LogLevel.Debug,
        Message = "Stopping grant removal")]
    internal static partial void StoppingGrantRemoval(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(TokenCleanupCancellationRequested),
        Level = LogLevel.Debug,
        Message = "CancellationRequested. Exiting.")]
    internal static partial void TokenCleanupCancellationRequested(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(TaskCanceled),
        Level = LogLevel.Debug,
        Message = "TaskCanceledException. Exiting.")]
    internal static partial void TaskCanceled(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(TaskDelayFailed),
        Level = LogLevel.Error,
        Message = "Task.Delay exception: {ExceptionMessage}. Exiting.")]
    internal static partial void TaskDelayFailed(this ILogger logger, string exceptionMessage);

    [LoggerMessage(
        EventName = nameof(ExpiredGrantRemovalFailed),
        Level = LogLevel.Error,
        Message = "Exception removing expired grants: {Exception}")]
    internal static partial void ExpiredGrantRemovalFailed(this ILogger logger, string exception);

    [LoggerMessage(
        EventName = nameof(OriginAllowed),
        Level = LogLevel.Debug,
        Message = "Origin {Origin} is allowed: {OriginAllowed}")]
    internal static partial void OriginAllowed(this ILogger logger, string origin, bool originAllowed);
}
