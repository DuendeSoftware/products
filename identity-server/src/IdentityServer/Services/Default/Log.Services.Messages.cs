// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ResponseFromBackChannelLogoutEndpointUrlStatus),
        Message = "Response from back-channel logout endpoint: {Url} status code: {Status}")]
    internal static partial void ResponseFromBackChannelLogoutEndpointUrlStatus(this ILogger logger, object url, object status);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(ResponseFromBackChannelLogoutEndpointUrlStatus2),
        Message = "Response from back-channel logout endpoint: {Url} status code: {Status}")]
    internal static partial void ResponseFromBackChannelLogoutEndpointUrlStatus2(this ILogger logger, object url, object status);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(ResponseFromBackChannelLogoutEndpointUrlStatus3),
        Message = "Response from back-channel logout endpoint: {Url} status code: {Status}, error: {Error}, error_description: {ErrorDescription}")]
    internal static partial void ResponseFromBackChannelLogoutEndpointUrlStatus3(this ILogger logger, object url, object status, object error, object errorDescription);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ExceptionInvokingBackChannelLogoutForUrlUrl),
        Message = "Exception invoking back-channel logout for url: {Url}")]
    internal static partial void ExceptionInvokingBackChannelLogoutForUrlUrl(this ILogger logger, System.Exception exception, object url);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(GettingPendingLoginRequestsForUser),
        Message = "Getting pending login requests for user")]
    internal static partial void GettingPendingLoginRequestsForUser(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(SuccessfulUpdateForBackchannelAuthenticationRequestIdId),
        Message = "Successful update for backchannel authentication request id {Id}")]
    internal static partial void SuccessfulUpdateForBackchannelAuthenticationRequestIdId(this ILogger logger, object id);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(GettingClaimsForIdentityTokenForSubjectSubject),
        Message = "Getting claims for identity token for subject: {Subject} and client: {ClientId}")]
    internal static partial void GettingClaimsForIdentityTokenForSubjectSubject(this ILogger logger, object subject, object clientId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(InAdditionToAnIdTokenAnAccess),
        Message = "In addition to an id_token, an access_token was requested. No claims other than sub are included in the id_token. To obtain more user claims, either use the user info endpoint or set AlwaysIncludeUserClaimsInIdToken on the client configuration.")]
    internal static partial void InAdditionToAnIdTokenAnAccess(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(GettingClaimsForAccessTokenForClientClientId),
        Message = "Getting claims for access token for client: {ClientId}")]
    internal static partial void GettingClaimsForAccessTokenForClientClientId(this ILogger logger, object clientId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientClientIdIsImpersonatingImpersonatedClientId),
        Message = "Client {ClientId} is impersonating {ImpersonatedClientId}")]
    internal static partial void ClientClientIdIsImpersonatingImpersonatedClientId(this ILogger logger, object clientId, object impersonatedClientId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(GettingClaimsForAccessTokenForSubjectSubject),
        Message = "Getting claims for access token for subject: {Subject}")]
    internal static partial void GettingClaimsForAccessTokenForSubjectSubject(this ILogger logger, object subject);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClaimTypesFromProfileServiceThatWereFiltered),
        Message = "Claim types from profile service that were filtered: {ClaimTypes}")]
    internal static partial void ClaimTypesFromProfileServiceThatWereFiltered(this ILogger logger, IEnumerable<string> claimTypes);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientIsConfiguredToNotRequireConsentNo),
        Message = "Client is configured to not require consent, no consent is required")]
    internal static partial void ClientIsConfiguredToNotRequireConsentNo(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoScopesBeingRequestedNoConsentIsRequired),
        Message = "No scopes being requested, no consent is required")]
    internal static partial void NoScopesBeingRequestedNoConsentIsRequired(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientIsConfiguredToNotAllowRememberingConsent),
        Message = "Client is configured to not allow remembering consent, consent is required")]
    internal static partial void ClientIsConfiguredToNotAllowRememberingConsent(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ScopesContainsParameterizedValuesConsentIsRequired),
        Message = "Scopes contains parameterized values, consent is required")]
    internal static partial void ScopesContainsParameterizedValuesConsentIsRequired(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ScopesContainsOfflineAccessConsentIsRequired),
        Message = "Scopes contains offline_access, consent is required")]
    internal static partial void ScopesContainsOfflineAccessConsentIsRequired(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(FoundNoPriorConsentFromConsentStoreConsent),
        Message = "Found no prior consent from consent store, consent is required")]
    internal static partial void FoundNoPriorConsentFromConsentStoreConsent(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ConsentFoundInConsentStoreIsExpiredConsent),
        Message = "Consent found in consent store is expired, consent is required")]
    internal static partial void ConsentFoundInConsentStoreIsExpiredConsent(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ConsentFoundInConsentStoreIsDifferentThan),
        Message = "Consent found in consent store is different than current request, consent is required")]
    internal static partial void ConsentFoundInConsentStoreIsDifferentThan(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ConsentFoundInConsentStoreIsSameAs),
        Message = "Consent found in consent store is same as current request, consent is not required")]
    internal static partial void ConsentFoundInConsentStoreIsSameAs(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ConsentFoundInConsentStoreHasNoScopes),
        Message = "Consent found in consent store has no scopes, consent is required")]
    internal static partial void ConsentFoundInConsentStoreHasNoScopes(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientAllowsRememberingConsentAndConsentGivenUpdating),
        Message = "Client allows remembering consent, and consent given. Updating consent store for subject: {Subject}")]
    internal static partial void ClientAllowsRememberingConsentAndConsentGivenUpdating(this ILogger logger, object subject);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientAllowsRememberingConsentAndNoScopesProvided),
        Message = "Client allows remembering consent, and no scopes provided. Removing consent from consent store for subject: {Subject}")]
    internal static partial void ClientAllowsRememberingConsentAndNoScopesProvided(this ILogger logger, object subject);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AllowAllTrueSoOriginValueIsAllowed),
        Message = "AllowAll true, so origin: {Value} is allowed")]
    internal static partial void AllowAllTrueSoOriginValueIsAllowed(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AllowedOriginsConfiguredAndOriginValueIsAllowed),
        Message = "AllowedOrigins configured and origin {Value} is allowed")]
    internal static partial void AllowedOriginsConfiguredAndOriginValueIsAllowed(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AllowedOriginsConfiguredAndOriginValueIsNotAllowed),
        Message = "AllowedOrigins configured and origin {Value} is not allowed")]
    internal static partial void AllowedOriginsConfiguredAndOriginValueIsNotAllowed(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ExitingOriginValueIsNotAllowed),
        Message = "Exiting; origin {Value} is not allowed")]
    internal static partial void ExitingOriginValueIsNotAllowed(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(DeviceAuthorizationFailureDescription),
        Message = "{Message}")]
    internal static partial void DeviceAuthorizationFailureDescription(this ILogger logger, object message);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(Event),
        Message = "{@Event}")]
    internal static partial void Event(this ILogger logger, object @event);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(AuthenticationContextBeingReturned),
        Message = "Authentication context being returned")]
    internal static partial void AuthenticationContextBeingReturned(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoAuthenticationContextFound),
        Message = "No authentication context found")]
    internal static partial void NoAuthenticationContextFound(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoAuthorizationRequestBeingReturned),
        Message = "No AuthorizationRequest being returned")]
    internal static partial void NoAuthorizationRequestBeingReturned(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(AuthorizationRequestBeingReturned),
        Message = "AuthorizationRequest being returned")]
    internal static partial void AuthorizationRequestBeingReturned(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoAuthorizationRequestBeingReturned2),
        Message = "No AuthorizationRequest being returned")]
    internal static partial void NoAuthorizationRequestBeingReturned2(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(ErrorContextLoaded),
        Message = "Error context loaded")]
    internal static partial void ErrorContextLoaded(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoErrorContextFound),
        Message = "No error context found")]
    internal static partial void NoErrorContextFound(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoErrorContextFound2),
        Message = "No error context found")]
    internal static partial void NoErrorContextFound2(this ILogger logger);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(SAMLSigninStateNotFoundOrExpiredFor),
        Message = "SAML signin state not found or expired for StateId {StateId}. " +
                    "The denial cannot be recorded — the callback will redirect to login")]
    internal static partial void SAMLSigninStateNotFoundOrExpiredFor(this ILogger logger, object stateId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RecordedSAMLAuthenticationDenialErrorForStateIdStateId),
        Message = "Recorded SAML authentication denial ({Error}) for StateId {StateId}")]
    internal static partial void RecordedSAMLAuthenticationDenialErrorForStateIdStateId(this ILogger logger, object error, object stateId);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(IsValidReturnUrlTrue),
        Message = "IsValidReturnUrl true")]
    internal static partial void IsValidReturnUrlTrue(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(IsValidReturnUrlFalse),
        Message = "IsValidReturnUrl false")]
    internal static partial void IsValidReturnUrlFalse(this ILogger logger);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(InvalidContentTypeTypeFromJwtUrlUrl),
        Message = "Invalid content type {Type} from jwt url {Url}")]
    internal static partial void InvalidContentTypeTypeFromJwtUrlUrl(this ILogger logger, object type, object url);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(SuccessHttpResponseFromJwtUrlUrl),
        Message = "Success http response from jwt url {Url}")]
    internal static partial void SuccessHttpResponseFromJwtUrlUrl(this ILogger logger, object url);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(InvalidHttpStatusCodeStatusFromJwtUrl),
        Message = "Invalid http status code {Status} from jwt url {Url}")]
    internal static partial void InvalidHttpStatusCodeStatusFromJwtUrl(this ILogger logger, System.Net.HttpStatusCode status, object url);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(OneOrMoreErrorsOccuredDuringDeserializationOf),
        Message = "One or more errors occured during deserialization of persisted grants, returning successfull items.")]
    internal static partial void OneOrMoreErrorsOccuredDuringDeserializationOf(this ILogger logger, System.Exception exception);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(FailedProcessingResultsFromGrantStore),
        Message = "Failed processing results from grant store.")]
    internal static partial void FailedProcessingResultsFromGrantStore(this ILogger logger, System.Exception exception);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(IsActiveCalledFromCaller),
        Message = "IsActive called from: {Caller}")]
    internal static partial void IsActiveCalledFromCaller(this ILogger logger, object caller);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(StartRefreshTokenValidation),
        Message = "Start refresh token validation")]
    internal static partial void StartRefreshTokenValidation(this ILogger logger);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(InvalidRefreshToken),
        Message = "Invalid refresh token")]
    internal static partial void InvalidRefreshToken(this ILogger logger);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(RefreshTokenHasExpired),
        Message = "Refresh token has expired.")]
    internal static partial void RefreshTokenHasExpired(this ILogger logger);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ClientIdTriesToRefreshTokenBelongingToRefreshTokenClientId),
        Message = "{ClientId} tries to refresh token belonging to {RefreshTokenClientId}")]
    internal static partial void ClientIdTriesToRefreshTokenBelongingToRefreshTokenClientId(this ILogger logger, object clientId, object refreshTokenClientId);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ClientIdDoesNotHaveAccessToOfflineAccess),
        Message = "{ClientId} does not have access to offline_access scope anymore")]
    internal static partial void ClientIdDoesNotHaveAccessToOfflineAccess(this ILogger logger, object clientId);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(RejectingRefreshTokenBecauseItHasBeenConsumed),
        Message = "Rejecting refresh token because it has been consumed already.")]
    internal static partial void RejectingRefreshTokenBecauseItHasBeenConsumed(this ILogger logger);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(SubjectIdHasBeenDisabled),
        Message = "{SubjectId} has been disabled")]
    internal static partial void SubjectIdHasBeenDisabled(this ILogger logger, object subjectId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(CreatingRefreshToken),
        Message = "Creating refresh token")]
    internal static partial void CreatingRefreshToken(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(SettingAnAbsoluteLifetimeAbsoluteLifetime),
        Message = "Setting an absolute lifetime: {AbsoluteLifetime}")]
    internal static partial void SettingAnAbsoluteLifetimeAbsoluteLifetime(this ILogger logger, object absoluteLifetime);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(ClientClientIdSConfiguredNameofRequestClientSlidingRefreshTokenLifetime),
        Message = "Client {ClientId}'s configured SlidingRefreshTokenLifetime" +
                    " of {SlidingLifetime} exceeds its AbsoluteRefreshTokenLifetime" +
                    " of {AbsoluteLifetime}. The refresh_token's sliding lifetime will be capped to the absolute lifetime")]
    internal static partial void ClientClientIdSConfiguredNameofRequestClientSlidingRefreshTokenLifetime(this ILogger logger, object clientId, object slidingLifetime, object absoluteLifetime);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(SettingASlidingLifetimeSlidingLifetime),
        Message = "Setting a sliding lifetime: {SlidingLifetime}")]
    internal static partial void SettingASlidingLifetimeSlidingLifetime(this ILogger logger, object slidingLifetime);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(UpdatingRefreshToken),
        Message = "Updating refresh token")]
    internal static partial void UpdatingRefreshToken(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(TokenUsageIsOneTimeOnlyAndRefresh),
        Message = "Token usage is one-time only and refresh behavior is delete. Deleting current handle, and generating new handle")]
    internal static partial void TokenUsageIsOneTimeOnlyAndRefresh(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(TokenUsageIsOneTimeOnlyAndRefresh2),
        Message = "Token usage is one-time only and refresh behavior is mark as consumed. Setting current handle as consumed, and generating new handle")]
    internal static partial void TokenUsageIsOneTimeOnlyAndRefresh2(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RefreshTokenExpirationIsSlidingExtendingLifetime),
        Message = "Refresh token expiration is sliding - extending lifetime")]
    internal static partial void RefreshTokenExpirationIsSlidingExtendingLifetime(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(CurrentLifetimeCurrentLifetime),
        Message = "Current lifetime: {CurrentLifetime}")]
    internal static partial void CurrentLifetimeCurrentLifetime(this ILogger logger, int currentLifetime);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NewLifetimeSlidingLifetime),
        Message = "New lifetime: {SlidingLifetime}")]
    internal static partial void NewLifetimeSlidingLifetime(this ILogger logger, int slidingLifetime);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NewLifetimeExceedsAbsoluteLifetimeCappingItTo),
        Message = "New lifetime exceeds absolute lifetime, capping it to {NewLifetime}")]
    internal static partial void NewLifetimeExceedsAbsoluteLifetimeCappingItTo(this ILogger logger, int newLifetime);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(CreatedRefreshTokenInStore),
        Message = "Created refresh token in store")]
    internal static partial void CreatedRefreshTokenInStore(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(UpdatedRefreshTokenInStore),
        Message = "Updated refresh token in store")]
    internal static partial void UpdatedRefreshTokenInStore(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoUpdatesToRefreshTokenDone),
        Message = "No updates to refresh token done")]
    internal static partial void NoUpdatesToRefreshTokenDone(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(DueToUserLogoutRemovingTokensForSubject),
        Message = "Due to user logout, removing tokens for subject id {SubjectId} and session id {SessionId}")]
    internal static partial void DueToUserLogoutRemovingTokensForSubject(this ILogger logger, object subjectId, object sessionId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(DueToUserLogoutInvokingBackchannelLogoutFor),
        Message = "Due to user logout, invoking backchannel logout for subject id {SubjectId} and session id {SessionId}")]
    internal static partial void DueToUserLogoutInvokingBackchannelLogoutFor(this ILogger logger, object subjectId, object sessionId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(DueToExpiredSessionRemovingTokensForSubject),
        Message = "Due to expired session, removing tokens for subject id {SubjectId} and session id {SessionId}")]
    internal static partial void DueToExpiredSessionRemovingTokensForSubject(this ILogger logger, object subjectId, object sessionId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(DueToExpiredSessionInvokingBackchannelLogoutFor),
        Message = "Due to expired session, invoking backchannel logout for subject id {SubjectId} and session id {SessionId}")]
    internal static partial void DueToExpiredSessionInvokingBackchannelLogoutFor(this ILogger logger, object subjectId, object sessionId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(DueToMissingExpiredServerSideSessionFailing),
        Message = "Due to missing/expired server-side session, failing token validation for subject id {SubjectId} and session id {SessionId}")]
    internal static partial void DueToMissingExpiredServerSideSessionFailing(this ILogger logger, object subjectId, object sessionId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(DueToClientTokenUseExtendingServerSide),
        Message = "Due to client token use, extending server-side session for subject id {SubjectId} and session id {SessionId}")]
    internal static partial void DueToClientTokenUseExtendingServerSide(this ILogger logger, object subjectId, object sessionId);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CreatingIdentityToken),
        Message = "Creating identity token")]
    internal static partial void CreatingIdentityToken(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CreatingAccessToken),
        Message = "Creating access token")]
    internal static partial void CreatingAccessToken(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CreatingJWTAccessToken),
        Message = "Creating JWT access token")]
    internal static partial void CreatingJWTAccessToken(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CreatingReferenceAccessToken),
        Message = "Creating reference access token")]
    internal static partial void CreatingReferenceAccessToken(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CreatingJWTIdentityToken),
        Message = "Creating JWT identity token")]
    internal static partial void CreatingJWTIdentityToken(this ILogger logger);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ErrorDecodingClientList),
        Message = "Error decoding client list")]
    internal static partial void ErrorDecodingClientList(this ILogger logger, System.Exception exception);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ErrorGettingSAMLSessionList),
        Message = "Error getting SAML session list")]
    internal static partial void ErrorGettingSAMLSessionList(this ILogger logger, System.Exception exception);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientFrontChannelLogoutURLsValue),
        Message = "Client front-channel logout URLs: {Value}")]
    internal static partial void ClientFrontChannelLogoutURLsValue(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoClientFrontChannelLogoutURLs),
        Message = "No client front-channel logout URLs")]
    internal static partial void NoClientFrontChannelLogoutURLs(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientBackChannelLogoutURLsValue),
        Message = "Client back-channel logout URLs: {Value}")]
    internal static partial void ClientBackChannelLogoutURLsValue(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoClientBackChannelLogoutURLs),
        Message = "No client back-channel logout URLs")]
    internal static partial void NoClientBackChannelLogoutURLs(this ILogger logger);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(IBackchannelAuthenticationUserNotificationServiceNotImplementedButForTestingVisitUrl),
        Message = "IBackchannelAuthenticationUserNotificationService not implemented. But for testing, visit {Url} to simulate what a user might need to do to complete the request.")]
    internal static partial void IBackchannelAuthenticationUserNotificationServiceNotImplementedButForTestingVisitUrl(this ILogger logger, object url);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(AuthorizationRequestBeingReturned2),
        Message = "AuthorizationRequest being returned")]
    internal static partial void AuthorizationRequestBeingReturned2(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoAuthorizationRequestBeingReturned3),
        Message = "No AuthorizationRequest being returned")]
    internal static partial void NoAuthorizationRequestBeingReturned3(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(ReturnUrlIsValid),
        Message = "returnUrl is valid")]
    internal static partial void ReturnUrlIsValid(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(ReturnUrlIsNotValid),
        Message = "returnUrl is not valid")]
    internal static partial void ReturnUrlIsNotValid(this ILogger logger);
}
