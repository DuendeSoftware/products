// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.Validation;

internal static class LogParameters
{
    public const string SpEntityId = "SpEntityId";
    public const string TrustLevel = "TrustLevel";
    public const string ExpectedDestination = "ExpectedDestination";
    public const string ActualDestination = "ActualDestination";
    public const string RequestedAcsUrl = "RequestedAcsUrl";
    public const string Format = "Format";
    public const string NameId = "NameId";
    public const string SessionIndex = "SessionIndex";
}

internal static partial class Log
{
    // AuthnRequestValidator

    [LoggerMessage(
        EventName = nameof(AuthnRequestSignatureTrustCheckFailed),
        Message = $"AuthnRequest from SP {{{LogParameters.SpEntityId}}} failed signature trust check. TrustLevel: {{{LogParameters.TrustLevel}}}")]
    internal static partial void AuthnRequestSignatureTrustCheckFailed(this ILogger logger, LogLevel logLevel, string spEntityId, TrustLevel trustLevel);

    [LoggerMessage(
        EventName = nameof(AuthnRequestDestinationMismatch),
        Message = $"AuthnRequest Destination mismatch. Expected: {{{LogParameters.ExpectedDestination}}}, Actual: {{{LogParameters.ActualDestination}}}")]
    internal static partial void AuthnRequestDestinationMismatch(this ILogger logger, LogLevel logLevel, string expectedDestination, string actualDestination);

    [LoggerMessage(
        EventName = nameof(AssertionConsumerServiceUrlNotRegistered),
        Message = $"AssertionConsumerServiceUrl is not registered for SP {{{LogParameters.SpEntityId}}}. Requested: {{{LogParameters.RequestedAcsUrl}}}")]
    internal static partial void AssertionConsumerServiceUrlNotRegistered(this ILogger logger, LogLevel logLevel, string spEntityId, string requestedAcsUrl);

    [LoggerMessage(
        EventName = nameof(AuthnRequestUnsupportedNameIdFormat),
        Message = $"AuthnRequest from SP {{{LogParameters.SpEntityId}}} requested unsupported NameID format '{{{LogParameters.Format}}}'.")]
    internal static partial void AuthnRequestUnsupportedNameIdFormat(this ILogger logger, LogLevel logLevel, string? spEntityId, string format);

    [LoggerMessage(
        EventName = nameof(AuthnRequestContainsScopingElement),
        Message = $"AuthnRequest from SP {{{LogParameters.SpEntityId}}} contains a Scoping element, which is not supported.")]
    internal static partial void AuthnRequestContainsScopingElement(this ILogger logger, LogLevel logLevel, string? spEntityId);

    // LogoutRequestValidator

    [LoggerMessage(
        EventName = nameof(LogoutRequestSignatureTrustCheckFailed),
        Message = $"LogoutRequest from SP {{{LogParameters.SpEntityId}}} failed signature trust check. TrustLevel: {{{LogParameters.TrustLevel}}}")]
    internal static partial void LogoutRequestSignatureTrustCheckFailed(this ILogger logger, LogLevel logLevel, string? spEntityId, TrustLevel trustLevel);

    [LoggerMessage(
        EventName = nameof(LogoutRequestDestinationMismatch),
        Message = $"LogoutRequest Destination mismatch. Expected: {{{LogParameters.ExpectedDestination}}}, Actual: {{{LogParameters.ActualDestination}}}")]
    internal static partial void LogoutRequestDestinationMismatch(this ILogger logger, LogLevel logLevel, string expectedDestination, string actualDestination);

    [LoggerMessage(
        EventName = nameof(LogoutRequestNoActiveSessionFound),
        Message = $"LogoutRequest from SP {{{LogParameters.SpEntityId}}} but no active SAML session found — user is already logged out")]
    internal static partial void LogoutRequestNoActiveSessionFound(this ILogger logger, LogLevel logLevel, string? spEntityId);

    [LoggerMessage(
        EventName = nameof(LogoutRequestNameIdMismatch),
        Message = $"LogoutRequest NameID {{{LogParameters.NameId}}} does not match any active session for SP {{{LogParameters.SpEntityId}}}")]
    internal static partial void LogoutRequestNameIdMismatch(this ILogger logger, LogLevel logLevel, string nameId, string? spEntityId);

    [LoggerMessage(
        EventName = nameof(LogoutRequestSessionIndexMismatch),
        Message = $"LogoutRequest SessionIndex {{{LogParameters.SessionIndex}}} does not match any active session for SP {{{LogParameters.SpEntityId}}} — treating as already logged out")]
    internal static partial void LogoutRequestSessionIndexMismatch(this ILogger logger, LogLevel logLevel, string sessionIndex, string? spEntityId);
}
