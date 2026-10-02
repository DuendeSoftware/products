// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.ServerSideSessions;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(FailedToCreateSessionWithKeyKeyResult),
        Message = "Failed to create session with key '{Key}'. Result: {Result}.")]
    internal static partial void FailedToCreateSessionWithKeyKeyResult(this ILogger logger, object key, object result);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoServerSideSessionKeyFoundUpdateSkipped),
        Message = "No server-side session '{Key}' found. Update skipped.")]
    internal static partial void NoServerSideSessionKeyFoundUpdateSkipped(this ILogger logger, object key);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(SessionWithKeyKeyCouldNotBeUpdated),
        Message = "Session with key '{Key}' could not be updated. Result: {Result}. " +
                "The session may have been concurrently modified or deleted.")]
    internal static partial void SessionWithKeyKeyCouldNotBeUpdated(this ILogger logger, object key, object result);
}
