// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(ProtectingMessageValue),
        Message = "Protecting message: {Value}")]
    internal static partial void ProtectingMessageValue(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ErrorUnprotectingMessageCookie),
        Message = "Error unprotecting message cookie")]
    internal static partial void ErrorUnprotectingMessageCookie(this ILogger logger, System.Exception exception);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(UnableToUnprotectCookieCookieName),
        Message = "Unable to unprotect cookie {CookieName}")]
    internal static partial void UnableToUnprotectCookieCookieName(this ILogger logger, System.Exception exception, object cookieName);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(PurgingStaleCookieCookieName),
        Message = "Purging stale cookie: {CookieName}")]
    internal static partial void PurgingStaleCookieCookieName(this ILogger logger, object cookieName);
}
