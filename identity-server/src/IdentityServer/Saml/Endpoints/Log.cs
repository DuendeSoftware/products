// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.Endpoints;

internal static class LogParameters
{
    public const string LogoutId = "LogoutId";
    public const string EntityId = "EntityId";
    public const string Issuer = "Issuer";
    public const string Status = "Status";
    public const string InResponseTo = "InResponseTo";
    public const string TrustLevel = "TrustLevel";
    public const string Received = "Received";
    public const string Total = "Total";
    public const string Succeeded = "Succeeded";
    public const string Skipped = "Skipped";
    public const string Outcome = "Outcome";
}

internal static partial class Log
{
    // SingleLogoutCallbackEndpoint

    [LoggerMessage(
        EventName = nameof(MissingLogoutIdParameter),
        Message = "Missing logoutId parameter in SAML logout callback")]
    internal static partial void MissingLogoutIdParameter(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(NoLogoutMessageFound),
        Message = $"No logout message found for logoutId: {{{LogParameters.LogoutId}}}")]
    internal static partial void NoLogoutMessageFound(this ILogger logger, LogLevel logLevel, string logoutId);

    [LoggerMessage(
        EventName = nameof(LogoutMessageMissingEntityId),
        Message = "Logout message does not contain SAML SP entity ID")]
    internal static partial void LogoutMessageMissingEntityId(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(LogoutMessageMissingRequestId),
        Message = "Logout message does not contain SAML logout request ID")]
    internal static partial void LogoutMessageMissingRequestId(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ServiceProviderNotFound),
        Level = LogLevel.Error,
        Message = $"Service provider not found: {{{LogParameters.EntityId}}}")]
    internal static partial void ServiceProviderNotFound(this ILogger logger, string entityId);

    [LoggerMessage(
        EventName = nameof(ServiceProviderDisabled),
        Level = LogLevel.Error,
        Message = $"Service provider is disabled: {{{LogParameters.EntityId}}}")]
    internal static partial void ServiceProviderDisabled(this ILogger logger, string entityId);

    [LoggerMessage(
        EventName = nameof(ServiceProviderHasNoSingleLogoutServiceUrl),
        Level = LogLevel.Error,
        Message = $"Service provider has no SingleLogoutServiceUrl: {{{LogParameters.EntityId}}}")]
    internal static partial void ServiceProviderHasNoSingleLogoutServiceUrl(this ILogger logger, string entityId);

    [LoggerMessage(
        EventName = nameof(NoLogoutSessionFound),
        Message = $"No logout session found for logoutId {{{LogParameters.LogoutId}}} — returning PartialLogout")]
    internal static partial void NoLogoutSessionFound(this ILogger logger, LogLevel logLevel, string logoutId);

    [LoggerMessage(
        EventName = nameof(LogoutSessionStatus),
        Message = $"Logout session for {{{LogParameters.LogoutId}}}: {{{LogParameters.Received}}}/{{{LogParameters.Total}}} SP responses received, {{{LogParameters.Succeeded}}} successful, {{{LogParameters.Skipped}}} skipped — returning {{{LogParameters.Outcome}}}")]
    internal static partial void LogoutSessionStatus(this ILogger logger, LogLevel logLevel, string logoutId, int received, int total, int succeeded, int skipped, string outcome);

    // SingleLogoutServiceEndpoint

    [LoggerMessage(
        EventName = nameof(SamlParsingError),
        Level = LogLevel.Error,
        Message = "SAML parsing error")]
    internal static partial void SamlParsingError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventName = nameof(FailedToParseSamlLogoutResponse),
        Level = LogLevel.Warning,
        Message = "Failed to parse SAML LogoutResponse")]
    internal static partial void FailedToParseSamlLogoutResponse(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventName = nameof(ReceivedSamlLogoutResponse),
        Message = $"Received SAML LogoutResponse from {{{LogParameters.Issuer}}} with status {{{LogParameters.Status}}} (InResponseTo: {{{LogParameters.InResponseTo}}})")]
    internal static partial void ReceivedSamlLogoutResponse(this ILogger logger, LogLevel logLevel, string? issuer, string? status, string? inResponseTo);

    [LoggerMessage(
        EventName = nameof(SpReportedNonSuccessLogoutStatus),
        Message = $"SP {{{LogParameters.Issuer}}} reported non-success logout status: {{{LogParameters.Status}}}")]
    internal static partial void SpReportedNonSuccessLogoutStatus(this ILogger logger, LogLevel logLevel, string? issuer, string? status);

    [LoggerMessage(
        EventName = nameof(SamlLogoutResponseMissingInResponseToOrIssuer),
        Message = "SAML LogoutResponse missing InResponseTo or Issuer — cannot record in session store")]
    internal static partial void SamlLogoutResponseMissingInResponseToOrIssuer(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(NoServiceProviderConfigurationFoundForIssuer),
        Message = $"No service provider configuration found for issuer {{{LogParameters.Issuer}}}. Falling back to global RequireSignedLogoutResponses default")]
    internal static partial void NoServiceProviderConfigurationFoundForIssuer(this ILogger logger, LogLevel logLevel, string issuer);

    [LoggerMessage(
        EventName = nameof(RejectingUnsignedSamlLogoutResponse),
        Message = $"Rejecting unsigned/untrusted SAML LogoutResponse from {{{LogParameters.Issuer}}} (TrustLevel: {{{LogParameters.TrustLevel}}}, required: TLS or higher). Set RequireSignedLogoutResponses = false on the SP to allow unsigned responses")]
    internal static partial void RejectingUnsignedSamlLogoutResponse(this ILogger logger, LogLevel logLevel, string issuer, TrustLevel trustLevel);

    [LoggerMessage(
        EventName = nameof(AcceptingUnsignedSamlLogoutResponse),
        Message = $"Accepting unsigned SAML LogoutResponse from {{{LogParameters.Issuer}}} (RequireSignedLogoutResponses is disabled for this SP)")]
    internal static partial void AcceptingUnsignedSamlLogoutResponse(this ILogger logger, LogLevel logLevel, string issuer);

    [LoggerMessage(
        EventName = nameof(FailedToRecordSamlLogoutResponse),
        Message = $"Failed to record SAML LogoutResponse for InResponseTo {{{LogParameters.InResponseTo}}} from {{{LogParameters.Issuer}}}. The request ID may not be tracked or the issuer may not match")]
    internal static partial void FailedToRecordSamlLogoutResponse(this ILogger logger, LogLevel logLevel, string inResponseTo, string issuer);
}
