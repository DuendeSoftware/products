// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(StartAuthorizeRequestProtocolValidation),
        Level = LogLevel.Debug,
        Message = "Start authorize request protocol validation")]
    internal static partial void StartAuthorizeRequestProtocolValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoCustomAuthorizeRequestValidator),
        Level = LogLevel.Debug,
        Message = "Calling into custom validator: {Type}")]
    internal static partial void CallingIntoCustomAuthorizeRequestValidator(this ILogger logger, string? type);

    [LoggerMessage(
        EventName = nameof(AuthorizeRequestProtocolValidationSuccessful),
        Level = LogLevel.Trace,
        Message = "Authorize request protocol validation successful")]
    internal static partial void AuthorizeRequestProtocolValidationSuccessful(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CheckingForPKCEParameters),
        Level = LogLevel.Debug,
        Message = "Checking for PKCE parameters")]
    internal static partial void CheckingForPKCEParameters(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoPKCEUsed),
        Level = LogLevel.Debug,
        Message = "No PKCE used.")]
    internal static partial void NoPKCEUsed(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(MissingCodeChallengeMethodDefaultingToPlain),
        Level = LogLevel.Debug,
        Message = "Missing code_challenge_method, defaulting to plain")]
    internal static partial void MissingCodeChallengeMethodDefaultingToPlain(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(IdTokenResponseTypeRequiresIdentityScopes),
        Level = LogLevel.Information,
        Message = "Requests for id_token response type must include identity scopes")]
    internal static partial void IdTokenResponseTypeRequiresIdentityScopes(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(IdTokenOnlyResponseTypeCannotIncludeResourceScopes),
        Level = LogLevel.Information,
        Message = "Requests for id_token response type only must not include resource scopes")]
    internal static partial void IdTokenOnlyResponseTypeCannotIncludeResourceScopes(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(TokenOnlyResponseTypeRequiresOnlyResourceScopes),
        Level = LogLevel.Information,
        Message = "Requests for token response type only must include resource scopes, but no identity scopes.")]
    internal static partial void TokenOnlyResponseTypeRequiresOnlyResourceScopes(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(UnsupportedDisplayModeIgnored),
        Level = LogLevel.Debug,
        Message = "Unsupported display mode - ignored: {Display}")]
    internal static partial void UnsupportedDisplayModeIgnored(this ILogger logger, object display);

    [LoggerMessage(
        EventName = nameof(AuthorizeRequestValidationInformationWithDetail),
        Level = LogLevel.Information,
        Message = "{Message}: {Detail}\n{@RequestDetails}")]
    internal static partial void AuthorizeRequestValidationInformationWithDetail(this ILogger logger, string message, object? detail, object requestDetails);

    [LoggerMessage(
        EventName = nameof(AuthorizeRequestValidationInformation),
        Level = LogLevel.Information,
        Message = "{Message}\n{@RequestDetails}")]
    internal static partial void AuthorizeRequestValidationInformation(this ILogger logger, string message, object requestDetails);

    [LoggerMessage(
        EventName = nameof(RequestMessageWithDetailsError),
        Level = LogLevel.Error,
        Message = "{Message}: {@Details}")]
    internal static partial void RequestMessageWithDetailsError(this ILogger logger, string message, object details);

    [LoggerMessage(
        EventName = nameof(RequestMessageWithDetailsWarning),
        Level = LogLevel.Warning,
        Message = "{Message}: {@Details}")]
    internal static partial void RequestMessageWithDetailsWarning(this ILogger logger, string message, object details);

    [LoggerMessage(
        EventName = nameof(RequestMessageWithDetailsInformation),
        Level = LogLevel.Information,
        Message = "{Message}: {@Details}")]
    internal static partial void RequestMessageWithDetailsInformation(this ILogger logger, string message, object details);

    [LoggerMessage(
        EventName = nameof(RequestMessageWithValuesAndDetailsError),
        Level = LogLevel.Error,
        Message = "{Message}: {@Values}, details: {@Details}")]
    internal static partial void RequestMessageWithValuesAndDetailsError(this ILogger logger, string message, object values, object details);

    [LoggerMessage(
        EventName = nameof(RequestMessageWithValuesAndDetailsWarning),
        Level = LogLevel.Warning,
        Message = "{Message}: {@Values}, details: {@Details}")]
    internal static partial void RequestMessageWithValuesAndDetailsWarning(this ILogger logger, string message, object values, object details);

    [LoggerMessage(
        EventName = nameof(RequestMessageWithValuesAndDetailsInformation),
        Level = LogLevel.Information,
        Message = "{Message}: {@Values}, details: {@Details}")]
    internal static partial void RequestMessageWithValuesAndDetailsInformation(this ILogger logger, string message, object values, object details);

    [LoggerMessage(
        EventName = nameof(ClientNotConfiguredWithCibaGrantType),
        Level = LogLevel.Error,
        Message = "Client {ClientId} not configured with the CIBA grant type., details: {@Details}")]
    internal static partial void ClientNotConfiguredWithCibaGrantType(this ILogger logger, string clientId, object details);

    [LoggerMessage(
        EventName = nameof(UnexpectedBackchannelAuthenticationUserValidatorError),
        Level = LogLevel.Error,
        Message = "Unexpected error from IBackchannelAuthenticationUserValidator: {Error}, details: {@Details}")]
    internal static partial void UnexpectedBackchannelAuthenticationUserValidatorError(this ILogger logger, string error, object details);

    [LoggerMessage(
        EventName = nameof(JwtRequestObjectClientIdMismatch),
        Level = LogLevel.Error,
        Message = "client_id found in the JWT request object does not match client_id used to authenticate, {@Values}, details: {@Details}")]
    internal static partial void JwtRequestObjectClientIdMismatch(this ILogger logger, object values, object details);

    [LoggerMessage(
        EventName = nameof(JwtRequestObjectParameterDuplicatedInRequestBody),
        Level = LogLevel.Error,
        Message = "Parameter from JWT request object also found in request body: {Name}, details: {@Details}")]
    internal static partial void JwtRequestObjectParameterDuplicatedInRequestBody(this ILogger logger, string name, object details);

    [LoggerMessage(
        EventName = nameof(UnexpectedCodeVerifier),
        Level = LogLevel.Error,
        Message = "Unexpected code_verifier: {CodeVerifier}. This happens when the client is trying to use PKCE, but it is not enabled. Set RequirePkce to true., details: {@Details}")]
    internal static partial void UnexpectedCodeVerifier(this ILogger logger, string codeVerifier, object details);

    [LoggerMessage(
        EventName = nameof(RequestDetailsError),
        Level = LogLevel.Error,
        Message = "{@Details}")]
    internal static partial void RequestDetailsError(this ILogger logger, object details);

    [LoggerMessage(
        EventName = nameof(RequestDetailsWarning),
        Level = LogLevel.Warning,
        Message = "{@Details}")]
    internal static partial void RequestDetailsWarning(this ILogger logger, object details);

    [LoggerMessage(
        EventName = nameof(RequestDetailsInformation),
        Level = LogLevel.Information,
        Message = "{@Details}")]
    internal static partial void RequestDetailsInformation(this ILogger logger, object details);

    [LoggerMessage(
        EventName = nameof(EndSessionValidationFailureWithoutDetails),
        Level = LogLevel.Information,
        Message = "{Message}")]
    internal static partial void EndSessionValidationFailureWithoutDetails(this ILogger logger, string message);

    [LoggerMessage(
        EventName = nameof(SamlLogoutIdMissing),
        Level = LogLevel.Warning,
        Message = "SAML front-channel logouts exist but SamlLogoutId is null. SP logout response tracking will be unavailable for this session")]
    internal static partial void SamlLogoutIdMissing(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionStoreNotRegistered),
        Level = LogLevel.Error,
        Message = "SAML logout session tracking was requested but ISamlLogoutSessionStore is not registered. Ensure SAML support is configured via AddSaml().")]
    internal static partial void SamlLogoutSessionStoreNotRegistered(this ILogger logger);

    [LoggerMessage(
        EventId = 1,
        EventName = nameof(GrantValidationError),
        Level = LogLevel.Error,
        Message = "Grant validation error: {Message}")]
    internal static partial void GrantValidationError(this ILogger logger, Exception exception, string message);

    [LoggerMessage(
        EventName = nameof(StartAPIValidation),
        Level = LogLevel.Trace,
        Message = "Start API validation")]
    internal static partial void StartAPIValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoAPISecretFound),
        Level = LogLevel.Debug,
        Message = "No API secret found")]
    internal static partial void NoAPISecretFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoAPIResourceWithThatNameFoundAborting),
        Level = LogLevel.Debug,
        Message = "No API resource with that name found. aborting")]
    internal static partial void NoAPIResourceWithThatNameFoundAborting(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(MoreThanOneAPIResourceWithThatName),
        Level = LogLevel.Error,
        Message = "More than one API resource with that name found. aborting")]
    internal static partial void MoreThanOneAPIResourceWithThatName(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(APIResourceNotEnabledAborting),
        Level = LogLevel.Error,
        Message = "API resource not enabled. aborting.")]
    internal static partial void APIResourceNotEnabledAborting(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(APIResourceValidationSuccess),
        Level = LogLevel.Debug,
        Message = "API resource validation success")]
    internal static partial void APIResourceValidationSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(APIValidationFailed),
        Level = LogLevel.Error,
        Message = "API validation failed.")]
    internal static partial void APIValidationFailed(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidAuthenticationRequestId),
        Level = LogLevel.Error,
        Message = "Invalid authentication request id")]
    internal static partial void InvalidAuthenticationRequestId(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientIsTryingToUseAAuthenticationRequest),
        Level = LogLevel.Error,
        Message = "Client {ClientId} is trying to use a authentication request id from client {RequestClientId}")]
    internal static partial void ClientIsTryingToUseAAuthenticationRequest(this ILogger logger, object? clientId, object? requestClientId);

    [LoggerMessage(
        EventName = nameof(ClientIsPollingTooFast),
        Level = LogLevel.Error,
        Message = "Client {ClientId} is polling too fast")]
    internal static partial void ClientIsPollingTooFast(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(ExpiredAuthenticationRequestId),
        Level = LogLevel.Error,
        Message = "Expired authentication request id")]
    internal static partial void ExpiredAuthenticationRequestId(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoScopesAuthorizedForBackchannelAuthenticationRequestAccess),
        Level = LogLevel.Error,
        Message = "No scopes authorized for backchannel authentication request. Access denied")]
    internal static partial void NoScopesAuthorizedForBackchannelAuthenticationRequestAccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(UserHasBeenDisabled),
        Level = LogLevel.Error,
        Message = "User has been disabled: {SubjectId}")]
    internal static partial void UserHasBeenDisabled(this ILogger logger, object? subjectId);

    [LoggerMessage(
        EventName = nameof(SuccessValidatingBackchannelAuthenticationRequestId),
        Level = LogLevel.Debug,
        Message = "Success validating backchannel authentication request id.")]
    internal static partial void SuccessValidatingBackchannelAuthenticationRequestId(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartBackchannelAuthenticationRequestValidation),
        Level = LogLevel.Debug,
        Message = "Start backchannel authentication request validation")]
    internal static partial void StartBackchannelAuthenticationRequestValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(IdpRequestedIsNotInClientRestrictionList),
        Level = LogLevel.Warning,
        Message = "idp requested ({Idp}) is not in client restriction list.")]
    internal static partial void IdpRequestedIsNotInClientRestrictionList(this ILogger logger, object? idp);

    [LoggerMessage(
        EventName = nameof(ErrorLoggingRequestDetails),
        Level = LogLevel.Error,
        Message = "Error logging {Exception}, request details: {@Details}")]
    internal static partial void ErrorLoggingRequestDetails(this ILogger logger, object? exception, object? details);

    [LoggerMessage(
        EventName = nameof(StartParsingBasicAuthenticationSecret),
        Level = LogLevel.Debug,
        Message = "Start parsing Basic Authentication secret")]
    internal static partial void StartParsingBasicAuthenticationSecret(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(AuthorizationHeaderExceedsMaximumLengthAllowed),
        Level = LogLevel.Error,
        Message = "Authorization header exceeds maximum length allowed.")]
    internal static partial void AuthorizationHeaderExceedsMaximumLengthAllowed(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(MalformedBasicAuthenticationCredential),
        Level = LogLevel.Warning,
        Message = "Malformed Basic Authentication credential.")]
    internal static partial void MalformedBasicAuthenticationCredential(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(MalformedBasicAuthenticationCredentialBasicAuthenticationSecretParser),
        Level = LogLevel.Warning,
        Message = "Malformed Basic Authentication credential.")]
    internal static partial void MalformedBasicAuthenticationCredentialBasicAuthenticationSecretParser(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(MalformedBasicAuthenticationCredentialBasicAuthenticationSecretParser2),
        Level = LogLevel.Warning,
        Message = "Malformed Basic Authentication credential.")]
    internal static partial void MalformedBasicAuthenticationCredentialBasicAuthenticationSecretParser2(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientIDExceedsMaximumLength),
        Level = LogLevel.Error,
        Message = "Client ID exceeds maximum length.")]
    internal static partial void ClientIDExceedsMaximumLength(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientSecretExceedsMaximumLength),
        Level = LogLevel.Error,
        Message = "Client secret exceeds maximum length.")]
    internal static partial void ClientSecretExceedsMaximumLength(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientIdWithoutSecretFound),
        Level = LogLevel.Debug,
        Message = "client id without secret found")]
    internal static partial void ClientIdWithoutSecretFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoBasicAuthenticationSecretFound),
        Level = LogLevel.Debug,
        Message = "No Basic Authentication secret found")]
    internal static partial void NoBasicAuthenticationSecretFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(BearerTokenFoundInHeader),
        Level = LogLevel.Debug,
        Message = "Bearer token found in header")]
    internal static partial void BearerTokenFoundInHeader(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(BearerTokenFoundInBody),
        Level = LogLevel.Debug,
        Message = "Bearer token found in body")]
    internal static partial void BearerTokenFoundInBody(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(BearerTokenNotFound),
        Level = LogLevel.Debug,
        Message = "Bearer token not found")]
    internal static partial void BearerTokenNotFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(UnexpectedHeaderFormat),
        Level = LogLevel.Trace,
        Message = "Unexpected header format: {Header}")]
    internal static partial void UnexpectedHeaderFormat(this ILogger logger, object? header);

    [LoggerMessage(
        EventName = nameof(StartClientValidation),
        Level = LogLevel.Debug,
        Message = "Start client validation")]
    internal static partial void StartClientValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoClientIdentifierFound),
        Level = LogLevel.Debug,
        Message = "No client identifier found")]
    internal static partial void NoClientIdentifierFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoClientWithIdFoundAborting),
        Level = LogLevel.Debug,
        Message = "No client with id '{ClientId}' found. aborting")]
    internal static partial void NoClientWithIdFoundAborting(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(PublicClientSkippingSecretValidationSuccess),
        Level = LogLevel.Debug,
        Message = "Public Client - skipping secret validation success")]
    internal static partial void PublicClientSkippingSecretValidationSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientSecretValidationFailedForClient),
        Level = LogLevel.Error,
        Message = "Client secret validation failed for client: {ClientId}.")]
    internal static partial void ClientSecretValidationFailedForClient(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(ClientValidationSuccess),
        Level = LogLevel.Debug,
        Message = "Client validation success")]
    internal static partial void ClientValidationSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DPoPProofTokenIsTooLong),
        Level = LogLevel.Debug,
        Message = "DPoP proof token is too long")]
    internal static partial void DPoPProofTokenIsTooLong(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(FailedToValidateDPoPHeader),
        Level = LogLevel.Debug,
        Message = "Failed to validate DPoP header")]
    internal static partial void FailedToValidateDPoPHeader(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(FailedToValidateDPoPSignature),
        Level = LogLevel.Debug,
        Message = "Failed to validate DPoP signature")]
    internal static partial void FailedToValidateDPoPSignature(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(FailedToValidateDPoPPayload),
        Level = LogLevel.Debug,
        Message = "Failed to validate DPoP payload")]
    internal static partial void FailedToValidateDPoPPayload(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(SuccessfullyValidatedDPoPProofTokenWithThumbprint),
        Level = LogLevel.Debug,
        Message = "Successfully validated DPoP proof token with thumbprint: {Jkt}")]
    internal static partial void SuccessfullyValidatedDPoPProofTokenWithThumbprint(this ILogger logger, object? jkt);

    [LoggerMessage(
        EventName = nameof(ErrorParsingDPoPToken),
        Level = LogLevel.Debug,
        Message = "Error parsing DPoP token: {Error}")]
    internal static partial void ErrorParsingDPoPToken(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(ErrorParsingDPoPJwkValue),
        Level = LogLevel.Debug,
        Message = "Error parsing DPoP jwk value: {Error}")]
    internal static partial void ErrorParsingDPoPJwkValue(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(NullCnfValueInDPoPAccessToken),
        Level = LogLevel.Debug,
        Message = "Null cnf value in DPoP access token.")]
    internal static partial void NullCnfValueInDPoPAccessToken(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(JktInDPoPAccessTokenDoesNotMatch),
        Level = LogLevel.Debug,
        Message = "jkt in DPoP access token does not match proof token key thumbprint.")]
    internal static partial void JktInDPoPAccessTokenDoesNotMatch(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(JktMemberMissingFromCnfClaimInDPoP),
        Level = LogLevel.Debug,
        Message = "jkt member missing from cnf claim in DPoP access token.")]
    internal static partial void JktMemberMissingFromCnfClaimInDPoP(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(FailedToParseDPoPCnfClaim),
        Level = LogLevel.Debug,
        Message = "Failed to parse DPoP cnf claim: {JsonExceptionMessage}")]
    internal static partial void FailedToParseDPoPCnfClaim(this ILogger logger, object? jsonExceptionMessage);

    [LoggerMessage(
        EventName = nameof(ErrorParsingDPoPTokenDefaultDPoPProofValidator),
        Level = LogLevel.Debug,
        Message = "Error parsing DPoP token: {Error}")]
    internal static partial void ErrorParsingDPoPTokenDefaultDPoPProofValidator(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(ErrorParsingDPoPTokenDefaultDPoPProofValidator2),
        Level = LogLevel.Debug,
        Message = "Error parsing DPoP token: {Error}")]
    internal static partial void ErrorParsingDPoPTokenDefaultDPoPProofValidator2(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(FailedToValidateDPoPTokenFreshness),
        Level = LogLevel.Debug,
        Message = "Failed to validate DPoP token freshness")]
    internal static partial void FailedToValidateDPoPTokenFreshness(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DetectedDPoPProofTokenReplayForJti),
        Level = LogLevel.Debug,
        Message = "Detected DPoP proof token replay for jti {Jti}")]
    internal static partial void DetectedDPoPProofTokenReplayForJti(this ILogger logger, object? jti);

    [LoggerMessage(
        EventName = nameof(AddingProofTokenWithJtiToReplayCache),
        Level = LogLevel.Debug,
        Message = "Adding proof token with jti {Jti} to replay cache for duration {CacheDuration}")]
    internal static partial void AddingProofTokenWithJtiToReplayCache(this ILogger logger, object? jti, object? cacheDuration);

    [LoggerMessage(
        EventName = nameof(InvalidTimeValueReadFromTheNonceValue),
        Level = LogLevel.Debug,
        Message = "Invalid time value read from the 'nonce' value")]
    internal static partial void InvalidTimeValueReadFromTheNonceValue(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DPoPNonceExpirationFailedItSPossibleThat),
        Level = LogLevel.Debug,
        Message = "DPoP 'nonce' expiration failed. It's possible that the server farm clocks might not be closely synchronized, so consider setting the ServerClockSkew on the DPoPOptions on the IdentityServerOptions.")]
    internal static partial void DPoPNonceExpirationFailedItSPossibleThat(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ErrorParsingDPoPNonceValue),
        Level = LogLevel.Debug,
        Message = "Error parsing DPoP 'nonce' value: {Error}")]
    internal static partial void ErrorParsingDPoPNonceValue(this ILogger logger, object? error);

    [LoggerMessage(
        EventName = nameof(ExpirationCheckFailedCreationTimeWasTooFar),
        Level = LogLevel.Debug,
        Message = "Expiration check failed. Creation time was too far in the future. The time being checked was {Iat}, and clock is now {Now}. The time difference is {Diff}")]
    internal static partial void ExpirationCheckFailedCreationTimeWasTooFar(this ILogger logger, object? iat, object? now, object? diff);

    [LoggerMessage(
        EventName = nameof(ExpirationCheckFailedExpirationHasAlreadyHappenedThe),
        Level = LogLevel.Debug,
        Message = "Expiration check failed. Expiration has already happened. The expiration was at {Exp}, and clock is now {Now}. The time difference is {Diff}")]
    internal static partial void ExpirationCheckFailedExpirationHasAlreadyHappenedThe(this ILogger logger, object? exp, object? now, object? diff);

    [LoggerMessage(
        EventName = nameof(CurrentIssuerIsNotAValidAbsoluteURI),
        Level = LogLevel.Debug,
        Message = "Current issuer is not a valid absolute URI: {Issuer}")]
    internal static partial void CurrentIssuerIsNotAValidAbsoluteURI(this ILogger logger, object? issuer);

    [LoggerMessage(
        EventName = nameof(CurrentIssuerPathDoesNotMatchTheProvided),
        Level = LogLevel.Debug,
        Message = "Current issuer path '{IssuerPath}' does not match the provided path '{ProvidedPath}'")]
    internal static partial void CurrentIssuerPathDoesNotMatchTheProvided(this ILogger logger, object? issuerPath, object? providedPath);

    [LoggerMessage(
        EventName = nameof(InvalidParsedScopeMessage),
        Level = LogLevel.Error,
        Message = "Invalid parsed scope {Scope}, message: {Error}")]
    internal static partial void InvalidParsedScopeMessage(this ILogger logger, object? scope, object? error);

    [LoggerMessage(
        EventName = nameof(InvalidResourceIdentifierItIsEitherNotFound),
        Level = LogLevel.Error,
        Message = "Invalid resource identifier {Resource}. It is either not found, not enabled, or does not support any of the requested scopes.")]
    internal static partial void InvalidResourceIdentifierItIsEitherNotFound(this ILogger logger, object? resource);

    [LoggerMessage(
        EventName = nameof(ScopeNotFoundInStoreOrNotSupported),
        Level = LogLevel.Error,
        Message = "Scope {Scope} not found in store or not supported by requested resource indicators.")]
    internal static partial void ScopeNotFoundInStoreOrNotSupported(this ILogger logger, object? scope);

    [LoggerMessage(
        EventName = nameof(ClientIsNotAllowedAccessToScope),
        Level = LogLevel.Error,
        Message = "Client {Client} is not allowed access to scope {Scope}.")]
    internal static partial void ClientIsNotAllowedAccessToScope(this ILogger logger, object? client, object? scope);

    [LoggerMessage(
        EventName = nameof(ClientIsNotAllowedAccessToScopeDefaultResourceValidator),
        Level = LogLevel.Error,
        Message = "Client {Client} is not allowed access to scope {Scope}.")]
    internal static partial void ClientIsNotAllowedAccessToScopeDefaultResourceValidator(this ILogger logger, object? client, object? scope);

    [LoggerMessage(
        EventName = nameof(ClientIsNotAllowedAccessToScopeOffline),
        Level = LogLevel.Error,
        Message = "Client {Client} is not allowed access to scope offline_access (via AllowOfflineAccess setting).")]
    internal static partial void ClientIsNotAllowedAccessToScopeOffline(this ILogger logger, object? client);

    [LoggerMessage(
        EventName = nameof(ACollectionOfScopesCannotBeNull),
        Level = LogLevel.Error,
        Message = "A collection of scopes cannot be null.")]
    internal static partial void ACollectionOfScopesCannotBeNull(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ScopeParsingIgnoringScope),
        Level = LogLevel.Debug,
        Message = "Scope parsing ignoring scope {Scope}")]
    internal static partial void ScopeParsingIgnoringScope(this ILogger logger, object? scope);

    [LoggerMessage(
        EventName = nameof(StartDeviceAuthorizationRequestValidation),
        Level = LogLevel.Debug,
        Message = "Start device authorization request validation")]
    internal static partial void StartDeviceAuthorizationRequestValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DeviceAuthorizationRequestValidationSuccess),
        Level = LogLevel.Debug,
        Message = "{ClientId} device authorization request validation success")]
    internal static partial void DeviceAuthorizationRequestValidationSuccess(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(DeviceAuthorizationRequestValidationFailure),
        Level = LogLevel.Error,
        Message = "{Message}:{RequestDetails}")]
    internal static partial void DeviceAuthorizationRequestValidationFailure(this ILogger logger, object? message, object? requestDetails);

    [LoggerMessage(
        EventName = nameof(LogMessageDeviceAuthorizationRequestValidator),
        Level = LogLevel.Error,
        Message = "{Message}: {Detail}:{RequestDetails}")]
    internal static partial void LogMessageDeviceAuthorizationRequestValidator(this ILogger logger, object? message, object? detail, object? requestDetails);

    [LoggerMessage(
        EventName = nameof(ClientProvidedNoScopesCheckingAllowedScopesList),
        Level = LogLevel.Trace,
        Message = "Client provided no scopes - checking allowed scopes list")]
    internal static partial void ClientProvidedNoScopesCheckingAllowedScopesList(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DefaultingTo),
        Level = LogLevel.Trace,
        Message = "Defaulting to: {Scopes}")]
    internal static partial void DefaultingTo(this ILogger logger, object? scopes);

    [LoggerMessage(
        EventName = nameof(InvalidDeviceCode),
        Level = LogLevel.Error,
        Message = "Invalid device code")]
    internal static partial void InvalidDeviceCode(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientIsTryingToUseADeviceCode),
        Level = LogLevel.Error,
        Message = "Client {ClientId} is trying to use a device code from client {DeviceCodeClientId}")]
    internal static partial void ClientIsTryingToUseADeviceCode(this ILogger logger, object? clientId, object? deviceCodeClientId);

    [LoggerMessage(
        EventName = nameof(ClientIsPollingTooFastDeviceCodeValidator),
        Level = LogLevel.Error,
        Message = "Client {ClientId} is polling too fast")]
    internal static partial void ClientIsPollingTooFastDeviceCodeValidator(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(ExpiredDeviceCode),
        Level = LogLevel.Error,
        Message = "Expired device code")]
    internal static partial void ExpiredDeviceCode(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoScopesAuthorizedForDeviceAuthorizationAccessDenied),
        Level = LogLevel.Error,
        Message = "No scopes authorized for device authorization. Access denied")]
    internal static partial void NoScopesAuthorizedForDeviceAuthorizationAccessDenied(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(UserHasBeenDisabledDeviceCodeValidator),
        Level = LogLevel.Error,
        Message = "User has been disabled: {SubjectId}")]
    internal static partial void UserHasBeenDisabledDeviceCodeValidator(this ILogger logger, object? subjectId);

    [LoggerMessage(
        EventName = nameof(StartEndSessionRequestValidation),
        Level = LogLevel.Debug,
        Message = "Start end session request validation")]
    internal static partial void StartEndSessionRequestValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(UILocaleTooLongItWillBeIgnored),
        Level = LogLevel.Warning,
        Message = "UI locale too long. It will be ignored:{@Details}")]
    internal static partial void UILocaleTooLongItWillBeIgnored(this ILogger logger, object? details);

    [LoggerMessage(
        EventName = nameof(InvalidPostLogoutRedirectUri),
        Level = LogLevel.Warning,
        Message = "Invalid PostLogoutRedirectUri: {PostLogoutRedirectUri}")]
    internal static partial void InvalidPostLogoutRedirectUri(this ILogger logger, object? postLogoutRedirectUri);

    [LoggerMessage(
        EventName = nameof(LogMessageEndSessionRequestValidator),
        Level = LogLevel.Information,
        Message = "{Message}:{@Details}")]
    internal static partial void LogMessageEndSessionRequestValidator(this ILogger logger, object? message, object? details);

    [LoggerMessage(
        EventName = nameof(EndSessionRequestValidationSuccess),
        Level = LogLevel.Information,
        Message = "End session request validation success:{@Details}")]
    internal static partial void EndSessionRequestValidationSuccess(this ILogger logger, object? details);

    [LoggerMessage(
        EventName = nameof(NoValidatorFoundForGrantType),
        Level = LogLevel.Error,
        Message = "No validator found for grant type")]
    internal static partial void NoValidatorFoundForGrantType(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoCustomGrantValidator),
        Level = LogLevel.Trace,
        Message = "Calling into custom grant validator: {Type}")]
    internal static partial void CallingIntoCustomGrantValidator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(HashedSharedSecretValidatorCannotProcess),
        Level = LogLevel.Debug,
        Message = "Hashed shared secret validator cannot process {Type}")]
    internal static partial void HashedSharedSecretValidatorCannotProcess(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(NoSharedSecretConfiguredForClient),
        Level = LogLevel.Debug,
        Message = "No shared secret configured for client.")]
    internal static partial void NoSharedSecretConfiguredForClient(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(SecretUsesInvalidHashingAlgorithm),
        Level = LogLevel.Information,
        Message = "Secret: {Description} uses invalid hashing algorithm.")]
    internal static partial void SecretUsesInvalidHashingAlgorithm(this ILogger logger, object? description);

    [LoggerMessage(
        EventName = nameof(SecretIsNull),
        Level = LogLevel.Information,
        Message = "Secret: {Description} is null.")]
    internal static partial void SecretIsNull(this ILogger logger, object? description);

    [LoggerMessage(
        EventName = nameof(SecretUsesInvalidHashingAlgorithmHashedSharedSecretValidator),
        Level = LogLevel.Information,
        Message = "Secret: {Description} uses invalid hashing algorithm.")]
    internal static partial void SecretUsesInvalidHashingAlgorithmHashedSharedSecretValidator(this ILogger logger, object? description);

    [LoggerMessage(
        EventName = nameof(NoMatchingHashedSecretFound),
        Level = LogLevel.Debug,
        Message = "No matching hashed secret found.")]
    internal static partial void NoMatchingHashedSecretFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(IntrospectionRequestValidationStarted),
        Level = LogLevel.Debug,
        Message = "Introspection request validation started.")]
    internal static partial void IntrospectionRequestValidationStarted(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(TokenIsMissing),
        Level = LogLevel.Error,
        Message = "Token is missing")]
    internal static partial void TokenIsMissing(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(TokenTypeHintFoundInRequest),
        Level = LogLevel.Debug,
        Message = "Token type hint found in request: {TokenTypeHint}")]
    internal static partial void TokenTypeHintFoundInRequest(this ILogger logger, object? tokenTypeHint);

    [LoggerMessage(
        EventName = nameof(UnsupportedTokenTypeHintFoundInRequest),
        Level = LogLevel.Debug,
        Message = "Unsupported token type hint found in request: {TokenTypeHint}")]
    internal static partial void UnsupportedTokenTypeHintFoundInRequest(this ILogger logger, object? tokenTypeHint);

    [LoggerMessage(
        EventName = nameof(FailedToValidateTokenAsAccessTokenPossible),
        Level = LogLevel.Debug,
        Message = "Failed to validate token as access token. Possible incorrect token_type_hint parameter.")]
    internal static partial void FailedToValidateTokenAsAccessTokenPossible(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(FailedToValidateTokenAsRefreshTokenPossible),
        Level = LogLevel.Debug,
        Message = "Failed to validate token as refresh token. Possible incorrect token_type_hint parameter.")]
    internal static partial void FailedToValidateTokenAsRefreshTokenPossible(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(IntrospectionRequestValidationSuccessful),
        Level = LogLevel.Debug,
        Message = "Introspection request validation successful.")]
    internal static partial void IntrospectionRequestValidationSuccessful(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(TokenIsInvalid),
        Level = LogLevel.Debug,
        Message = "Token is invalid.")]
    internal static partial void TokenIsInvalid(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ValidatedAccessToken),
        Level = LogLevel.Debug,
        Message = "Validated access token")]
    internal static partial void ValidatedAccessToken(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ValidatedAccessTokenIntrospectionRequestValidator),
        Level = LogLevel.Debug,
        Message = "Validated access token")]
    internal static partial void ValidatedAccessTokenIntrospectionRequestValidator(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartParsingForJWTClientAssertionInPost),
        Level = LogLevel.Debug,
        Message = "Start parsing for JWT client assertion in post body")]
    internal static partial void StartParsingForJWTClientAssertionInPost(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ContentTypeIsNotAForm),
        Level = LogLevel.Debug,
        Message = "Content type is not a form")]
    internal static partial void ContentTypeIsNotAForm(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientAssertionTokenExceedsMaximumLength),
        Level = LogLevel.Error,
        Message = "Client assertion token exceeds maximum length.")]
    internal static partial void ClientAssertionTokenExceedsMaximumLength(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientIDExceedsMaximumLengthJwtBearerClientAssertionSecretParser),
        Level = LogLevel.Error,
        Message = "Client ID exceeds maximum length.")]
    internal static partial void ClientIDExceedsMaximumLengthJwtBearerClientAssertionSecretParser(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoJWTClientAssertionFoundInPostBody),
        Level = LogLevel.Debug,
        Message = "No JWT client assertion found in post body")]
    internal static partial void NoJWTClientAssertionFoundInPostBody(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CouldNotParseClientAssertion),
        Level = LogLevel.Warning,
        Message = "Could not parse client assertion")]
    internal static partial void CouldNotParseClientAssertion(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(CouldNotParseClientSecrets),
        Level = LogLevel.Error,
        Message = "Could not parse client secrets")]
    internal static partial void CouldNotParseClientSecrets(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(ThereAreNoKeysAvailableToValidateJWT),
        Level = LogLevel.Error,
        Message = "There are no keys available to validate JWT.")]
    internal static partial void ThereAreNoKeysAvailableToValidateJWT(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(JWTTokenValidationError),
        Level = LogLevel.Error,
        Message = "JWT token validation error")]
    internal static partial void JWTTokenValidationError(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(JWTPayloadMustNotContainRequestOrRequest),
        Level = LogLevel.Error,
        Message = "JWT payload must not contain request or request_uri")]
    internal static partial void JWTPayloadMustNotContainRequestOrRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(JWTRequestObjectValidationSuccess),
        Level = LogLevel.Debug,
        Message = "JWT request object validation success.")]
    internal static partial void JWTRequestObjectValidationSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartParsingForClientIdInPostBody),
        Level = LogLevel.Debug,
        Message = "Start parsing for client id in post body")]
    internal static partial void StartParsingForClientIdInPostBody(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ContentTypeIsNotAFormMutualTlsSecretParser),
        Level = LogLevel.Debug,
        Message = "Content type is not a form")]
    internal static partial void ContentTypeIsNotAFormMutualTlsSecretParser(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientIDExceedsMaximumLengthMutualTlsSecretParser),
        Level = LogLevel.Error,
        Message = "Client ID exceeds maximum length.")]
    internal static partial void ClientIDExceedsMaximumLengthMutualTlsSecretParser(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientCertificateNotPresent),
        Level = LogLevel.Debug,
        Message = "Client certificate not present")]
    internal static partial void ClientCertificateNotPresent(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoPostBodyFound),
        Level = LogLevel.Debug,
        Message = "No post body found")]
    internal static partial void NoPostBodyFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ResourceOwnerPasswordCredentialTypeNotSupportedConfigure),
        Level = LogLevel.Information,
        Message = "Resource owner password credential type not supported. Configure an IResourceOwnerPasswordValidator.")]
    internal static partial void ResourceOwnerPasswordCredentialTypeNotSupportedConfigure(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ParsedSecretShouldNotBeOfType),
        Level = LogLevel.Error,
        Message = "Parsed secret should not be of type: {Type}")]
    internal static partial void ParsedSecretShouldNotBeOfType(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(NoSharedSecretConfiguredForClientPlainTextSharedSecretValidator),
        Level = LogLevel.Debug,
        Message = "No shared secret configured for client.")]
    internal static partial void NoSharedSecretConfiguredForClientPlainTextSharedSecretValidator(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoMatchingPlainTextSecretFound),
        Level = LogLevel.Debug,
        Message = "No matching plain text secret found.")]
    internal static partial void NoMatchingPlainTextSecretFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartParsingForSecretInPostBody),
        Level = LogLevel.Debug,
        Message = "Start parsing for secret in post body")]
    internal static partial void StartParsingForSecretInPostBody(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ContentTypeIsNotAFormPostBodySecretParser),
        Level = LogLevel.Debug,
        Message = "Content type is not a form")]
    internal static partial void ContentTypeIsNotAFormPostBodySecretParser(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientIDExceedsMaximumLengthPostBodySecretParser),
        Level = LogLevel.Error,
        Message = "Client ID exceeds maximum length.")]
    internal static partial void ClientIDExceedsMaximumLengthPostBodySecretParser(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientSecretExceedsMaximumLengthPostBodySecretParser),
        Level = LogLevel.Error,
        Message = "Client secret exceeds maximum length.")]
    internal static partial void ClientSecretExceedsMaximumLengthPostBodySecretParser(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientIdWithoutSecretFoundPostBodySecretParser),
        Level = LogLevel.Debug,
        Message = "client id without secret found")]
    internal static partial void ClientIdWithoutSecretFoundPostBodySecretParser(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoSecretInPostBodyFound),
        Level = LogLevel.Debug,
        Message = "No secret in post body found")]
    internal static partial void NoSecretInPostBodyFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ParsedSecretCredentialIsNotAString),
        Level = LogLevel.Error,
        Message = "ParsedSecret.Credential is not a string.")]
    internal static partial void ParsedSecretCredentialIsNotAString(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CouldNotParseSecrets),
        Level = LogLevel.Error,
        Message = "Could not parse secrets")]
    internal static partial void CouldNotParseSecrets(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(ThereAreNoKeysAvailableToValidateClient),
        Level = LogLevel.Error,
        Message = "There are no keys available to validate client assertion.")]
    internal static partial void ThereAreNoKeysAvailableToValidateClient(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ErrorReadingJWTHeader),
        Level = LogLevel.Error,
        Message = "Error reading JWT header.")]
    internal static partial void ErrorReadingJWTHeader(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(JWTTokenValidationErrorPrivateKeyJwtSecretValidator),
        Level = LogLevel.Error,
        Message = "JWT token validation error")]
    internal static partial void JWTTokenValidationErrorPrivateKeyJwtSecretValidator(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(BothSubAndIssInTheClientAssertion),
        Level = LogLevel.Error,
        Message = "Both 'sub' and 'iss' in the client assertion token must have a value of client_id.")]
    internal static partial void BothSubAndIssInTheClientAssertion(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ExpIsMissing),
        Level = LogLevel.Error,
        Message = "exp is missing.")]
    internal static partial void ExpIsMissing(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(JtiIsMissing),
        Level = LogLevel.Error,
        Message = "jti is missing.")]
    internal static partial void JtiIsMissing(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(JtiIsFoundInReplayCachePossibleReplay),
        Level = LogLevel.Error,
        Message = "jti is found in replay cache. Possible replay attack.")]
    internal static partial void JtiIsFoundInReplayCachePossibleReplay(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(AudienceValidatedAudience),
        Level = LogLevel.Information,
        Message = "Audience Validated.Audience: '{Audience}'")]
    internal static partial void AudienceValidatedAudience(this ILogger logger, object? audience);

    [LoggerMessage(
        EventName = nameof(DPoPProofTokenIsTooLongPushedAuthorizationRequestValidator),
        Level = LogLevel.Error,
        Message = "DPoP proof token is too long")]
    internal static partial void DPoPProofTokenIsTooLongPushedAuthorizationRequestValidator(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(LogMessageRequestObjectValidator),
        Level = LogLevel.Error,
        Message = "{Message}: {@RequestDetails}")]
    internal static partial void LogMessageRequestObjectValidator(this ILogger logger, object? message, object? requestDetails);

    [LoggerMessage(
        EventName = nameof(LogMessageRequestObjectValidator2),
        Level = LogLevel.Error,
        Message = "{Message}: {Detail}:{@RequestDetails}")]
    internal static partial void LogMessageRequestObjectValidator2(this ILogger logger, object? message, object? detail, object? requestDetails);

    [LoggerMessage(
        EventName = nameof(ParserFoundSecret),
        Level = LogLevel.Debug,
        Message = "Parser found secret: {Type}")]
    internal static partial void ParserFoundSecret(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(SecretIdFound),
        Level = LogLevel.Debug,
        Message = "Secret id found: {Id}")]
    internal static partial void SecretIdFound(this ILogger logger, object? id);

    [LoggerMessage(
        EventName = nameof(ParserFoundNoSecret),
        Level = LogLevel.Debug,
        Message = "Parser found no secret")]
    internal static partial void ParserFoundNoSecret(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(SecretIsExpired),
        Level = LogLevel.Information,
        Message = "Secret [{Description}] is expired")]
    internal static partial void SecretIsExpired(this ILogger logger, object? description);

    [LoggerMessage(
        EventName = nameof(SecretValidatorSuccess),
        Level = LogLevel.Debug,
        Message = "Secret validator success: {Value}")]
    internal static partial void SecretValidatorSuccess(this ILogger logger, object? value);

    [LoggerMessage(
        EventName = nameof(SecretValidatorsCouldNotValidateSecret),
        Level = LogLevel.Debug,
        Message = "Secret validators could not validate secret")]
    internal static partial void SecretValidatorsCouldNotValidateSecret(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CheckingFor127001RedirectURI),
        Level = LogLevel.Debug,
        Message = "Checking for 127.0.0.1 redirect URI")]
    internal static partial void CheckingFor127001RedirectURI(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(RequestedUriIsNullOrEmpty),
        Level = LogLevel.Debug,
        Message = "'requestedUri' is null or empty")]
    internal static partial void RequestedUriIsNullOrEmpty(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidFormatHttp127001Port),
        Level = LogLevel.Debug,
        Message = "invalid format - http://127.0.0.1:port is required.")]
    internal static partial void InvalidFormatHttp127001Port(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidFormatHttp127001PortStrictRedirectUriValidatorAppAuth),
        Level = LogLevel.Debug,
        Message = "invalid format - http://127.0.0.1:port is required.")]
    internal static partial void InvalidFormatHttp127001PortStrictRedirectUriValidatorAppAuth(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(InvalidPort),
        Level = LogLevel.Debug,
        Message = "invalid port")]
    internal static partial void InvalidPort(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartTokenRequestValidation),
        Level = LogLevel.Debug,
        Message = "Start token request validation")]
    internal static partial void StartTokenRequestValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CallingIntoCustomRequestValidator),
        Level = LogLevel.Trace,
        Message = "Calling into custom request validator: {Type}")]
    internal static partial void CallingIntoCustomRequestValidator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(StartValidationOfAuthorizationCodeTokenRequest),
        Level = LogLevel.Debug,
        Message = "Start validation of authorization code token request")]
    internal static partial void StartValidationOfAuthorizationCodeTokenRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientRequiredAProofKeyForCodeExchange),
        Level = LogLevel.Debug,
        Message = "Client required a proof key for code exchange. Starting PKCE validation")]
    internal static partial void ClientRequiredAProofKeyForCodeExchange(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ValidationOfAuthorizationCodeTokenRequestSuccess),
        Level = LogLevel.Debug,
        Message = "Validation of authorization code token request success")]
    internal static partial void ValidationOfAuthorizationCodeTokenRequestSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartClientCredentialsTokenRequestValidation),
        Level = LogLevel.Debug,
        Message = "Start client credentials token request validation")]
    internal static partial void StartClientCredentialsTokenRequestValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(CredentialsTokenRequestValidationSuccess),
        Level = LogLevel.Debug,
        Message = "{ClientId} credentials token request validation success")]
    internal static partial void CredentialsTokenRequestValidationSuccess(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(StartResourceOwnerPasswordTokenRequestValidation),
        Level = LogLevel.Debug,
        Message = "Start resource owner password token request validation")]
    internal static partial void StartResourceOwnerPasswordTokenRequestValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ResourceOwnerPasswordTokenRequestValidationSuccess),
        Level = LogLevel.Debug,
        Message = "Resource owner password token request validation success.")]
    internal static partial void ResourceOwnerPasswordTokenRequestValidationSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartValidationOfRefreshTokenRequest),
        Level = LogLevel.Debug,
        Message = "Start validation of refresh token request")]
    internal static partial void StartValidationOfRefreshTokenRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ValidationOfRefreshTokenRequestSuccess),
        Level = LogLevel.Debug,
        Message = "Validation of refresh token request success")]
    internal static partial void ValidationOfRefreshTokenRequestSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartValidationOfDeviceCodeRequest),
        Level = LogLevel.Debug,
        Message = "Start validation of device code request")]
    internal static partial void StartValidationOfDeviceCodeRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ValidationOfDeviceCodeTokenRequestSuccess),
        Level = LogLevel.Debug,
        Message = "Validation of device code token request success")]
    internal static partial void ValidationOfDeviceCodeTokenRequestSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartValidationOfCIBARequest),
        Level = LogLevel.Debug,
        Message = "Start validation of CIBA request")]
    internal static partial void StartValidationOfCIBARequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ValidationOfCIBATokenRequestSuccess),
        Level = LogLevel.Debug,
        Message = "Validation of CIBA token request success")]
    internal static partial void ValidationOfCIBATokenRequestSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(StartValidationOfCustomGrantTokenRequest),
        Level = LogLevel.Debug,
        Message = "Start validation of custom grant token request")]
    internal static partial void StartValidationOfCustomGrantTokenRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ValidationOfExtensionGrantTokenRequestSuccess),
        Level = LogLevel.Debug,
        Message = "Validation of extension grant token request success")]
    internal static partial void ValidationOfExtensionGrantTokenRequestSuccess(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientProvidedNoScopesCheckingAllowedScopesListTokenRequestValidator),
        Level = LogLevel.Trace,
        Message = "Client provided no scopes - checking allowed scopes list")]
    internal static partial void ClientProvidedNoScopesCheckingAllowedScopesListTokenRequestValidator(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DefaultingToTokenRequestValidator),
        Level = LogLevel.Trace,
        Message = "Defaulting to: {Scopes}")]
    internal static partial void DefaultingToTokenRequestValidator(this ILogger logger, object? scopes);

    [LoggerMessage(
        EventName = nameof(ErrorLoggingRequestDetailsTokenRequestValidator),
        Level = LogLevel.Error,
        Message = "Error logging {Exception}, request details: {@Details}")]
    internal static partial void ErrorLoggingRequestDetailsTokenRequestValidator(this ILogger logger, object? exception, object? details);

    [LoggerMessage(
        EventName = nameof(ValidateRequestAsyncCalled),
        Level = LogLevel.Trace,
        Message = "ValidateRequestAsync called")]
    internal static partial void ValidateRequestAsyncCalled(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoParametersPassed),
        Level = LogLevel.Error,
        Message = "no parameters passed")]
    internal static partial void NoParametersPassed(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoClientPassed),
        Level = LogLevel.Error,
        Message = "no client passed")]
    internal static partial void NoClientPassed(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoTokenFoundInRequest),
        Level = LogLevel.Error,
        Message = "No token found in request")]
    internal static partial void NoTokenFoundInRequest(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(TokenTypeHintFoundInRequestTokenRevocationRequestValidator),
        Level = LogLevel.Debug,
        Message = "Token type hint found in request: {TokenTypeHint}")]
    internal static partial void TokenTypeHintFoundInRequestTokenRevocationRequestValidator(this ILogger logger, object? tokenTypeHint);

    [LoggerMessage(
        EventName = nameof(InvalidTokenTypeHint),
        Level = LogLevel.Error,
        Message = "Invalid token type hint: {TokenTypeHint}")]
    internal static partial void InvalidTokenTypeHint(this ILogger logger, object? tokenTypeHint);

    [LoggerMessage(
        EventName = nameof(ValidateRequestAsyncResult),
        Level = LogLevel.Debug,
        Message = "ValidateRequestAsync result: {ValidateRequestResult}")]
    internal static partial void ValidateRequestAsyncResult(this ILogger logger, object? validateRequestResult);

    [LoggerMessage(
        EventName = nameof(StartIdentityTokenValidation),
        Level = LogLevel.Debug,
        Message = "Start identity token validation")]
    internal static partial void StartIdentityTokenValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(JWTTooLong),
        Level = LogLevel.Information,
        Message = "JWT too long")]
    internal static partial void JWTTooLong(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoClientIdSuppliedCanTFindIdIn),
        Level = LogLevel.Information,
        Message = "No clientId supplied, can't find id in identity token.")]
    internal static partial void NoClientIdSuppliedCanTFindIdIn(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(UnknownOrDisabledClient),
        Level = LogLevel.Information,
        Message = "Unknown or disabled client: {ClientId}.")]
    internal static partial void UnknownOrDisabledClient(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(ClientFoundForTokenValidation),
        Level = LogLevel.Debug,
        Message = "Client found: {ClientId} / {ClientName}")]
    internal static partial void ClientFoundForTokenValidation(this ILogger logger, object? clientId, object? clientName);

    [LoggerMessage(
        EventName = nameof(CallingIntoCustomTokenValidator),
        Level = LogLevel.Debug,
        Message = "Calling into custom token validator: {Type}")]
    internal static partial void CallingIntoCustomTokenValidator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(StartAccessTokenValidation),
        Level = LogLevel.Trace,
        Message = "Start access token validation")]
    internal static partial void StartAccessTokenValidation(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ClientDeletedOrDisabled),
        Level = LogLevel.Information,
        Message = "Client deleted or disabled: {ClientId}")]
    internal static partial void ClientDeletedOrDisabled(this ILogger logger, object? clientId);

    [LoggerMessage(
        EventName = nameof(UserMarkedAsNotActive),
        Level = LogLevel.Information,
        Message = "User marked as not active: {Subject}")]
    internal static partial void UserMarkedAsNotActive(this ILogger logger, object? subject);

    [LoggerMessage(
        EventName = nameof(ServerSideSessionInvalidForSubjectIdAnd),
        Level = LogLevel.Information,
        Message = "Server-side session invalid for subject Id {SubjectId} and session Id {SessionId}.")]
    internal static partial void ServerSideSessionInvalidForSubjectIdAnd(this ILogger logger, object? subjectId, object? sessionId);

    [LoggerMessage(
        EventName = nameof(CallingIntoCustomTokenValidatorTokenValidator),
        Level = LogLevel.Debug,
        Message = "Calling into custom token validator: {Type}")]
    internal static partial void CallingIntoCustomTokenValidatorTokenValidator(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(JWTTokenValidationErrorTokenValidator),
        Level = LogLevel.Information,
        Message = "JWT token validation error: {Exception}")]
    internal static partial void JWTTokenValidationErrorTokenValidator(this ILogger logger, Exception ex, object? exception);

    [LoggerMessage(
        EventName = nameof(JWTTokenValidationErrorTokenValidator2),
        Level = LogLevel.Information,
        Message = "JWT token validation error: {Exception}")]
    internal static partial void JWTTokenValidationErrorTokenValidator2(this ILogger logger, Exception ex, object? exception);

    [LoggerMessage(
        EventName = nameof(MalformedJWTToken),
        Level = LogLevel.Information,
        Message = "Malformed JWT token: {Exception}")]
    internal static partial void MalformedJWTToken(this ILogger logger, Exception ex, object? exception);

    [LoggerMessage(
        EventName = nameof(LogMessageTokenValidator),
        Level = LogLevel.Information,
        Message = "{Message}:{@LogMessage}")]
    internal static partial void LogMessageTokenValidator(this ILogger logger, object? message, object? logMessage);

    [LoggerMessage(
        EventName = nameof(TokenValidationSuccess),
        Level = LogLevel.Debug,
        Message = "Token validation success:{@LogMessage}")]
    internal static partial void TokenValidationSuccess(this ILogger logger, object? logMessage);

    [LoggerMessage(
        EventName = nameof(TokenContainsNoSubClaim),
        Level = LogLevel.Error,
        Message = "Token contains no sub claim")]
    internal static partial void TokenContainsNoSubClaim(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(LoadingSubjectClaimsFromServerSideSessionStore),
        Level = LogLevel.Debug,
        Message = "Loading subject claims from server-side session store")]
    internal static partial void LoadingSubjectClaimsFromServerSideSessionStore(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(LoadingSubjectClaimsFromAccessToken),
        Level = LogLevel.Debug,
        Message = "Loading subject claims from access token")]
    internal static partial void LoadingSubjectClaimsFromAccessToken(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(UserIsNotActive),
        Level = LogLevel.Error,
        Message = "User is not active: {Sub}")]
    internal static partial void UserIsNotActive(this ILogger logger, object? sub);

    [LoggerMessage(
        EventName = nameof(X509NameSecretValidatorCannotProcess),
        Level = LogLevel.Debug,
        Message = "X509 name secret validator cannot process {Type}")]
    internal static partial void X509NameSecretValidatorCannotProcess(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(NoSubjectNameFoundInX509Certificate),
        Level = LogLevel.Warning,
        Message = "No subject/name found in X509 certificate.")]
    internal static partial void NoSubjectNameFoundInX509Certificate(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoX509NameSecretsConfiguredForClient),
        Level = LogLevel.Debug,
        Message = "No x509 name secrets configured for client.")]
    internal static partial void NoX509NameSecretsConfiguredForClient(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoMatchingX509NameSecretFound),
        Level = LogLevel.Debug,
        Message = "No matching x509 name secret found.")]
    internal static partial void NoMatchingX509NameSecretFound(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(X509ThumbprintSecretValidatorCannotProcess),
        Level = LogLevel.Debug,
        Message = "X509 thumbprint secret validator cannot process {Type}")]
    internal static partial void X509ThumbprintSecretValidatorCannotProcess(this ILogger logger, object? @type);

    [LoggerMessage(
        EventName = nameof(NoThumbprintFoundInX509Certificate),
        Level = LogLevel.Warning,
        Message = "No thumbprint found in X509 certificate.")]
    internal static partial void NoThumbprintFoundInX509Certificate(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoThumbprintSecretsConfiguredForClient),
        Level = LogLevel.Debug,
        Message = "No thumbprint secrets configured for client.")]
    internal static partial void NoThumbprintSecretsConfiguredForClient(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoMatchingX509ThumbprintSecretFound),
        Level = LogLevel.Debug,
        Message = "No matching x509 thumbprint secret found.")]
    internal static partial void NoMatchingX509ThumbprintSecretFound(this ILogger logger);
}
