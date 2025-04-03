// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Bff.General;

internal static class LogCategories
{
    public const string ManagementEndpoints = "Duende.Bff.ManagementEndpoints";
    public const string RemoteApiEndpoints = "Duende.Bff.RemoteApiEndpoints";
}

internal static class EventIds
{
    public static readonly EventId AntiForgeryValidationFailed = new(1, "AntiForgeryValidationFailed");
    public static readonly EventId BackChannelLogout = new(2, "BackChannelLogout");
    public static readonly EventId BackChannelLogoutError = new(3, "BackChannelLogoutError");
    public static readonly EventId AccessTokenMissing = new(4, "AccessTokenMissing");
    public static readonly EventId InvalidRouteConfiguration = new(5, "InvalidRouteConfiguration");
}

internal static class Log
{
    private static readonly Action<ILogger, string, Exception?> AntiForgeryValidationFailedMessage = LoggerMessage.Define<string>(
        LogLevel.Error,
        EventIds.AntiForgeryValidationFailed,
        "Anti-forgery validation failed. local path: '{localPath}'");

    private static readonly Action<ILogger, string, string, Exception?> BackChannelLogoutMessage = LoggerMessage.Define<string, string>(
        LogLevel.Information,
        EventIds.BackChannelLogout,
        "Back-channel logout. sub: '{sub}', sid: '{sid}'");

    private static readonly Action<ILogger, string, Exception?> BackChannelLogoutErrorMessage = LoggerMessage.Define<string>(
        LogLevel.Information,
        EventIds.BackChannelLogoutError,
        "Back-channel logout error. error: '{error}'");

    private static readonly Action<ILogger, string, string, string, Exception?> AccessTokenMissingMessage = LoggerMessage.Define<string, string, string>(
        LogLevel.Warning,
        EventIds.AccessTokenMissing,
        "Access token is missing. token type: '{tokenType}', local path: '{localpath}', detail: '{detail}'");

    private static readonly Action<ILogger, string, string, Exception?> InvalidRouteConfigurationMessage = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        EventIds.InvalidRouteConfiguration,
        "Invalid route configuration. Cannot combine a required access token (a call to WithAccessToken) and an optional access token (a call to WithOptionalUserAccessToken). clusterId: '{clusterId}', routeId: '{routeId}'");

    private static readonly Action<ILogger, string, Exception?> FailedToRequestNewTokenMessage = LoggerMessage.Define<string>(
        LogLevel.Warning,
        EventIds.InvalidRouteConfiguration,
        "Failed to request new User Access Token due to: {message}. This can mean that the refresh token is expired or revoked but the cookie session is still active. If the session was not revoked, ensure that the session cookie lifetime is smaller than the refresh token lifetime.");

    private static readonly Action<ILogger, string, Exception?> UserSessionRevokedMessage = LoggerMessage.Define<string>(
        LogLevel.Warning,
        EventIds.InvalidRouteConfiguration,
        "Failed to request new User Access Token due to: {message}. This likely means that the user's refresh token is expired or revoked. The user's session will be ended, which will force the user to log in.");

    public static void AntiForgeryValidationFailed(this ILogger logger, string localPath)
    {
        AntiForgeryValidationFailedMessage(logger, localPath, null);
    }

    public static void BackChannelLogout(this ILogger logger, string sub, string sid)
    {
        BackChannelLogoutMessage(logger, sub, sid, null);
    }

    public static void BackChannelLogoutError(this ILogger logger, string error)
    {
        BackChannelLogoutErrorMessage(logger, error, null);
    }

    public static void AccessTokenMissing(this ILogger logger, string tokenType, string localPath, string detail)
    {
        AccessTokenMissingMessage(logger, tokenType, localPath, detail, null);
    }

    public static void InvalidRouteConfiguration(this ILogger logger, string? clusterId, string routeId)
    {
        InvalidRouteConfigurationMessage(logger, clusterId ?? "no cluster id", routeId, null);
    }

    public static void FailedToRequestNewUserAccessToken(this ILogger logger, string message)
    {
        FailedToRequestNewTokenMessage(logger, message, null);
    }

    public static void UserSessionRevoked(this ILogger logger, string message)
    {
        UserSessionRevokedMessage(logger, message, null);
    }
}
