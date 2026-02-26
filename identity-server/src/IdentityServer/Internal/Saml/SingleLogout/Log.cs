// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

internal static class SingleLogoutLogParameters
{
    public const string RequestId = "requestId";
    public const string Issuer = "issuer";
    public const string SessionIndex = "sessionIndex";
    public const string Message = "message";
    public const string SpName = "spName";
    public const string StatusCode = "statusCode";
    public const string NotOnOrAfter = "notOnOrAfter";
    public const string ExpectedSessionIndex = "expectedSessionIndex";
    public const string ReceivedSessionIndex = "receivedSessionIndex";
    public const string EntityId = "entityId";
    public const string Version = "version";
    public const string IssueInstant = "issueInstant";
    public const string Destination = "destination";
    public const string ExpectedDestination = "expectedDestination";
    public const string Count = "count";
}

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(ParsedLogoutRequest),
        Message = $"Parsed LogoutRequest. ID: {{{SingleLogoutLogParameters.RequestId}}}, Issuer: {{{SingleLogoutLogParameters.Issuer}}}, SessionIndex: {{{SingleLogoutLogParameters.SessionIndex}}}"
        )]
    internal static partial void ParsedLogoutRequest(this ILogger logger, LogLevel logLevel, string requestId, string issuer, string sessionIndex);

    [LoggerMessage(
        EventName = nameof(FailedToParseLogoutRequest),
        Level = LogLevel.Error,
        Message = $"Failed to parse LogoutRequest: {{{SingleLogoutLogParameters.Message}}}")]
    internal static partial void FailedToParseLogoutRequest(this ILogger logger, Exception exception, string message);

    [LoggerMessage(
        EventName = nameof(UnexpectedErrorParsingLogoutRequest),
        Level = LogLevel.Error,
        Message = "Unexpected error parsing LogoutRequest")]
    internal static partial void UnexpectedErrorParsingLogoutRequest(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = nameof(ReceivedLogoutRequest),
        Message = $"Received SAML LogoutRequest from {{{SingleLogoutLogParameters.Issuer}}}. RequestId: {{{SingleLogoutLogParameters.RequestId}}}, SessionIndex: {{{SingleLogoutLogParameters.SessionIndex}}}")]
    internal static partial void ReceivedLogoutRequest(this ILogger logger, LogLevel logLevel, string issuer, string requestId, string sessionIndex);

    [LoggerMessage(
        EventName = nameof(SuccessfullyProcessedLogoutRequest),
        Message = $"Logout request {{{SingleLogoutLogParameters.RequestId}}} with session index {{{SingleLogoutLogParameters.SessionIndex}}} processed successfully")]
    internal static partial void SuccessfullyProcessedLogoutRequest(this ILogger logger, LogLevel logLevel, string requestId, string sessionIndex);

    [LoggerMessage(
        EventName = nameof(SamlLogoutValidationError),
        Message = $"SAML logout validation error:  {{{SingleLogoutLogParameters.Message}}}")]
    internal static partial void SamlLogoutValidationError(this ILogger logger, LogLevel logLevel, string message);

    [LoggerMessage(
        EventName = nameof(SamlLogoutProtocolError),
        Message = $"SAML logout protocol error: {{{SingleLogoutLogParameters.StatusCode}}} - {{{SingleLogoutLogParameters.Message}}}")]
    internal static partial void SamlLogoutProtocolError(this ILogger logger, LogLevel logLevel, string statusCode, string message);

    [LoggerMessage(
        EventName = nameof(SamlLogoutRequestFromUnknownOrDisabledServiceProvider),
        Message = $"LogoutRequest from unknown or disabled SP: {{{SingleLogoutLogParameters.Issuer}}}")]
    internal static partial void SamlLogoutRequestFromUnknownOrDisabledServiceProvider(this ILogger logger, LogLevel logLevel, string issuer);

    [LoggerMessage(
        EventName = nameof(ProcessingSamlLogoutRequest),
        Message = $"Processing LogoutRequest {{{SingleLogoutLogParameters.RequestId}}} from SP: {{{SingleLogoutLogParameters.SpName}}} ({{{SingleLogoutLogParameters.Issuer}}})")]
    internal static partial void ProcessingSamlLogoutRequest(this ILogger logger, LogLevel logLevel, string requestId, string spName, string issuer);

    [LoggerMessage(
        EventName = nameof(SamlLogoutRequestReceivedButNoActiveUserSession),
        Message = $"LogoutRequest {{{SingleLogoutLogParameters.RequestId}}} received from {{{SingleLogoutLogParameters.Issuer}}} but no active user session found")]
    internal static partial void SamlLogoutRequestReceivedButNoActiveUserSession(this ILogger logger, LogLevel logLevel, string requestId, string issuer);

    [LoggerMessage(
        EventName = nameof(SamlLogoutRequestReceivedWithWrongSessionIndex),
        Message = $"SessionIndex mismatch. Request: {{{SingleLogoutLogParameters.RequestId}}}, SessionIndex: {{{SingleLogoutLogParameters.SessionIndex}}}")]
    internal static partial void SamlLogoutRequestReceivedWithWrongSessionIndex(this ILogger logger, LogLevel logLevel, string requestId, string sessionIndex);

    [LoggerMessage(
        EventName = nameof(SamlLogoutRedirectToLogoutPage),
        Message = $"Redirecting SAML logout to host logout page {{{SingleLogoutLogParameters.Issuer}}}")]
    internal static partial void SamlLogoutRedirectToLogoutPage(this ILogger logger, LogLevel logLevel, string issuer);

    [LoggerMessage(
        EventName = nameof(SamlLogoutNoCertificatesForSignatureValidation),
        Message = $"SP {{{SingleLogoutLogParameters.EntityId}}} has no signing certificates configured. LogoutRequest requires signature authentication")]
    internal static partial void SamlLogoutNoCertificatesForSignatureValidation(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(SamlLogoutRequestExpired),
        Message = $"LogoutRequest {{{SingleLogoutLogParameters.RequestId}}} expired. NotOnOrAfter: {{{SingleLogoutLogParameters.NotOnOrAfter}}}")]
    internal static partial void SamlLogoutRequestExpired(this ILogger logger, LogLevel logLevel, string requestId, DateTime notOnOrAfter);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSignatureValidationFailed),
        Message = $"LogoutRequest signature validation failed for SP {{{SingleLogoutLogParameters.EntityId}}}: {{{SingleLogoutLogParameters.Message}}}")]
    internal static partial void SamlLogoutSignatureValidationFailed(this ILogger logger, LogLevel logLevel, string entityId, string message);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSignatureValidationSucceeded),
        Message = "LogoutRequest signature validated successfully")]
    internal static partial void SamlLogoutSignatureValidationSucceeded(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(SamlLogoutNoSessionFoundForServiceProvider),
        Message = $"No session with session index {{{SingleLogoutLogParameters.SessionIndex}}} found for SP {{{SingleLogoutLogParameters.Issuer}}}")]
    internal static partial void SamlLogoutNoSessionFoundForServiceProvider(this ILogger logger, LogLevel logLevel, string sessionIndex, string issuer);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionIndexMisMatch),
        Message = $"SessionIndex mismatch. Expected: {{{SingleLogoutLogParameters.ExpectedSessionIndex}}}, Received: {{{SingleLogoutLogParameters.ReceivedSessionIndex}}}")]
    internal static partial void SamlLogoutSessionIndexMisMatch(this ILogger logger, LogLevel logLevel, string expectedSessionIndex, string receivedSessionIndex);

    [LoggerMessage(
        EventName = nameof(SamlLogoutNoSingleLogoutServiceUrl),
        Message = $"SP {{{SingleLogoutLogParameters.EntityId}}} has no SingleLogoutServiceUrl configured. Cannot send LogoutResponse")]
    internal static partial void SamlLogoutNoSingleLogoutServiceUrl(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(SamlLogoutUnsupportedVersion),
        Message = $"LogoutRequest has unsupported SAML version: {{{SingleLogoutLogParameters.Version}}}. Only 2.0 is supported")]
    internal static partial void SamlLogoutUnsupportedVersion(this ILogger logger, LogLevel logLevel, string version);

    [LoggerMessage(
        EventName = nameof(SamlLogoutRequestIssueInstantInFuture),
        Message = $"LogoutRequest {{{SingleLogoutLogParameters.RequestId}}} has IssueInstant in the future: {{{SingleLogoutLogParameters.IssueInstant}}}")]
    internal static partial void SamlLogoutRequestIssueInstantInFuture(this ILogger logger, LogLevel logLevel, string requestId, DateTime issueInstant);

    [LoggerMessage(
        EventName = nameof(SamlLogoutRequestIssueInstantTooOld),
        Message = $"LogoutRequest {{{SingleLogoutLogParameters.RequestId}}} has IssueInstant too old (expired): {{{SingleLogoutLogParameters.IssueInstant}}}")]
    internal static partial void SamlLogoutRequestIssueInstantTooOld(this ILogger logger, LogLevel logLevel, string requestId, DateTime issueInstant);

    [LoggerMessage(
        EventName = nameof(SamlLogoutRequestInvalidDestination),
        Message = $"LogoutRequest {{{SingleLogoutLogParameters.RequestId}}} has invalid Destination. Received: {{{SingleLogoutLogParameters.Destination}}}, Expected: {{{SingleLogoutLogParameters.ExpectedDestination}}}")]
    internal static partial void SamlLogoutRequestInvalidDestination(this ILogger logger, LogLevel logLevel, string requestId, Uri destination, string expectedDestination);

    [LoggerMessage(
        EventName = nameof(ProcessingSamlLogoutCallback),
        Message = "Processing SAML logout callback")]
    internal static partial void ProcessingSamlLogoutCallback(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(MissingLogoutId),
        Message = "Missing logoutId parameter in callback request")]
    internal static partial void MissingLogoutId(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(InvalidLogoutId),
        Message = "Invalid logoutId in callback request: {logoutId}")]
    internal static partial void InvalidLogoutId(this ILogger logger, LogLevel logLevel, string logoutId);

    [LoggerMessage(
        EventName = nameof(NotSamlInitiatedLogout),
        Message = "Logout callback was not for a SAML logout")]
    internal static partial void NotSamlInitiatedLogout(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ServiceProviderNotFound),
        Message = $"Service Provider not found for EntityId: {{{SingleLogoutLogParameters.EntityId}}}")]
    internal static partial void ServiceProviderNotFound(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(ReturningLogoutResponseToSp),
        Message = $"Returning LogoutResponse to Service Provider: {{{SingleLogoutLogParameters.EntityId}}}")]
    internal static partial void ReturningLogoutResponseToSp(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(NoSamlServiceProvidersToNotifyForLogout),
        Message = "No SAML Service Providers to notify for logout")]
    internal static partial void NoSamlServiceProvidersToNotifyForLogout(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(SkippingLogoutUrlGenerationForUnknownOrDisabledServiceProvider),
        Message = $"Skipping SAML logout for disabled or unknown SP: {{{SingleLogoutLogParameters.EntityId}}}")]
    internal static partial void SkippingLogoutUrlGenerationForUnknownOrDisabledServiceProvider(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(SkippingLogoutUrlGenerationForServiceProviderWithNoSingleLogout),
        Message = $"Skipping SAML logout for SP without any SingleLogoutServiceUrl: {{{SingleLogoutLogParameters.EntityId}}}")]
    internal static partial void SkippingLogoutUrlGenerationForServiceProviderWithNoSingleLogout(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(NoSessionDataFoundForLogoutUrlGenerationForServiceProvider),
        Message = $"No session data found for SP: {{{SingleLogoutLogParameters.EntityId}}}")]
    internal static partial void NoSessionDataFoundForLogoutUrlGenerationForServiceProvider(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(FailedToGenerateLogoutUrlForServiceProvider),
        Level = LogLevel.Error,
        Message = $"Failed to build SAML logout URL for SP: {{{SingleLogoutLogParameters.EntityId}}}")]
    internal static partial void FailedToGenerateLogoutUrlForServiceProvider(this ILogger logger, Exception ex, string entityId);

    [LoggerMessage(
        EventName = nameof(GeneratedSamlFrontChannelLogoutUrls),
        Message = $"Generated {{{SingleLogoutLogParameters.Count}}} SAML front-channel logout URLs")]
    internal static partial void GeneratedSamlFrontChannelLogoutUrls(this ILogger logger, LogLevel logLevel, int count);

    [LoggerMessage(
        EventName = nameof(NoSamlFrontChannelLogoutUrlsGenerated),
        Message = "No SAML front-channel logout URLs generated")]
    internal static partial void NoSamlFrontChannelLogoutUrlsGenerated(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(NoLogoutMessageFound),
        Message = $"No logout message found for logoutId: {{logoutId}}")]
    internal static partial void NoLogoutMessageFound(this ILogger logger, LogLevel logLevel, string logoutId);

    [LoggerMessage(
        EventName = nameof(LogoutMessageMissingSamlEntityId),
        Message = "Logout message does not contain SAML SP entity ID")]
    internal static partial void LogoutMessageMissingSamlEntityId(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(BuildingLogoutResponseForSp),
        Message = $"Building SAML logout response for SP: {{{SingleLogoutLogParameters.EntityId}}}")]
    internal static partial void BuildingLogoutResponseForSp(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(ServiceProviderDisabled),
        Message = $"Service Provider is disabled: {{{SingleLogoutLogParameters.EntityId}}}")]
    internal static partial void ServiceProviderDisabled(this ILogger logger, LogLevel logLevel, string entityId);

    [LoggerMessage(
        EventName = nameof(LogoutMessageMissingRequestId),
        Message = "Logout message does not contain SAML logout request ID")]
    internal static partial void LogoutMessageMissingRequestId(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(SuccessfullyBuiltLogoutResponse),
        Message = $"Successfully built SAML logout response for SP: {{{SingleLogoutLogParameters.EntityId}}}, InResponseTo: {{{SingleLogoutLogParameters.RequestId}}}")]
    internal static partial void SuccessfullyBuiltLogoutResponse(this ILogger logger, LogLevel logLevel, string entityId, string requestId);

    [LoggerMessage(
        EventName = nameof(InvalidHttpMethodForLogoutCallback),
        Message = "Invalid HTTP method for SAML logout callback endpoint")]
    internal static partial void InvalidHttpMethodForLogoutCallback(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ProcessingSamlLogoutCallbackRequest),
        Message = "Processing SAML logout callback request")]
    internal static partial void ProcessingSamlLogoutCallbackRequest(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(MissingLogoutIdParameter),
        Message = "Missing logoutId parameter in SAML logout callback")]
    internal static partial void MissingLogoutIdParameter(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ErrorProcessingLogoutCallback),
        Message = $"Error processing SAML logout callback: {{{SingleLogoutLogParameters.Message}}}")]
    internal static partial void ErrorProcessingLogoutCallback(this ILogger logger, LogLevel logLevel, string message);

    [LoggerMessage(
        EventName = nameof(SuccessfullyProcessedLogoutCallback),
        Message = "Successfully processed SAML logout callback")]
    internal static partial void SuccessfullyProcessedLogoutCallback(this ILogger logger, LogLevel logLevel);
}

