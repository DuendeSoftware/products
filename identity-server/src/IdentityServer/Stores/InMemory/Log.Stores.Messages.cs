// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(SAMLLogoutSessionStoreHasReachedTheMaximum),
        Message = "SAML logout session store has reached the maximum capacity of {MaxEntries}. " +
                "Rejecting new session for logoutId {LogoutId}")]
    internal static partial void SAMLLogoutSessionStoreHasReachedTheMaximum(this ILogger logger, object maxEntries, object logoutId);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(SAMLLogoutResponseIssuerMismatchForRequestIdRequestId),
        Message = "SAML logout response issuer mismatch for requestId {RequestId}. " +
                    "Expected {ExpectedIssuer}, received {ActualIssuer}")]
    internal static partial void SAMLLogoutResponseIssuerMismatchForRequestIdRequestId(this ILogger logger, object requestId, object expectedIssuer, object actualIssuer);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(SAMLSigninStateStateIdNotFoundForUpdate),
        Message = "SAML signin state {StateId} not found for update")]
    internal static partial void SAMLSigninStateStateIdNotFoundForUpdate(this ILogger logger, object stateId);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(SAMLSigninStateStateIdExpiredCannotUpdate),
        Message = "SAML signin state {StateId} expired, cannot update")]
    internal static partial void SAMLSigninStateStateIdExpiredCannotUpdate(this ILogger logger, object stateId);
}
