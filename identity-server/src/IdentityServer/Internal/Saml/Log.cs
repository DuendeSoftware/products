// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Internal.Saml;

internal static class SamlLogParameters
{
    internal const string RedirectUrl = "redirectUrl";
    internal const string EntityId = "entityId";
    internal const string SamlSigningBehavior = "samlSigningBehavior";
    internal const string ErrorMessage = "errorMessage";
    internal const string SecurityKey = "securityKey";
    internal const string RequestedAuthnContextRequirementsWereMet = "requestedAuthnContextRequirementsWereMet";
    internal const string ClaimCount = "claimCount";
    internal const string AttributeCount = "attributeCount";
    internal const string MessageType = "messageType";
}

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(Redirecting),
        Message = $"Redirecting to {{{SamlLogParameters.RedirectUrl}}}"
    )]
    internal static partial void Redirecting(this ILogger logger, LogLevel level, Uri redirectUrl);

    [LoggerMessage(
        EventName = nameof(StartSamlSigninRequest),
        Message = $"Starting Saml Signin request"
    )]
    internal static partial void StartSamlSigninRequest(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(StartSamlSigninCallbackRequest),
        Message = $"Starting Saml Signin Callback request"
    )]
    internal static partial void StartSamlSigninCallbackRequest(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(SamlInteractionPassiveAndForced),
        Message = $"AuthN request asks for both passive and forced. This is not supported, so returning 'nopassive'"
    )]
    internal static partial void SamlInteractionPassiveAndForced(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(SamlInteractionForced),
        Message = $"AuthN request asks for forced. User is already authenticated, so signing out user and triggering new login."
    )]
    internal static partial void SamlInteractionForced(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(SamlInteractionAlreadyAuthenticated),
        Message = $"AuthN request asked for Passive. User is already authenticated, so triggering callback."
    )]
    internal static partial void SamlInteractionAlreadyAuthenticated(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(SamlInteractionNoPassive),
        Message = $"AuthN request asks for passive. User is not authenticated, so returning error 'NoPassive'"
    )]
    internal static partial void SamlInteractionNoPassive(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(SamlInteractionConsent),
        Message = $"ServiceProvider is configured to require consent. The AuthN request indicates that consent hasn't already been provided, so triggering consent screen."
    )]
    internal static partial void SamlInteractionConsent(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(SamlInteractionLogin),
        Message = $"AuthN request asks for login. User is not authenticated, so triggering login."
    )]
    internal static partial void SamlInteractionLogin(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(SigningDisabledForServiceProvider),
        Message = $"Signing disabled for SP {{{SamlLogParameters.EntityId}}}")]
    internal static partial void SigningDisabledForServiceProvider(this ILogger logger, LogLevel level, string entityId);

    [LoggerMessage(
        EventName = nameof(SigningSamlResponse),
        Message = $"Signing SAML message for SP {{{SamlLogParameters.EntityId}}} with signing behavior {{{SamlLogParameters.SamlSigningBehavior}}}")]
    internal static partial void SigningSamlResponse(this ILogger logger, LogLevel level, string entityId, SamlSigningBehavior samlSigningBehavior);

    [LoggerMessage(
        EventName = nameof(SuccessfullySignedSamlResponse),
        Message = $"Successfully signed SAML message for SP {{{SamlLogParameters.EntityId}}}")]
    internal static partial void SuccessfullySignedSamlResponse(this ILogger logger, LogLevel level, string entityId);

    [LoggerMessage(
        EventName = nameof(FailedToSignSamlResponse),
        Level = LogLevel.Error,
        Message = $"Failed to sign SAML Response for SP {{{SamlLogParameters.EntityId}}}: {{{SamlLogParameters.ErrorMessage}}}")]
    internal static partial void FailedToSignSamlResponse(this ILogger logger, Exception ex, string entityId, string errorMessage);

    [LoggerMessage(
        EventName = nameof(SigningCredentialIsNotX509Certificate),
        Message = $"Signing credential is not an X509 certificate (Key: {{{SamlLogParameters.SecurityKey}}}). SAML signing requires X509 certificates with private keys.")]
    internal static partial void SigningCredentialIsNotX509Certificate(this ILogger logger, LogLevel level, SecurityKey securityKey);

    [LoggerMessage(
        EventName = nameof(StateNotFound),
        Message = "SAML signin state not found for state ID {StateId}")]
    internal static partial void StateNotFound(this ILogger logger, LogLevel level, StateId stateId);

    [LoggerMessage(
        EventName = nameof(ServiceProviderNotFound),
        Message = $"Service Provider {{{SamlLogParameters.EntityId}}} not found")]
    internal static partial void ServiceProviderNotFound(this ILogger logger, LogLevel level, string entityId);

    [LoggerMessage(
        EventName = nameof(NoSamlAuthenticationStateFound),
        Message = "Cannot load SAML authentication state.")]
    internal static partial void NoSamlAuthenticationStateFound(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(AuthenticationStateLoaded),
        Message = $"SAML authentication request context loaded for SP {{{SamlLogParameters.EntityId}}}")]
    internal static partial void AuthenticationStateLoaded(this ILogger logger, LogLevel level, string entityId);

    [LoggerMessage(
        EventName = nameof(RequestedAuthnContextRequirementsWereMetUpdatedInState),
        Message = $"Stored requestedAuthnContextRequirementsWereMet for SAML request: {{{SamlLogParameters.RequestedAuthnContextRequirementsWereMet}}}")]
    internal static partial void RequestedAuthnContextRequirementsWereMetUpdatedInState(this ILogger logger,
        LogLevel level, bool requestedAuthnContextRequirementsWereMet);

    [LoggerMessage(
        EventName = nameof(StartIdpInitiatedRequest),
        Message = "Starting IdP-initiated SAML request for SP '{serviceProviderEntityId}'")]
    internal static partial void StartIdpInitiatedRequest(this ILogger logger, LogLevel level, string serviceProviderEntityId);

    [LoggerMessage(
        EventName = nameof(IdpInitiatedRequestFailed),
        Message = "IdP-initiated SAML request failed: {ErrorMessage}")]
    internal static partial void IdpInitiatedRequestFailed(this ILogger logger, LogLevel level, string errorMessage);

    [LoggerMessage(
        EventName = nameof(IdpInitiatedRequestSuccess),
        Message = "IdP-initiated SAML request succeeded, redirecting to {RedirectUrl}")]
    internal static partial void IdpInitiatedRequestSuccess(this ILogger logger, LogLevel level, Uri redirectUrl);

    [LoggerMessage(
        EventName = nameof(RetrievedClaimsFromProfileService),
        Message = $"Retrieved {{{SamlLogParameters.ClaimCount}}} claims from profile service")]
    internal static partial void RetrievedClaimsFromProfileService(this ILogger logger, LogLevel level, int claimCount);

    [LoggerMessage(
        EventName = nameof(UsingCustomClaimMapper),
        Message = $"Using custom claim mapper for SP {{{SamlLogParameters.EntityId}}}")]
    internal static partial void UsingCustomClaimMapper(this ILogger logger, LogLevel level, string entityId);

    [LoggerMessage(
        EventName = nameof(MappedClaimsToAttributes),
        Message = $"Mapped {{{SamlLogParameters.ClaimCount}}} claims to {{{SamlLogParameters.AttributeCount}}} SAML attributes for SP {{{SamlLogParameters.EntityId}}}")]
    internal static partial void MappedClaimsToAttributes(this ILogger logger, LogLevel level, int claimCount, int attributeCount, string entityId);

    [LoggerMessage(
        EventName = nameof(SigningSamlProtocolMessage),
        Message = $"Signing SAML protocol message ({{{SamlLogParameters.MessageType}}}) for SP {{{SamlLogParameters.EntityId}}}")]
    internal static partial void SigningSamlProtocolMessage(this ILogger logger, LogLevel level, string entityId, string messageType);

    [LoggerMessage(
        EventName = nameof(SuccessfullySignedSamlProtocolMessage),
        Message = $"Successfully signed SAML protocol message ({{{SamlLogParameters.MessageType}}}) for SP {{{SamlLogParameters.EntityId}}}")]
    internal static partial void SuccessfullySignedSamlProtocolMessage(this ILogger logger, LogLevel level, string entityId, string messageType);

    [LoggerMessage(
        EventName = nameof(FailedToSignSamlProtocolMessage),
        Level = LogLevel.Error,
        Message = $"Failed to sign SAML protocol message ({{{SamlLogParameters.MessageType}}}) for SP {{{SamlLogParameters.EntityId}}}: {{{SamlLogParameters.ErrorMessage}}}")]
    internal static partial void FailedToSignSamlProtocolMessage(this ILogger logger, Exception ex, string entityId, string messageType, string errorMessage);

    [LoggerMessage(
        EventName = nameof(SamlSigninSuccess),
        Message = $"SAML signin request processed successfully, redirecting to {{{SamlLogParameters.RedirectUrl}}}")]
    internal static partial void SamlSigninSuccess(this ILogger logger, LogLevel level, Uri redirectUrl);

    [LoggerMessage(
        EventName = nameof(SamlSigninValidationError),
        Message = $"SAML signin validation error: {{{SamlLogParameters.ErrorMessage}}}")]
    internal static partial void SamlSigninValidationError(this ILogger logger, LogLevel level, string errorMessage);

    [LoggerMessage(
        EventName = nameof(SamlSigninProtocolError),
        Message = $"SAML signin protocol error: {{statusCode}} - {{{SamlLogParameters.ErrorMessage}}}")]
    internal static partial void SamlSigninProtocolError(this ILogger logger, LogLevel level, string statusCode, string errorMessage);

    [LoggerMessage(
        EventName = nameof(SamlRequestContainedInvalidXml),
        Level = LogLevel.Warning,
        Message = $"SAML request contained invalid XML: {{{SamlLogParameters.ErrorMessage}}}")]
    internal static partial void SamlRequestContainedInvalidXml(this ILogger logger, Exception exception, string errorMessage);
}
