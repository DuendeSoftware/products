// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Otel;

internal static partial class LogMessages
{
    [LoggerMessage(
        Message = "FrontendSelection: No frontends registered in the store.")]
    public static partial void NoFrontendsRegistered(ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        $"Invalid prompt value {{{OTelParameters.Prompt}}}.")]
    public static partial void InvalidPromptValue(this ILogger logger, LogLevel logLevel, string prompt);

    [LoggerMessage(
        $"Invalid return url {{{OTelParameters.Url}}}.")]
    public static partial void InvalidReturnUrl(this ILogger logger, LogLevel logLevel, string url);

    [LoggerMessage(
        $"Invalid sid {{{OTelParameters.Sid}}}.")]
    public static partial void InvalidSid(this ILogger logger, LogLevel logLevel, string sid);


    [LoggerMessage(
        $"Failed To clear IndexHtmlCache for BFF Frontend {{{OTelParameters.Frontend}}}")]
    public static partial void FailedToClearIndexHtmlCacheForFrontend(this ILogger logger, LogLevel logLevel, Exception ex, BffFrontendName frontend);

    [LoggerMessage(
        $"No OpenID Configuration found for scheme {{{OTelParameters.Scheme}}}")]
    public static partial void NoOpenIdConfigurationFoundForScheme(this ILogger logger, LogLevel logLevel, Scheme scheme);

    [LoggerMessage(
        $"No frontend selected.")]
    public static partial void NoFrontendSelected(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        $"Selected frontend '{{{OTelParameters.Frontend}}}'")]
    public static partial void SelectedFrontend(this ILogger logger, LogLevel logLevel, BffFrontendName frontend);

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
        message: $"Access token is missing. token type: '{{{OTelParameters.TokenType}}}', local path: '{{{OTelParameters.LocalPath}}}', detail: '{{{OTelParameters.Detail}}}'")]
    public static partial void AccessTokenMissing(this ILogger logger, LogLevel logLevel, string tokenType, string localPath, string detail);

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

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"BFF management endpoint {{endpoint}} is only intended for a browser window to request and load. It is not intended to be accessed with Ajax or fetch requests.")]
    public static partial void ManagementEndpointAccessedViaAjax(this ILogger logger, string endpoint);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Challenge was called for a BFF API endpoint, BFF response handling changing status code to 401.")]
    public static partial void ChallengeForBffApiEndpoint(this ILogger logger);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Forbid was called for a BFF API endpoint, BFF response handling changing status code to 403.")]
    public static partial void ForbidForBffApiEndpoint(this ILogger logger);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Creating user session record in store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void CreatingUserSession(this ILogger logger, string sub, string? sid);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Detected a duplicate insert of the same session. This can happen when multiple browser tabs are open and can safely be ignored.")]
    public static partial void DuplicateSessionInsertDetected(this ILogger logger, Exception ex);

    [LoggerMessage(
        level: LogLevel.Warning,
        message: $"Exception creating new server-side session in database: {{{OTelParameters.Error}}}. If this is a duplicate key error, it's safe to ignore. This can happen (for example) when two identical tabs are open.")]
    public static partial void ExceptionCreatingSession(this ILogger logger, Exception ex, string error);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"No record found in user session store when trying to delete user session for key {{{OTelParameters.Key}}}")]
    public static partial void NoRecordFoundForKey(this ILogger logger, string key);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Deleting user session record in store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void DeletingUserSession(this ILogger logger, string sub, string? sid);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"DbUpdateConcurrencyException: {{{OTelParameters.Error}}}")]
    public static partial void DbUpdateConcurrencyException(this ILogger logger, string error);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Getting user session record from store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void GettingUserSession(this ILogger logger, string sub, string? sid);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Getting {{{OTelParameters.Count}}} user session(s) from store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void GettingUserSessions(this ILogger logger, int count, string? sub, string? sid);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Deleting {{{OTelParameters.Count}}} user session(s) from store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void DeletingUserSessions(this ILogger logger, int count, string? sub, string? sid);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Updating user session record in store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void UpdatingUserSession(this ILogger logger, string? sub, string? sid);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Removing {{{OTelParameters.Count}}} server side sessions")]
    public static partial void RemovingServerSideSessions(this ILogger logger, int count);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Retrieving token for user {{{OTelParameters.User}}}")]
    public static partial void RetrievingTokenForUser(this ILogger logger, string? user);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Retrieving session {{{OTelParameters.Sid}}} for sub {{{OTelParameters.Sub}}}")]
    public static partial void RetrievingSession(this ILogger logger, string sid, string sub);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Storing token for user {{{OTelParameters.User}}}")]
    public static partial void StoringTokenForUser(this ILogger logger, string? user);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Removing token for user {{{OTelParameters.User}}}")]
    public static partial void RemovingTokenForUser(this ILogger logger, string? user);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Failed to find a session to update, bailing out")]
    public static partial void FailedToFindSessionToUpdate(this ILogger logger);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Creating entry in store for AuthenticationTicket, key {{{OTelParameters.Key}}}, with expiration: {{{OTelParameters.Expiration}}}")]
    public static partial void CreatingAuthenticationTicketEntry(this ILogger logger, string key, DateTime? expiration);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Retrieve AuthenticationTicket for key {{{OTelParameters.Key}}}")]
    public static partial void RetrieveAuthenticationTicket(this ILogger logger, string key);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"No AuthenticationTicket found in store for {{{OTelParameters.Key}}}")]
    public static partial void NoAuthenticationTicketFoundForKey(this ILogger logger, string key);

    [LoggerMessage(
        level: LogLevel.Warning,
        message: $"Failed to deserialize authentication ticket from store, deleting record for key {{{OTelParameters.Key}}}")]
    public static partial void FailedToDeserializeAuthenticationTicket(this ILogger logger, string key);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Renewing AuthenticationTicket for key {{{OTelParameters.Key}}}, with expiration: {{{OTelParameters.Expiration}}}")]
    public static partial void RenewingAuthenticationTicket(this ILogger logger, string key, DateTime? expiration);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Removing AuthenticationTicket from store for key {{{OTelParameters.Key}}}")]
    public static partial void RemovingAuthenticationTicket(this ILogger logger, string key);

    [LoggerMessage(
        level: LogLevel.Debug,
        message: $"Getting AuthenticationTickets from store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void GettingAuthenticationTickets(this ILogger logger, string? sub, string? sid);
    [LoggerMessage(
        message: $"Frontend selected via path mapping '{{{OTelParameters.PathMapping}}}', but request path '{{{OTelParameters.LocalPath}}}' has different case. Cookie path names are case sensitive, so the cookie likely doesn't work.")]
    public static partial void FrontendSelectedWithPathCasingIssue(this ILogger logger, LogLevel level, string pathMapping, LocalPath localPath);

    public static string Sanitize(this string toSanitize) => toSanitize.ReplaceLineEndings(string.Empty);

    public static string Sanitize(this PathString toSanitize) => toSanitize.ToString().ReplaceLineEndings(string.Empty);
}
