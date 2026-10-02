// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Extensions;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(DeserializingAuthenticationTicketEnvelopeFoundIncorrectVersionForKey),
        Message = "Deserializing AuthenticationTicket envelope found incorrect version for key {Key}.")]
    internal static partial void DeserializingAuthenticationTicketEnvelopeFoundIncorrectVersionForKey(this ILogger logger, object key);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(FailedToUnprotectAuthenticationTicketPayloadForKeyKey),
        Message = "Failed to unprotect AuthenticationTicket payload for key {Key}")]
    internal static partial void FailedToUnprotectAuthenticationTicketPayloadForKeyKey(this ILogger logger, System.Exception exception, object key);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(FailedToDeserializeUserSessionPayloadForKeyKey),
        Message = "Failed to deserialize UserSession payload for key {Key}")]
    internal static partial void FailedToDeserializeUserSessionPayloadForKeyKey(this ILogger logger, System.Exception exception, object key);

    [LoggerMessage(
        LogLevel.Critical,
        EventName = nameof(ErrorCreatingTheJWTPayload),
        Message = "Error creating the JWT payload")]
    internal static partial void ErrorCreatingTheJWTPayload(this ILogger logger, System.Exception exception);
}
