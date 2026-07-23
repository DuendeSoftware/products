// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.SamlLogoutSession;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionStoreFailed),
        Message = $"Failed to store SAML logout session {{{LogParameters.LogoutId}}}: {{{LogParameters.Result}}}")]
    internal static partial void SamlLogoutSessionStoreFailed(this ILogger logger, LogLevel logLevel, string logoutId, string result);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionStored),
        Message = $"Stored SAML logout session {{{LogParameters.LogoutId}}}")]
    internal static partial void SamlLogoutSessionStored(this ILogger logger, LogLevel logLevel, string logoutId);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionNotFound),
        Message = $"SAML logout session {{{LogParameters.LogoutId}}} not found")]
    internal static partial void SamlLogoutSessionNotFound(this ILogger logger, LogLevel logLevel, string logoutId);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionExpired),
        Message = $"SAML logout session {{{LogParameters.LogoutId}}} has expired")]
    internal static partial void SamlLogoutSessionExpired(this ILogger logger, LogLevel logLevel, string logoutId);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionDeserializationFailed),
        Message = $"Failed to deserialize SAML logout session {{{LogParameters.LogoutId}}}")]
    internal static partial void SamlLogoutSessionDeserializationFailed(this ILogger logger, LogLevel logLevel, string logoutId);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionDeserializationFailedForRequestId),
        Message = $"Failed to deserialize SAML logout session for requestId {{{LogParameters.RequestId}}}")]
    internal static partial void SamlLogoutSessionDeserializationFailedForRequestId(this ILogger logger, LogLevel logLevel, string requestId);

    [LoggerMessage(
        EventName = nameof(SamlLogoutResponseConcurrencyRetry),
        Message = $"Concurrency conflict recording SAML logout response for requestId {{{LogParameters.RequestId}}}, retrying (attempt {{{LogParameters.Attempt}}})")]
    internal static partial void SamlLogoutResponseConcurrencyRetry(this ILogger logger, LogLevel logLevel, string requestId, int attempt);

    [LoggerMessage(
        EventName = nameof(SamlLogoutResponseConcurrencyExhausted),
        Message = $"Failed to record SAML logout response for requestId {{{LogParameters.RequestId}}} after {{{LogParameters.MaxRetries}}} attempts due to concurrency conflicts")]
    internal static partial void SamlLogoutResponseConcurrencyExhausted(this ILogger logger, LogLevel logLevel, string requestId, int maxRetries);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionRemoved),
        Message = $"Removed SAML logout session {{{LogParameters.LogoutId}}}")]
    internal static partial void SamlLogoutSessionRemoved(this ILogger logger, LogLevel logLevel, string logoutId);

    [LoggerMessage(
        EventName = nameof(SamlLogoutSessionKeyConflict),
        Message = $"Key conflict updating SAML logout session for requestId {{{LogParameters.RequestId}}} — possible duplicate requestId across sessions")]
    internal static partial void SamlLogoutSessionKeyConflict(this ILogger logger, LogLevel logLevel, string requestId);

    [LoggerMessage(
        EventName = nameof(SamlLogoutResponseIssuerMismatch),
        Message = $"SAML logout response issuer mismatch for requestId {{{LogParameters.RequestId}}}. Expected {{{LogParameters.ExpectedIssuer}}}, received {{{LogParameters.ActualIssuer}}}")]
    internal static partial void SamlLogoutResponseIssuerMismatch(this ILogger logger, LogLevel logLevel, string requestId, string expectedIssuer, string actualIssuer);
}

internal static class LogParameters
{
    public const string LogoutId = "LogoutId";
    public const string RequestId = "RequestId";
    public const string Result = "Result";
    public const string Attempt = "Attempt";
    public const string MaxRetries = "MaxRetries";
    public const string ExpectedIssuer = "ExpectedIssuer";
    public const string ActualIssuer = "ActualIssuer";
}
