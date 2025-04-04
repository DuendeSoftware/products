// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Bff.Blazor.Client.Internals;

internal static partial class Log
{
    [LoggerMessage(
        level: LogLevel.Information,
        message: "Fetching user information.")]
    public static partial void FetchingUserInformation(this ILogger logger);

    [LoggerMessage(
        level: LogLevel.Warning,
        message: "Fetching user failed.")]
    public static partial void FetchingUserFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: "Failed to load persisted user.")]
    public static partial void FailedToLoadPersistedUser(this ILogger logger);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: "Persisted user loaded.")]
    public static partial void PersistedUserLoaded(this ILogger logger);

}
