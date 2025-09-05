// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.SessionManagement.SessionStore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Otel;

internal static partial class LogMessages
{
    [LoggerMessage(
        Message = $"Retrieved IndexHTML for {{{OTelParameters.Frontend}}}. Statuscode was {{{OTelParameters.StatusCode}}}")]
    public static partial void RetrievedIndexHTML(this ILogger logger, LogLevel level, BffFrontendName frontend, HttpStatusCode statusCode);


    [LoggerMessage(
        Message = $"Failed to retrieve IndexHTML for {{{OTelParameters.Frontend}}}. Statuscode was {{{OTelParameters.StatusCode}}}. This result will not be cached.")]
    public static partial void IndexHtmlRetrievalFailed(this ILogger logger, LogLevel level, BffFrontendName frontend, HttpStatusCode statusCode);

    [LoggerMessage(
        Message = $"Duplicate 'default' frontend configured. This is a frontend without an origin and path. The first one is used. ")]
    public static partial void DuplicateDefaultRouteConfigured(this ILogger logger, LogLevel level);

    [LoggerMessage(
        Message = $"Failed to apply yarp access token request transform.")]
    public static partial void FailedToApplyYarpAccessTokenRequestTransform(this ILogger logger, Exception ex, LogLevel level);

    [LoggerMessage(
        Message = $"AllowedSilentLoginReferrers have been configured but no referer header was found on silent login callback. Returning bad request. ")]
    public static partial void SilentLoginEndpointRefererHeaderMissing(this ILogger logger, LogLevel level);

    [LoggerMessage(
        Message = $"Referer {OTelParameters.Referer} not in allowed referers: {{AllowedReferers}}")]
    public static partial void SilentLoginEndpointRefererNotAllowed(this ILogger logger, LogLevel level, string referer, string allowedReferers);


    [LoggerMessage(
        Message = $"Proxy response error. local path: '{{{OTelParameters.LocalPath}}}', error: '{{{OTelParameters.Error}}}'")]
    public static partial void ProxyResponseError(this ILogger logger, LogLevel level, string localPath, string error);

    [LoggerMessage(
        message:
        $"Deserializing AuthenticationTicket envelope failed or found incorrect version for key {{{OTelParameters.Key}}}")]
    public static partial void AuthenticationTicketEnvelopeVersionInvalid(this ILogger logger, LogLevel logLevel,
        UserSessionKey key);

    [LoggerMessage(
        message:
        $"Failed to unprotect AuthenticationTicket payload for key {{{OTelParameters.Key}}}")]
    public static partial void AuthenticationTicketPayloadInvalid(this ILogger logger, Exception? ex, LogLevel logLevel,
        UserSessionKey key);

    [LoggerMessage(
        message:
        $"Failed to deserialize AuthenticationTicket payload for key {{{OTelParameters.Key}}}")]
    public static partial void AuthenticationTicketFailedToDeserialize(this ILogger logger, Exception? ex,
        LogLevel logLevel,
        UserSessionKey key);

    [LoggerMessage(
        Message = "FrontendSelection: No frontends registered in the store.")]
    public static partial void NoFrontendsRegistered(this ILogger logger, LogLevel logLevel);

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
    public static partial void FailedToClearIndexHtmlCacheForFrontend(this ILogger logger, LogLevel logLevel,
        Exception ex, BffFrontendName frontend);

    [LoggerMessage(
        $"No OpenID Configuration found for scheme {{{OTelParameters.Scheme}}}")]
    public static partial void NoOpenIdConfigurationFoundForScheme(this ILogger logger, LogLevel logLevel,
        Scheme scheme);

    [LoggerMessage(
        $"None of the configured frontends matched the selection criteria.")]
    public static partial void NoFrontendSelected(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        $"BFF is not configured to work with multiple frontends.")]
    public static partial void MultiFrontendDisabled(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        $"Selected frontend '{{{OTelParameters.Frontend}}}'")]
    public static partial void SelectedFrontend(this ILogger logger, LogLevel logLevel, BffFrontendName frontend);

    [LoggerMessage(
        LogLevel.Error,
        $"Anti-forgery validation failed. local path: '{{{OTelParameters.LocalPath}}}'")]
    public static partial void AntiForgeryValidationFailed(this ILogger logger, string localPath);

    [LoggerMessage(
        message: $"Back-channel logout. sub: '{{{OTelParameters.Sub}}}', sid: '{{{OTelParameters.Sid}}}'")]
    public static partial void BackChannelLogout(this ILogger logger, LogLevel logLevel, string sub, string sid);


    [LoggerMessage(
        message:
        $"Access token is missing. token type: '{{{OTelParameters.TokenType}}}', local path: '{{{OTelParameters.LocalPath}}}', detail: '{{{OTelParameters.Detail}}}'")]
    public static partial void AccessTokenMissing(this ILogger logger, LogLevel logLevel, string tokenType,
        string localPath, string detail);

    [LoggerMessage(
        message:
        $"Invalid route configuration. Cannot combine a required access token (a call to WithAccessToken) and an optional access token (a call to WithOptionalUserAccessToken). clusterId: '{{{OTelParameters.ClusterId}}}', routeId: '{{{OTelParameters.RouteId}}}'")]
    public static partial void InvalidRouteConfiguration(this ILogger logger, LogLevel logLevel, string? clusterId, string routeId);

    [LoggerMessage(
        message:
        $"Failed to request new User Access Token due to: {{{OTelParameters.Error}}}. This can mean that the refresh token is expired or revoked but the cookie session is still active. If the session was not revoked, ensure that the session cookie lifetime is smaller than the refresh token lifetime.")]
    public static partial void FailedToRequestNewUserAccessToken(this ILogger logger, LogLevel logLevel, string error);

    [LoggerMessage(
        message:
        $"Failed to request new User Access Token due to: {{{OTelParameters.Error}}}. This likely means that the user's refresh token is expired or revoked. The user's session will be ended, which will force the user to log in.")]
    public static partial void UserSessionRevoked(this ILogger logger, LogLevel logLevel, string error);

    [LoggerMessage(
        message:
        $"BFF management endpoint {{endpoint}} is only intended for a browser window to request and load. It is not intended to be accessed with Ajax or fetch requests.")]
    public static partial void ManagementEndpointAccessedViaAjax(this ILogger logger, LogLevel logLevel, string endpoint);

    [LoggerMessage(
        message: $"Challenge was called for a BFF API endpoint, BFF response handling changing status code to 401.")]
    public static partial void ChallengeForBffApiEndpoint(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: $"Forbid was called for a BFF API endpoint, BFF response handling changing status code to 403.")]
    public static partial void ForbidForBffApiEndpoint(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message:
        $"Creating user session record in store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void CreatingUserSession(this ILogger logger, LogLevel logLevel, string sub, string? sid);

    [LoggerMessage(
        message:
        $"Detected a duplicate insert of the same session. This can happen when multiple browser tabs are open and can safely be ignored.")]
    public static partial void DuplicateSessionInsertDetected(this ILogger logger, LogLevel logLevel, Exception? ex = null);

    [LoggerMessage(
        message:
        $"Exception creating new server-side session in database: {{{OTelParameters.Error}}}. If this is a duplicate key error, it's safe to ignore. This can happen (for example) when two identical tabs are open.")]
    public static partial void ExceptionCreatingSession(this ILogger logger, LogLevel logLevel, Exception ex, string error);

    [LoggerMessage(
        message:
        $"No record found in user session store when trying to delete user session for key {{{OTelParameters.Key}}}")]
    public static partial void NoRecordFoundForKey(this ILogger logger, LogLevel logLevel, UserSessionKey key);

    [LoggerMessage(
        message:
        $"Deleting user session record in store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void DeletingUserSession(this ILogger logger, LogLevel logLevel, string sub, string? sid);

    [LoggerMessage(
        message: $"DbUpdateConcurrencyException: {{{OTelParameters.Error}}}")]
    public static partial void DbUpdateConcurrencyException(this ILogger logger, LogLevel logLevel, string error);

    [LoggerMessage(
        message:
        $"Getting user session record from store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void GettingUserSession(this ILogger logger, LogLevel logLevel, string sub, string? sid);

    [LoggerMessage(
        message:
        $"Getting {{{OTelParameters.Count}}} user session(s) from store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void GettingUserSessions(this ILogger logger, LogLevel logLevel, int count, string? sub, string? sid);

    [LoggerMessage(
        message:
        $"Deleting {{{OTelParameters.Count}}} user session(s) from store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void DeletingUserSessions(this ILogger logger, LogLevel logLevel, int count, string? sub, string? sid);

    [LoggerMessage(
        message:
        $"Updating user session record in store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void UpdatingUserSession(this ILogger logger, LogLevel logLevel, string? sub, string? sid);

    [LoggerMessage(
        message: $"Removing {{{OTelParameters.Count}}} server side sessions")]
    public static partial void RemovingServerSideSessions(this ILogger logger, LogLevel logLevel, int count);

    [LoggerMessage(
        message: $"Retrieving token for user {{{OTelParameters.User}}}")]
    public static partial void RetrievingTokenForUser(this ILogger logger, LogLevel logLevel, string? user);

    [LoggerMessage(
        message: $"Retrieving session {{{OTelParameters.Sid}}} for sub {{{OTelParameters.Sub}}}")]
    public static partial void RetrievingSession(this ILogger logger, LogLevel logLevel, string sid, string sub);

    [LoggerMessage(
        message: $"Storing token for user {{{OTelParameters.User}}}")]
    public static partial void StoringTokenForUser(this ILogger logger, LogLevel logLevel, string? user);

    [LoggerMessage(
        message: $"Removing token for user {{{OTelParameters.User}}}")]
    public static partial void RemovingTokenForUser(this ILogger logger, LogLevel logLevel, string? user);

    [LoggerMessage(
        message: $"Failed to find a session to update, bailing out")]
    public static partial void FailedToFindSessionToUpdate(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message:
        $"Creating entry in store for AuthenticationTicket, key {{{OTelParameters.Key}}}, with expiration: {{{OTelParameters.Expiration}}}")]
    public static partial void CreatingAuthenticationTicketEntry(this ILogger logger, LogLevel logLevel, string key, DateTime? expiration);

    [LoggerMessage(
        message: $"Retrieve AuthenticationTicket for key {{{OTelParameters.Key}}}")]
    public static partial void RetrieveAuthenticationTicket(this ILogger logger, LogLevel logLevel, string key);

    [LoggerMessage(
        message: $"Ticket loaded for key: {{{OTelParameters.Key}}}, with expiration: {{{OTelParameters.Expiration}}}")]
    public static partial void TicketLoaded(this ILogger logger, LogLevel logLevel, string key, DateTime? expiration);

    [LoggerMessage(
        message: $"No AuthenticationTicket found in store for {{{OTelParameters.Key}}}")]
    public static partial void NoAuthenticationTicketFoundForKey(this ILogger logger, LogLevel logLevel, string key);

    [LoggerMessage(
        message:
        $"Failed to deserialize authentication ticket from store, deleting record for key {{{OTelParameters.Key}}}")]
    public static partial void FailedToDeserializeAuthenticationTicket(this ILogger logger, LogLevel logLevel, string key);

    [LoggerMessage(
        message:
        $"Renewing AuthenticationTicket for key {{{OTelParameters.Key}}}, with expiration: {{{OTelParameters.Expiration}}}")]
    public static partial void RenewingAuthenticationTicket(this ILogger logger, LogLevel logLevel, string key, DateTime? expiration);

    [LoggerMessage(
        message: $"Removing AuthenticationTicket from store for key {{{OTelParameters.Key}}}")]
    public static partial void RemovingAuthenticationTicket(this ILogger logger, LogLevel logLevel, string key);

    [LoggerMessage(
        message:
        $"Getting AuthenticationTickets from store for sub {{{OTelParameters.Sub}}} sid {{{OTelParameters.Sid}}}")]
    public static partial void GettingAuthenticationTickets(this ILogger logger, LogLevel logLevel, string? sub, string? sid);

    [LoggerMessage(
        message:
        $"Frontend selected via path mapping '{{{OTelParameters.PathMapping}}}', but request path '{{{OTelParameters.LocalPath}}}' has different case. Cookie path names are case sensitive, so the cookie likely doesn't work.")]
    public static partial void FrontendSelectedWithPathCasingIssue(this ILogger logger, LogLevel logLevel,
        string pathMapping, LocalPath localPath);

    [LoggerMessage(
        message:
        $"Management endpoints are automatically mapped, so the call to MapBffManagementEndpoints will be ignored. If you're using BffOptions.AutomaticallyRegisterBffMiddleware, you don't need to call endpoints.MapBffManagementEndpoints()")]
    public static partial void AlreadyMappedManagementEndpoints(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Authenticating scheme: {Scheme}")]
    public static partial void AuthenticatingScheme(this ILogger logger, LogLevel logLevel, string? scheme);
    [LoggerMessage(
        message: "Signing out of scheme: {Scheme}")]
    public static partial void SigningOutOfScheme(this ILogger logger, LogLevel logLevel, string? scheme);

    [LoggerMessage(
        message: "Setting OIDC ProtocolMessage.Prompt to 'none' for BFF silent login")]
    public static partial void SettingOidcPromptNoneForSilentLogin(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Setting OIDC ProtocolMessage.Prompt to {Prompt} for BFF silent login")]
    public static partial void SettingOidcPromptForSilentLogin(this ILogger logger, LogLevel logLevel, string prompt);

    [LoggerMessage(
        message: "Handling error response from OIDC provider for BFF silent login.")]
    public static partial void HandlingErrorResponseFromOidcProviderForSilentLogin(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Handling failed response from OIDC provider for BFF silent login.")]
    public static partial void HandlingFailedResponseFromOidcProviderForSilentLogin(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Processing back-channel logout request")]
    public static partial void ProcessingBackChannelLogoutRequest(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "No claims in back-channel JWT")]
    public static partial void NoClaimsInBackChannelJwt(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Claims found in back-channel JWT {Claims}")]
    public static partial void ClaimsFoundInBackChannelJwt(this ILogger logger, LogLevel logLevel, string claims);

    [LoggerMessage(
        message: "Back-channel JWT validation successful")]
    public static partial void BackChannelJwtValidationSuccessful(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Processing login request")]
    public static partial void ProcessingLoginRequest(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Login endpoint triggering Challenge with returnUrl {ReturnUrl}")]
    public static partial void LoginEndpointTriggeringChallenge(this ILogger logger, LogLevel logLevel, string returnUrl);

    [LoggerMessage(
        message: "Processing logout request")]
    public static partial void ProcessingLogoutRequest(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Logout endpoint triggering SignOut with returnUrl {ReturnUrl}")]
    public static partial void LogoutEndpointTriggeringSignOut(this ILogger logger, LogLevel logLevel, string returnUrl);

    [LoggerMessage(
        message: "Processing silent login callback request")]
    public static partial void ProcessingSilentLoginCallbackRequest(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Silent login endpoint rendering HTML with JS postMessage to origin {Origin} with isLoggedIn {IsLoggedIn}")]
    public static partial void SilentLoginEndpointRenderingHtml(this ILogger logger, LogLevel logLevel, string origin, string isLoggedIn);

    [LoggerMessage(
        message: "Processing silent login request")]
    public static partial void ProcessingSilentLoginRequest(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Using deprecated silentlogin endpoint. This endpoint will be removed in future versions. Consider calling the BFF Login endpoint with prompt=none.")]
    public static partial void UsingDeprecatedSilentLoginEndpoint(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Processing user request")]
    public static partial void ProcessingUserRequest(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "User endpoint indicates the user is not logged in, using status code {StatusCode}")]
    public static partial void UserEndpointNotLoggedIn(this ILogger logger, LogLevel logLevel, int statusCode);

    [LoggerMessage(
        message: "User endpoint indicates the user is logged in with claims {Claims}")]
    public static partial void UserEndpointLoggedInWithClaims(this ILogger logger, LogLevel logLevel, string claims);

    [LoggerMessage(
        message: "Nop implementation of session revocation for sub: {Sub}, and sid: {Sid}. Implement ISessionRevocationService to provide your own implementation.")]
    public static partial void NopSessionRevocation(this ILogger logger, LogLevel logLevel, string? sub, string? sid);

    [LoggerMessage(
        message: "Revoking sessions for sub {Sub} and sid {Sid}")]
    public static partial void RevokingSessions(this ILogger logger, LogLevel logLevel, string? sub, string? sid);

    [LoggerMessage(
        message: "Refresh token revoked for sub {Sub} and sid {Sid}")]
    public static partial void RefreshTokenRevoked(this ILogger logger, LogLevel logLevel, string sub, string? sid);

    [LoggerMessage(
        message: "BFF session cleanup is enabled, but no IUserSessionStoreCleanup is registered in DI. BFF session cleanup will not run.")]
    public static partial void SessionCleanupNotRegistered(this ILogger logger, LogLevel logLevel);


    [LoggerMessage(
        message: "Failed to cleanup session")]
    public static partial void FailedToCleanupSession(this ILogger logger, LogLevel logLevel, Exception ex);

    [LoggerMessage(
        message: "Failed to cleanup expired sessions")]
    public static partial void FailedToCleanupExpiredSessions(this ILogger logger, LogLevel logLevel, Exception ex);

    [LoggerMessage(
        message: "Revoking user's refresh tokens in OnSigningOut for subject id: {Sub}")]
    public static partial void RevokingUserRefreshTokensOnSigningOut(this ILogger logger, LogLevel logLevel, string? sub);

    [LoggerMessage(
        message: "Explicitly setting ShouldRenew=false in OnValidatePrincipal due to query param suppressing slide behavior.")]
    public static partial void SuppressingSlideBehaviorOnValidatePrincipal(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Explicitly setting ShouldRenew=false in OnCheckSlidingExpiration due to query param suppressing slide behavior.")]
    public static partial void SuppressingSlideBehaviorOnCheckSlidingExpiration(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Failed to process backchannel logout request. 'Logout token is missing'")]
    public static partial void FailedToProcessBackchannelLogoutRequestMissingToken(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Failed to process backchannel logout request.")]
    public static partial void FailedToProcessBackchannelLogoutRequest(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Logout token missing sub and sid claims.")]
    public static partial void LogoutTokenMissingSubAndSidClaims(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Logout token should not contain nonce claim.")]
    public static partial void LogoutTokenShouldNotContainNonceClaim(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Logout token missing events claim.")]
    public static partial void LogoutTokenMissingEventsClaim(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Logout token contains missing http://schemas.openid.net/event/backchannel-logout value.")]
    public static partial void LogoutTokenMissingBackchannelLogoutValue(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        message: "Logout token contains invalid JSON in events claim value.")]
    public static partial void LogoutTokenContainsInvalidJsonInEventsClaim(this ILogger logger, LogLevel logLevel, Exception ex);

    [LoggerMessage(
        message: "Error validating logout token.")]
    public static partial void ErrorValidatingLogoutToken(this ILogger logger, LogLevel logLevel, Exception ex);

    [LoggerMessage(
        message: $"A changed frontend was detected. Clearing caches for frontend {{{OTelParameters.Frontend}}}")]
    public static partial void ChangedFrontendDetected_ClearingCaches(this ILogger logger, LogLevel logLevel, BffFrontendName frontend);

    [LoggerMessage(
        message: $"Failed to clear hybrid cache for frontend {{{OTelParameters.Frontend}}}")]
    public static partial void FailedToClearSchemeCache(this ILogger logger, LogLevel logLevel, BffFrontendName frontend, Exception ex);

    [LoggerMessage(
        message: $"Error occurred while processing frontend changes")]
    public static partial void ErrorWhileProcessingFrontendChanges(this ILogger logger, LogLevel logLevel, Exception ex);

    [LoggerMessage(
        message: $"Failed to add frontend change to queue")]
    public static partial void FailedToAddFrontendToQueue(this ILogger logger, LogLevel logLevel, BffFrontendName frontend);



    public static string Sanitize(this string toSanitize) => toSanitize.ReplaceLineEndings(string.Empty);

    public static string Sanitize(this PathString toSanitize) => toSanitize.ToString().ReplaceLineEndings(string.Empty);
}
