// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Bff.Internal;

internal static class LogCategories
{
    public const string ManagementEndpoints = "Duende.Bff.ManagementEndpoints";
    public const string RemoteApiEndpoints = "Duende.Bff.RemoteApiEndpoints";
}

internal class OTelParameters
{
    public const string CacheKey = "CacheKey";
    public const string ClientId = "ClientId";
    public const string ClientName = "ClientName";
    public const string ClusterId = "ClusterId";
    public const string Detail = "Detail";
    public const string Error = "Error";
    public const string ErrorDescription = "ErrorDescription";
    public const string Expiration = "Expiration";
    public const string ForceRenewal = "ForceRenewal";
    public const string LocalPath = "LocalPath";
    public const string Method = "Method";
    public const string RequestUrl = "RequestUrl";
    public const string Resource = "Resource";
    public const string RouteId = "RouteId";
    public const string Scheme = "Scheme";
    public const string Sid = "Sid";
    public const string StatusCode = "StatusCode";
    public const string Sub = "Sub";
    public const string TokenHash = "TokenHash";
    public const string TokenType = "TokenType";
    public const string Url = "Url";
    public const string User = "User";
}
internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Error,
        $"Anti-forgery validation failed. local path: '{{{OTelParameters.LocalPath}}}'")]
    public static partial void AntiForgeryValidationFailed(this ILogger logger, string localPath);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Back-channel logout. sub: '{{{OTelParameters.Sub}}}', sid: '{{{OTelParameters.Sid}}}'")]
    public static partial void BackChannelLogout(this ILogger logger, string sub, string sid);

    [LoggerMessage(
        level: LogLevel.Warning,
        message: $"Back-channel logout error. error: '{{{OTelParameters.Error}}}'")]
    public static partial void BackChannelLogoutError(this ILogger logger, string error);

    [LoggerMessage(
        level: LogLevel.Warning,
        message: $"Access token is missing. token type: '{{{OTelParameters.TokenType}}}', local path: '{{{OTelParameters.LocalPath}}}', detail: '{{{OTelParameters.Detail}}}'")]
    public static partial void AccessTokenMissing(this ILogger logger, string tokenType, string localPath, string detail);

    [LoggerMessage(
        level: LogLevel.Warning,
        message: $"Invalid route configuration. Cannot combine a required access token (a call to WithAccessToken) and an optional access token (a call to WithOptionalUserAccessToken). clusterId: '{{{OTelParameters.ClusterId}}}', routeId: '{{{OTelParameters.RouteId}}}'")]
    public static partial void InvalidRouteConfiguration(this ILogger logger, string? clusterId, string routeId);

    [LoggerMessage(
        level: LogLevel.Warning,
        message: $"Failed to request new User Access Token due to: {{{OTelParameters.Error}}}. This can mean that the refresh token is expired or revoked but the cookie session is still active. If the session was not revoked, ensure that the session cookie lifetime is smaller than the refresh token lifetime.")]
    public static partial void FailedToRequestNewUserAccessToken(this ILogger logger, string error);

    [LoggerMessage(
        level: LogLevel.Warning,
        message: $"Failed to request new User Access Token due to: {{{OTelParameters.Error}}}. This likely means that the user's refresh token is expired or revoked. The user's session will be ended, which will force the user to log in.")]
    public static partial void UserSessionRevoked(this ILogger logger, string error);
}
