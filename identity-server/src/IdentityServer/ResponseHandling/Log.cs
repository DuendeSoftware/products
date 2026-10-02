// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.ResponseHandling;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(ProcessInteractionAsync),
        Level = LogLevel.Trace,
        Message = "ProcessInteractionAsync")]
    internal static partial void ProcessInteractionAsync(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ErrorUserConsentResult),
        Level = LogLevel.Information,
        Message = "{ErrorPrefix}: User consent result: {Error}")]
    internal static partial void ErrorUserConsentResult(this ILogger logger, string errorPrefix, object? error);

    [LoggerMessage(
        EventName = nameof(ChangingResponseToLoginRequiredPromptNoneWasRequested),
        Level = LogLevel.Information,
        Message = "Changing response to LoginRequired: prompt=none was requested")]
    internal static partial void ChangingResponseToLoginRequiredPromptNoneWasRequested(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ShowingCreateAccountRequestContainsPromptCreate),
        Level = LogLevel.Information,
        Message = "Showing create account: request contains prompt=create")]
    internal static partial void ShowingCreateAccountRequestContainsPromptCreate(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ShowingLoginRequestContainsPrompt),
        Level = LogLevel.Information,
        Message = "Showing login: request contains prompt={PromptModes}")]
    internal static partial void ShowingLoginRequestContainsPrompt(this ILogger logger, object? promptModes);

    [LoggerMessage(
        EventName = nameof(ShowingLoginRequestContainsMaxAge0),
        Level = LogLevel.Information,
        Message = "Showing login: request contains max_age=0.")]
    internal static partial void ShowingLoginRequestContainsMaxAge0(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ShowingLoginUserIsNotAuthenticated),
        Level = LogLevel.Information,
        Message = "Showing login: User is not authenticated")]
    internal static partial void ShowingLoginUserIsNotAuthenticated(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ShowingLoginUserIsNotActive),
        Level = LogLevel.Information,
        Message = "Showing login: User is not active")]
    internal static partial void ShowingLoginUserIsNotActive(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ShowingLoginCurrentTenantIsNotTheRequested),
        Level = LogLevel.Information,
        Message = "Showing login: Current tenant ({CurrentTenant}) is not the requested tenant ({Tenant})")]
    internal static partial void ShowingLoginCurrentTenantIsNotTheRequested(this ILogger logger, object? currentTenant, object? tenant);

    [LoggerMessage(
        EventName = nameof(ShowingLoginCurrentIdPIsNotTheRequested),
        Level = LogLevel.Information,
        Message = "Showing login: Current IdP ({CurrentIdp}) is not the requested IdP ({Idp})")]
    internal static partial void ShowingLoginCurrentIdPIsNotTheRequested(this ILogger logger, object? currentIdp, object? idp);

    [LoggerMessage(
        EventName = nameof(ShowingLoginRequestedMaxAgeExceeded),
        Level = LogLevel.Information,
        Message = "Showing login: Requested MaxAge exceeded.")]
    internal static partial void ShowingLoginRequestedMaxAgeExceeded(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ShowingLoginUserLoggedInLocallyButClient),
        Level = LogLevel.Information,
        Message = "Showing login: User logged in locally, but client does not allow local logins")]
    internal static partial void ShowingLoginUserLoggedInLocallyButClient(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ShowingLoginUserIsLoggedInWithIdp),
        Level = LogLevel.Information,
        Message = "Showing login: User is logged in with idp: {Idp}, but idp not in client restriction list.")]
    internal static partial void ShowingLoginUserIsLoggedInWithIdp(this ILogger logger, object? idp);

    [LoggerMessage(
        EventName = nameof(ShowingLoginUserSAuthSessionDurationExceeds),
        Level = LogLevel.Information,
        Message = "Showing login: User's auth session duration: {SessionDuration} exceeds client's user SSO lifetime: {UserSsoLifetime}.")]
    internal static partial void ShowingLoginUserSAuthSessionDurationExceeds(this ILogger logger, object? sessionDuration, object? userSsoLifetime);

    [LoggerMessage(
        EventName = nameof(InvalidPromptMode),
        Level = LogLevel.Error,
        Message = "Invalid prompt mode: {PromptMode}")]
    internal static partial void InvalidPromptMode(this ILogger logger, object? promptMode);

    [LoggerMessage(
        EventName = nameof(ErrorPromptNoneRequestedButConsentIsRequired),
        Level = LogLevel.Information,
        Message = "{ErrorPrefix}: prompt=none requested, but consent is required.")]
    internal static partial void ErrorPromptNoneRequestedButConsentIsRequired(this ILogger logger, string errorPrefix);

    [LoggerMessage(
        EventName = nameof(ShowingConsentUserHasNotYetConsented),
        Level = LogLevel.Information,
        Message = "Showing consent: User has not yet consented")]
    internal static partial void ShowingConsentUserHasNotYetConsented(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ConsentWasShownToUser),
        Level = LogLevel.Trace,
        Message = "Consent was shown to user")]
    internal static partial void ConsentWasShownToUser(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ErrorUserConsentResultAuthorizeInteractionResponseGenerator),
        Level = LogLevel.Information,
        Message = "{ErrorPrefix}: User consent result: {Error}")]
    internal static partial void ErrorUserConsentResultAuthorizeInteractionResponseGenerator(this ILogger logger, string errorPrefix, object? error);

    [LoggerMessage(
        EventName = nameof(ErrorUserDeniedConsentToRequiredScopes),
        Level = LogLevel.Information,
        Message = "{ErrorPrefix}: User denied consent to required scopes")]
    internal static partial void ErrorUserDeniedConsentToRequiredScopes(this ILogger logger, string errorPrefix);

    [LoggerMessage(
        EventName = nameof(UserConsentedToScopes),
        Level = LogLevel.Information,
        Message = "User consented to scopes: {Scopes}")]
    internal static partial void UserConsentedToScopes(this ILogger logger, IEnumerable<string>? scopes);

    [LoggerMessage(
        EventName = nameof(UserIndicatedToRememberConsentForScopes),
        Level = LogLevel.Debug,
        Message = "User indicated to remember consent for scopes: {Scopes}")]
    internal static partial void UserIndicatedToRememberConsentForScopes(this ILogger logger, IEnumerable<string>? scopes);

    [LoggerMessage(
        EventName = nameof(UnsupportedGrantType),
        Level = LogLevel.Error,
        Message = "Unsupported grant type: {GrantType}")]
    internal static partial void UnsupportedGrantType(this ILogger logger, object? grantType);

    [LoggerMessage(
        EventName = nameof(CreatingHybridFlowResponse),
        Level = LogLevel.Debug,
        Message = "Creating Hybrid Flow response.")]
    internal static partial void CreatingHybridFlowResponse(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingAuthorizationCodeFlowResponse),
        Level = LogLevel.Debug,
        Message = "Creating Authorization Code Flow response.")]
    internal static partial void CreatingAuthorizationCodeFlowResponse(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingImplicitFlowResponse),
        Level = LogLevel.Debug,
        Message = "Creating Implicit Flow response.")]
    internal static partial void CreatingImplicitFlowResponse(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingResponseForBackchannelAuthenticationRequest),
        Level = LogLevel.Trace,
        Message = "Creating response for backchannel authentication request")]
    internal static partial void CreatingResponseForBackchannelAuthenticationRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingResponseForDeviceAuthorizationRequest),
        Level = LogLevel.Trace,
        Message = "Creating response for device authorization request")]
    internal static partial void CreatingResponseForDeviceAuthorizationRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DiscoveryCustomEntryCannotBeAddedBecauseIt),
        Level = LogLevel.Error,
        Message = "Discovery custom entry {Key} cannot be added, because it already exists.")]
    internal static partial void DiscoveryCustomEntryCannotBeAddedBecauseIt(this ILogger logger, object? key);

    [LoggerMessage(
        EventName = nameof(CreatingIntrospectionResponse),
        Level = LogLevel.Trace,
        Message = "Creating introspection response")]
    internal static partial void CreatingIntrospectionResponse(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingIntrospectionResponseForInactiveToken),
        Level = LogLevel.Debug,
        Message = "Creating introspection response for inactive token.")]
    internal static partial void CreatingIntrospectionResponseForInactiveToken(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingIntrospectionResponseForActiveToken),
        Level = LogLevel.Debug,
        Message = "Creating introspection response for active token.")]
    internal static partial void CreatingIntrospectionResponseForActiveToken(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ExpectedScopeIsMissingInToken),
        Level = LogLevel.Error,
        Message = "Expected scope {Scopes} is missing in token")]
    internal static partial void ExpectedScopeIsMissingInToken(this ILogger logger, IEnumerable<string>? scopes);

    [LoggerMessage(
        EventName = nameof(CreatingResponseForClientCredentialsRequest),
        Level = LogLevel.Trace,
        Message = "Creating response for client credentials request")]
    internal static partial void CreatingResponseForClientCredentialsRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingResponseForPasswordRequest),
        Level = LogLevel.Trace,
        Message = "Creating response for password request")]
    internal static partial void CreatingResponseForPasswordRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingResponseForAuthorizationCodeRequest),
        Level = LogLevel.Trace,
        Message = "Creating response for authorization code request")]
    internal static partial void CreatingResponseForAuthorizationCodeRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingResponseForRefreshTokenRequest),
        Level = LogLevel.Trace,
        Message = "Creating response for refresh token request")]
    internal static partial void CreatingResponseForRefreshTokenRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingResponseForDeviceCodeRequest),
        Level = LogLevel.Trace,
        Message = "Creating response for device code request")]
    internal static partial void CreatingResponseForDeviceCodeRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingResponseForCIBARequest),
        Level = LogLevel.Trace,
        Message = "Creating response for CIBA request")]
    internal static partial void CreatingResponseForCIBARequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CreatingResponseForExtensionGrantRequest),
        Level = LogLevel.Trace,
        Message = "Creating response for extension grant request")]
    internal static partial void CreatingResponseForExtensionGrantRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(HintWasForAccessToken),
        Level = LogLevel.Trace,
        Message = "Hint was for access token")]
    internal static partial void HintWasForAccessToken(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(HintWasForRefreshToken),
        Level = LogLevel.Trace,
        Message = "Hint was for refresh token")]
    internal static partial void HintWasForRefreshToken(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoHintForTokenType),
        Level = LogLevel.Trace,
        Message = "No hint for token type")]
    internal static partial void NoHintForTokenType(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(AccessTokenRevoked),
        Level = LogLevel.Debug,
        Message = "Access token revoked")]
    internal static partial void AccessTokenRevoked(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientDeniedFromRevokingAccessTokenBelongingTo),
        Level = LogLevel.Warning,
        Message = "Client {ClientId} denied from revoking access token belonging to Client {TokenClientId}")]
    internal static partial void ClientDeniedFromRevokingAccessTokenBelongingTo(this ILogger logger, object? clientId, object? tokenClientId);

    [LoggerMessage(
        EventName = nameof(RefreshTokenRevoked),
        Level = LogLevel.Debug,
        Message = "Refresh token revoked")]
    internal static partial void RefreshTokenRevoked(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientDeniedFromRevokingARefreshTokenBelonging),
        Level = LogLevel.Warning,
        Message = "Client {ClientId} denied from revoking a refresh token belonging to Client {TokenClientId}")]
    internal static partial void ClientDeniedFromRevokingARefreshTokenBelonging(this ILogger logger, object? clientId, object? tokenClientId);

    [LoggerMessage(
        EventName = nameof(CreatingUserinfoResponse),
        Level = LogLevel.Debug,
        Message = "Creating userinfo response")]
    internal static partial void CreatingUserinfoResponse(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(RequestedClaimTypes),
        Level = LogLevel.Debug,
        Message = "Requested claim types: {ClaimTypes}")]
    internal static partial void RequestedClaimTypes(this ILogger logger, object? claimTypes);

    [LoggerMessage(
        EventName = nameof(ProfileServiceReturnedNoClaimsNull),
        Level = LogLevel.Information,
        Message = "Profile service returned no claims (null)")]
    internal static partial void ProfileServiceReturnedNoClaimsNull(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ProfileServiceReturnedTheFollowingClaimTypes),
        Level = LogLevel.Information,
        Message = "Profile service returned the following claim types: {Types}")]
    internal static partial void ProfileServiceReturnedTheFollowingClaimTypes(this ILogger logger, object? types);

    [LoggerMessage(
        EventName = nameof(ProfileServiceReturnedIncorrectSubjectValue),
        Level = LogLevel.Error,
        Message = "Profile service returned incorrect subject value: {Sub}")]
    internal static partial void ProfileServiceReturnedIncorrectSubjectValue(this ILogger logger, object? sub);

    [LoggerMessage(
        EventName = nameof(ScopesInAccessToken),
        Level = LogLevel.Debug,
        Message = "Scopes in access token: {Scopes}")]
    internal static partial void ScopesInAccessToken(this ILogger logger, object? scopes);
}
