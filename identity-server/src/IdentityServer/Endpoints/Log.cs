// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Endpoints;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(ProcessingOAuthDiscoveryRequest),
        Level = LogLevel.Trace,
        Message = "Processing OAuth discovery request.")]
    internal static partial void ProcessingOAuthDiscoveryRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(OAuthDiscoveryEndpointOnlySupportsGETRequests),
        Level = LogLevel.Warning,
        Message = "OAuth Discovery endpoint only supports GET requests")]
    internal static partial void OAuthDiscoveryEndpointOnlySupportsGETRequests(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartOAuthDiscoveryRequest),
        Level = LogLevel.Debug,
        Message = "Start OAuth discovery request")]
    internal static partial void StartOAuthDiscoveryRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(OAuthDiscoveryEndpointDisabled404),
        Level = LogLevel.Information,
        Message = "OAuth Discovery endpoint disabled. 404.")]
    internal static partial void OAuthDiscoveryEndpointDisabled404(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(RequestForOAuthDiscoveryDocumentContainsPathBaseReturning),
        Level = LogLevel.Debug,
        Message = "Request for OAuth discovery document contains PathBase. Returning 404")]
    internal static partial void RequestForOAuthDiscoveryDocumentContainsPathBaseReturning(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(RequestForOAuthDiscoveryDocumentContainsInvalidSub),
        Level = LogLevel.Debug,
        Message = "Request for OAuth discovery document contains invalid sub-path. Returning 404")]
    internal static partial void RequestForOAuthDiscoveryDocumentContainsInvalidSub(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(OAuthDiscoveryRequestUriMismatch),
        Level = LogLevel.Debug,
        Message = "Request for OAuth discovery document with a request URL that does not match the issuer URI. Returning 404. Issuer: {Issuer}, Request: {Request}")]
    internal static partial void OAuthDiscoveryRequestUriMismatch(this ILogger logger, object issuer, object request);

    [LoggerMessage(
        EventName = nameof(CallingIntoOAuthDiscoveryResponseGenerator),
        Level = LogLevel.Trace,
        Message = "Calling into discovery response generator: {Type}")]
    internal static partial void CallingIntoOAuthDiscoveryResponseGenerator(this ILogger logger, string? type);

    [LoggerMessage(
        EventName = nameof(EndpointError),
        Level = LogLevel.Error,
        Message = "{Message}")]
    internal static partial void EndpointError(this ILogger logger, string message);

    [LoggerMessage(
        EventName = nameof(ValidatedAuthorizeRequestDetails),
        Level = LogLevel.Debug,
        Message = "ValidatedAuthorizeRequest\n{@ValidationDetails}")]
    internal static partial void ValidatedAuthorizeRequestDetails(this ILogger logger, object validationDetails);

    [LoggerMessage(
        EventName = nameof(AuthorizeEndpointResponseDetails),
        Level = LogLevel.Debug,
        Message = "Authorize endpoint response\n{@Details}")]
    internal static partial void AuthorizeEndpointResponseDetails(this ILogger logger, object details);

    [LoggerMessage(
        EventName = nameof(NoAccessTokenFound),
        Level = LogLevel.Error,
        Message = "No access token found.")]
    internal static partial void NoAccessTokenFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPMethodForAuthorizeEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP method for authorize endpoint.")]
    internal static partial void InvalidHTTPMethodForAuthorizeEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartAuthorizeCallbackRequest),
        Level = LogLevel.Debug,
        Message = "Start authorize callback request")]
    internal static partial void StartAuthorizeCallbackRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(EndAuthorizeRequestResultType),
        Level = LogLevel.Trace,
        Message = "End Authorize Request. Result type: {Value}")]
    internal static partial void EndAuthorizeRequestResultType(this ILogger logger, object? value);

    [LoggerMessage(
        EventName = nameof(StartAuthorizeRequest),
        Level = LogLevel.Debug,
        Message = "Start authorize request")]
    internal static partial void StartAuthorizeRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(EndAuthorizeRequestResultTypeAuthorizeEndpoint),
        Level = LogLevel.Trace,
        Message = "End authorize request. result type: {Value}")]
    internal static partial void EndAuthorizeRequestResultTypeAuthorizeEndpoint(this ILogger logger, object? value);

    [LoggerMessage(
        EventName = nameof(UserInAuthorizeRequest),
        Level = LogLevel.Debug,
        Message = "User in authorize request: {SubjectId}")]
    internal static partial void UserInAuthorizeRequest(this ILogger logger, object? subjectId);

    [LoggerMessage(
        EventName = nameof(NoUserPresentInAuthorizeRequest),
        Level = LogLevel.Debug,
        Message = "No user present in authorize request")]
    internal static partial void NoUserPresentInAuthorizeRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(AuthorizeRequestValidationErrorDetails),
        Level = LogLevel.Information,
        Message = "{@ValidationDetails}")]
    internal static partial void AuthorizeRequestValidationErrorDetails(this ILogger logger, object? validationDetails);

    [LoggerMessage(
        EventName = nameof(IdentityTokenIssuedFor),
        Level = LogLevel.Trace,
        Message = "Identity token issued for {ClientId} / {SubjectId}: {Token}")]
    internal static partial void IdentityTokenIssuedFor(this ILogger logger, object? clientId, object? subjectId, object? token);

    [LoggerMessage(
        EventName = nameof(CodeIssuedFor),
        Level = LogLevel.Trace,
        Message = "Code issued for {ClientId} / {SubjectId}: {Token}")]
    internal static partial void CodeIssuedFor(this ILogger logger, object? clientId, object? subjectId, object? token);

    [LoggerMessage(
        EventName = nameof(AccessTokenIssuedFor),
        Level = LogLevel.Trace,
        Message = "Access token issued for {ClientId} / {SubjectId}: {Token}")]
    internal static partial void AccessTokenIssuedFor(this ILogger logger, object? clientId, object? subjectId, object? token);

    [LoggerMessage(
        EventName = nameof(ProcessingBackchannelAuthenticationRequest),
        Level = LogLevel.Trace,
        Message = "Processing backchannel authentication request.")]
    internal static partial void ProcessingBackchannelAuthenticationRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPRequestForBackchannelAuthenticationEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP request for backchannel authentication endpoint")]
    internal static partial void InvalidHTTPRequestForBackchannelAuthenticationEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPRequestForBackchannelAuthenticationEndpointBackchannelAuthenticationEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP request for backchannel authentication endpoint")]
    internal static partial void InvalidHTTPRequestForBackchannelAuthenticationEndpointBackchannelAuthenticationEndpoint(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(StartBackchannelAuthenticationRequest),
        Level = LogLevel.Debug,
        Message = "Start backchannel authentication request.")]
    internal static partial void StartBackchannelAuthenticationRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientValidationFailedForBackchannelAuthenticationEndpoint),
        Level = LogLevel.Warning,
        Message = "Client validation failed for backchannel authentication endpoint: {Error}")]
    internal static partial void ClientValidationFailedForBackchannelAuthenticationEndpoint(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(CallingIntoBackchannelAuthenticationRequestValidator),
        Level = LogLevel.Trace,
        Message = "Calling into backchannel authentication request validator: {Type}")]
    internal static partial void CallingIntoBackchannelAuthenticationRequestValidator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(CallingIntoBackchannelAuthenticationRequestResponseGenerator),
        Level = LogLevel.Trace,
        Message = "Calling into backchannel authentication request response generator: {Type}")]
    internal static partial void CallingIntoBackchannelAuthenticationRequestResponseGenerator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(BackchannelAuthenticationRequestSuccess),
        Level = LogLevel.Debug,
        Message = "Backchannel authentication request success.")]
    internal static partial void BackchannelAuthenticationRequestSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(BackchannelAuthenticationResponseForSubject),
        Level = LogLevel.Trace,
        Message = "BackchannelAuthenticationResponse: {@Response} for subject {SubjectId}")]
    internal static partial void BackchannelAuthenticationResponseForSubject(this ILogger logger, object? response, object? subjectId);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPMethodForCheckSessionEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP method for check session endpoint")]
    internal static partial void InvalidHTTPMethodForCheckSessionEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(RenderingCheckSessionResult),
        Level = LogLevel.Debug,
        Message = "Rendering check session result")]
    internal static partial void RenderingCheckSessionResult(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ProcessingDeviceAuthorizeRequest),
        Level = LogLevel.Trace,
        Message = "Processing device authorize request.")]
    internal static partial void ProcessingDeviceAuthorizeRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPRequestForDeviceAuthorizeEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP request for device authorize endpoint")]
    internal static partial void InvalidHTTPRequestForDeviceAuthorizeEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPRequestForDeviceEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP request for device endpoint")]
    internal static partial void InvalidHTTPRequestForDeviceEndpoint(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(StartDeviceAuthorizeRequest),
        Level = LogLevel.Debug,
        Message = "Start device authorize request.")]
    internal static partial void StartDeviceAuthorizeRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoDeviceAuthorizeResponseGenerator),
        Level = LogLevel.Trace,
        Message = "Calling into device authorize response generator: {Type}")]
    internal static partial void CallingIntoDeviceAuthorizeResponseGenerator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(DeviceAuthorizeRequestSuccess),
        Level = LogLevel.Debug,
        Message = "Device authorize request success.")]
    internal static partial void DeviceAuthorizeRequestSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DeviceAuthorizationError),
        Level = LogLevel.Error,
        Message = "Device authorization error: {Error}:{ErrorDescriptions}")]
    internal static partial void DeviceAuthorizationError(this ILogger logger, object? error, object? errorDescriptions);

    [LoggerMessage(
        EventName = nameof(DeviceCodeIssuedFor),
        Level = LogLevel.Trace,
        Message = "Device code issued for {ClientId}: {DeviceCode}")]
    internal static partial void DeviceCodeIssuedFor(this ILogger logger, object? clientId, object? deviceCode);

    [LoggerMessage(
        EventName = nameof(UserCodeIssuedFor),
        Level = LogLevel.Trace,
        Message = "User code issued for {ClientId}: {UserCode}")]
    internal static partial void UserCodeIssuedFor(this ILogger logger, object? clientId, object? userCode);

    [LoggerMessage(
        EventName = nameof(VerificationURIIssuedFor),
        Level = LogLevel.Trace,
        Message = "Verification URI issued for {ClientId}: {VerificationUri}")]
    internal static partial void VerificationURIIssuedFor(this ILogger logger, object? clientId, object? verificationUri);

    [LoggerMessage(
        EventName = nameof(VerificationURICompleteIssuedFor),
        Level = LogLevel.Trace,
        Message = "Verification URI (Complete) issued for {ClientId}: {VerificationUriComplete}")]
    internal static partial void VerificationURICompleteIssuedFor(this ILogger logger, object? clientId, object? verificationUriComplete);

    [LoggerMessage(
        EventName = nameof(ProcessingDiscoveryRequest),
        Level = LogLevel.Trace,
        Message = "Processing discovery request.")]
    internal static partial void ProcessingDiscoveryRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DiscoveryEndpointOnlySupportsGETRequests),
        Level = LogLevel.Warning,
        Message = "Discovery endpoint only supports GET requests")]
    internal static partial void DiscoveryEndpointOnlySupportsGETRequests(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartDiscoveryRequest),
        Level = LogLevel.Debug,
        Message = "Start discovery request")]
    internal static partial void StartDiscoveryRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DiscoveryEndpointDisabled404),
        Level = LogLevel.Information,
        Message = "Discovery endpoint disabled. 404.")]
    internal static partial void DiscoveryEndpointDisabled404(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoDiscoveryResponseGenerator),
        Level = LogLevel.Trace,
        Message = "Calling into discovery response generator: {Type}")]
    internal static partial void CallingIntoDiscoveryResponseGenerator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(ProcessingDiscoveryRequestDiscoveryKeyEndpoint),
        Level = LogLevel.Trace,
        Message = "Processing discovery request.")]
    internal static partial void ProcessingDiscoveryRequestDiscoveryKeyEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DiscoveryEndpointOnlySupportsGETRequestsDiscoveryKeyEndpoint),
        Level = LogLevel.Warning,
        Message = "Discovery endpoint only supports GET requests")]
    internal static partial void DiscoveryEndpointOnlySupportsGETRequestsDiscoveryKeyEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartKeyDiscoveryRequest),
        Level = LogLevel.Debug,
        Message = "Start key discovery request")]
    internal static partial void StartKeyDiscoveryRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(KeyDiscoveryDisabled404),
        Level = LogLevel.Information,
        Message = "Key discovery disabled. 404.")]
    internal static partial void KeyDiscoveryDisabled404(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoDiscoveryResponseGeneratorDiscoveryKeyEndpoint),
        Level = LogLevel.Trace,
        Message = "Calling into discovery response generator: {Type}")]
    internal static partial void CallingIntoDiscoveryResponseGeneratorDiscoveryKeyEndpoint(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPMethodForEndSessionCallbackEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP method for end session callback endpoint.")]
    internal static partial void InvalidHTTPMethodForEndSessionCallbackEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ProcessingSignoutCallbackRequest),
        Level = LogLevel.Debug,
        Message = "Processing signout callback request")]
    internal static partial void ProcessingSignoutCallbackRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(SuccessfulSignoutCallback),
        Level = LogLevel.Information,
        Message = "Successful signout callback.")]
    internal static partial void SuccessfulSignoutCallback(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ErrorValidatingSignoutCallback),
        Level = LogLevel.Error,
        Message = "Error validating signout callback: {Error}")]
    internal static partial void ErrorValidatingSignoutCallback(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPRequestForEndSessionEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP request for end session endpoint")]
    internal static partial void InvalidHTTPRequestForEndSessionEndpoint(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPMethodForEndSessionEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP method for end session endpoint.")]
    internal static partial void InvalidHTTPMethodForEndSessionEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ProcessingSignoutRequestFor),
        Level = LogLevel.Debug,
        Message = "Processing signout request for {SubjectId}")]
    internal static partial void ProcessingSignoutRequestFor(this ILogger logger, object? subjectId);

    [LoggerMessage(
        EventName = nameof(ErrorProcessingEndSessionRequest),
        Level = LogLevel.Error,
        Message = "Error processing end session request {Error}")]
    internal static partial void ErrorProcessingEndSessionRequest(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(SuccessValidatingEndSessionRequestFrom),
        Level = LogLevel.Debug,
        Message = "Success validating end session request from {ClientId}")]
    internal static partial void SuccessValidatingEndSessionRequestFrom(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(ProcessingIntrospectionRequest),
        Level = LogLevel.Trace,
        Message = "Processing introspection request.")]
    internal static partial void ProcessingIntrospectionRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(IntrospectionEndpointOnlySupportsPOSTRequests),
        Level = LogLevel.Warning,
        Message = "Introspection endpoint only supports POST requests")]
    internal static partial void IntrospectionEndpointOnlySupportsPOSTRequests(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidMediaTypeForIntrospectionEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid media type for introspection endpoint")]
    internal static partial void InvalidMediaTypeForIntrospectionEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPRequestForIntrospectionEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP request for introspection endpoint")]
    internal static partial void InvalidHTTPRequestForIntrospectionEndpoint(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(StartingIntrospectionRequest),
        Level = LogLevel.Debug,
        Message = "Starting introspection request.")]
    internal static partial void StartingIntrospectionRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(UnauthorizedCallIntrospectionEndpointAborting),
        Level = LogLevel.Error,
        Message = "Unauthorized call introspection endpoint. aborting.")]
    internal static partial void UnauthorizedCallIntrospectionEndpointAborting(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientMakingIntrospectionRequest),
        Level = LogLevel.Debug,
        Message = "Client making introspection request: {ClientId}")]
    internal static partial void ClientMakingIntrospectionRequest(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(ApiResourceMakingIntrospectionRequest),
        Level = LogLevel.Debug,
        Message = "ApiResource making introspection request: {ApiId}")]
    internal static partial void ApiResourceMakingIntrospectionRequest(this ILogger logger, object? apiId);

    [LoggerMessage(
        EventName = nameof(MalformedRequestBodyAborting),
        Level = LogLevel.Error,
        Message = "Malformed request body. aborting.")]
    internal static partial void MalformedRequestBodyAborting(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoIntrospectionRequestValidator),
        Level = LogLevel.Trace,
        Message = "Calling into introspection request validator: {Type}")]
    internal static partial void CallingIntoIntrospectionRequestValidator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(CallingIntoIntrospectionResponseGenerator),
        Level = LogLevel.Trace,
        Message = "Calling into introspection response generator: {Type}")]
    internal static partial void CallingIntoIntrospectionResponseGenerator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(SuccessTokenIntrospectionTokenActiveForCaller),
        Level = LogLevel.Information,
        Message = "Success token introspection. Token active: {TokenActive}, for caller: {CallerName}")]
    internal static partial void SuccessTokenIntrospectionTokenActiveForCaller(this ILogger logger, object? tokenActive, object? callerName);

    [LoggerMessage(
        EventName = nameof(FailedTokenIntrospectionForCaller),
        Level = LogLevel.Error,
        Message = "Failed token introspection: {Error}, for caller: {CallerName}")]
    internal static partial void FailedTokenIntrospectionForCaller(this ILogger logger, object? error, object? callerName);

    [LoggerMessage(
        EventName = nameof(StartPushedAuthorizationRequest),
        Level = LogLevel.Debug,
        Message = "Start pushed authorization request")]
    internal static partial void StartPushedAuthorizationRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(LogMessagePushedAuthorizationEndpoint),
        Level = LogLevel.Information,
        Message = "{@ValidationDetails}")]
    internal static partial void LogMessagePushedAuthorizationEndpoint(this ILogger logger, object? validationDetails);

    [LoggerMessage(
        EventName = nameof(PushedAuthorizationRequestReturnedAnErrorWithA),
        Level = LogLevel.Debug,
        Message = "Pushed authorization request returned an error with a server issued nonce. This is an expected event when using DPoP server nonces.")]
    internal static partial void PushedAuthorizationRequestReturnedAnErrorWithA(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ProcessingTokenRequest),
        Level = LogLevel.Trace,
        Message = "Processing token request.")]
    internal static partial void ProcessingTokenRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPRequestForTokenEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP request for token endpoint")]
    internal static partial void InvalidHTTPRequestForTokenEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPRequestForTokenEndpointTokenEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP request for token endpoint")]
    internal static partial void InvalidHTTPRequestForTokenEndpointTokenEndpoint(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(StartTokenRequest),
        Level = LogLevel.Debug,
        Message = "Start token request.")]
    internal static partial void StartTokenRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientValidationFailedForTokenEndpoint),
        Level = LogLevel.Warning,
        Message = "Client validation failed for token endpoint: {Error}")]
    internal static partial void ClientValidationFailedForTokenEndpoint(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(CallingIntoTokenRequestValidator),
        Level = LogLevel.Trace,
        Message = "Calling into token request validator: {Type}")]
    internal static partial void CallingIntoTokenRequestValidator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(TokenRequestReturnedAnErrorWithAServer),
        Level = LogLevel.Debug,
        Message = "Token request returned an error with a server issued nonce. This is an expected event when using DPoP server nonces.")]
    internal static partial void TokenRequestReturnedAnErrorWithAServer(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoTokenRequestResponseGenerator),
        Level = LogLevel.Trace,
        Message = "Calling into token request response generator: {Type}")]
    internal static partial void CallingIntoTokenRequestResponseGenerator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(TokenRequestSuccess),
        Level = LogLevel.Debug,
        Message = "Token request success.")]
    internal static partial void TokenRequestSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(TooManyDPoPHeadersProvided),
        Level = LogLevel.Debug,
        Message = "Too many DPoP headers provided.")]
    internal static partial void TooManyDPoPHeadersProvided(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(IdentityTokenIssuedForTokenEndpoint),
        Level = LogLevel.Trace,
        Message = "Identity token issued for {ClientId} / {SubjectId}: {Token}")]
    internal static partial void IdentityTokenIssuedForTokenEndpoint(this ILogger logger, object? clientId, object? subjectId, object? token);

    [LoggerMessage(
        EventName = nameof(RefreshTokenIssuedFor),
        Level = LogLevel.Trace,
        Message = "Refresh token issued for {ClientId} / {SubjectId}: {Token}")]
    internal static partial void RefreshTokenIssuedFor(this ILogger logger, object? clientId, object? subjectId, object? token);

    [LoggerMessage(
        EventName = nameof(AccessTokenIssuedForTokenEndpoint),
        Level = LogLevel.Trace,
        Message = "Access token issued for {ClientId} / {SubjectId}: {Token}")]
    internal static partial void AccessTokenIssuedForTokenEndpoint(this ILogger logger, object? clientId, object? subjectId, object? token);

    [LoggerMessage(
        EventName = nameof(ProcessingRevocationRequest),
        Level = LogLevel.Trace,
        Message = "Processing revocation request.")]
    internal static partial void ProcessingRevocationRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPMethod),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP method")]
    internal static partial void InvalidHTTPMethod(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidMediaType),
        Level = LogLevel.Warning,
        Message = "Invalid media type")]
    internal static partial void InvalidMediaType(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPRequestForRevocationEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP request for revocation endpoint")]
    internal static partial void InvalidHTTPRequestForRevocationEndpoint(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(StartRevocationRequest),
        Level = LogLevel.Debug,
        Message = "Start revocation request.")]
    internal static partial void StartRevocationRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientValidationFailedForRevocationEndpoint),
        Level = LogLevel.Warning,
        Message = "Client validation failed for revocation endpoint: {Error}")]
    internal static partial void ClientValidationFailedForRevocationEndpoint(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(ClientValidationSuccessful),
        Level = LogLevel.Trace,
        Message = "Client validation successful")]
    internal static partial void ClientValidationSuccessful(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoTokenRevocationRequestValidator),
        Level = LogLevel.Trace,
        Message = "Calling into token revocation request validator: {Type}")]
    internal static partial void CallingIntoTokenRevocationRequestValidator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(CallingIntoTokenRevocationResponseGenerator),
        Level = LogLevel.Trace,
        Message = "Calling into token revocation response generator: {Type}")]
    internal static partial void CallingIntoTokenRevocationResponseGenerator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(TokenRevocationComplete),
        Level = LogLevel.Information,
        Message = "Token revocation complete")]
    internal static partial void TokenRevocationComplete(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoMatchingTokenFound),
        Level = LogLevel.Information,
        Message = "No matching token found")]
    internal static partial void NoMatchingTokenFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidHTTPMethodForUserinfoEndpoint),
        Level = LogLevel.Warning,
        Message = "Invalid HTTP method for userinfo endpoint.")]
    internal static partial void InvalidHTTPMethodForUserinfoEndpoint(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartUserinfoRequest),
        Level = LogLevel.Debug,
        Message = "Start userinfo request")]
    internal static partial void StartUserinfoRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoUserinfoRequestValidator),
        Level = LogLevel.Trace,
        Message = "Calling into userinfo request validator: {Type}")]
    internal static partial void CallingIntoUserinfoRequestValidator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(CallingIntoUserinfoResponseGenerator),
        Level = LogLevel.Trace,
        Message = "Calling into userinfo response generator: {Type}")]
    internal static partial void CallingIntoUserinfoResponseGenerator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(EndUserinfoRequest),
        Level = LogLevel.Debug,
        Message = "End userinfo request")]
    internal static partial void EndUserinfoRequest(this ILogger logger);
}
