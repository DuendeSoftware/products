// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(GrantTypeGrantWithValueKeyNotFoundIn),
        Message = "{GrantType} grant with value: {Key} not found in store.")]
    internal static partial void GrantTypeGrantWithValueKeyNotFoundIn(this ILogger logger, object grantType, object key);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(FailedToDeserializeJSONFromGrantStore),
        Message = "Failed to deserialize JSON from grant store.")]
    internal static partial void FailedToDeserializeJSONFromGrantStore(this ILogger logger, System.Exception exception);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(ExceptionReadingProtectedMessage),
        Message = "Exception reading protected message")]
    internal static partial void ExceptionReadingProtectedMessage(this ILogger logger, System.Exception exception);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ExceptionWritingProtectedMessage),
        Message = "Exception writing protected message")]
    internal static partial void ExceptionWritingProtectedMessage(this ILogger logger, System.Exception exception);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(CreatingEntryInStoreForAuthenticationTicketKeyKey),
        Message = "Creating entry in store for AuthenticationTicket, key {Key}, with expiration: {Expiration}")]
    internal static partial void CreatingEntryInStoreForAuthenticationTicketKeyKey(this ILogger logger, object key, object expiration);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RetrieveAuthenticationTicketForKeyKey),
        Message = "Retrieve AuthenticationTicket for key {Key}")]
    internal static partial void RetrieveAuthenticationTicketForKeyKey(this ILogger logger, object key);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoTicketFoundInStoreForKey),
        Message = "No ticket found in store for {Key}")]
    internal static partial void NoTicketFoundInStoreForKey(this ILogger logger, object key);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(TicketLoadedForKeyKeyWithExpirationExpiration),
        Message = "Ticket loaded for key: {Key}, with expiration: {Expiration}")]
    internal static partial void TicketLoadedForKeyKeyWithExpirationExpiration(this ILogger logger, object key, object expiration);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(FailedToDeserializeAuthenticationTicketFromStoreDeleting),
        Message = "Failed to deserialize authentication ticket from store, deleting record for key {Key}")]
    internal static partial void FailedToDeserializeAuthenticationTicketFromStoreDeleting(this ILogger logger, object key);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RenewingAuthenticationTicketForKeyKeyWithExpirationExpiration),
        Message = "Renewing AuthenticationTicket for key {Key}, with expiration: {Expiration}")]
    internal static partial void RenewingAuthenticationTicketForKeyKeyWithExpirationExpiration(this ILogger logger, object key, object expiration);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(SessionOverwriteDetectedForKeyKeyRevokingGrants),
        Message = "Session overwrite detected for key {Key}; revoking grants for prior subject id {SubjectId} and session id {SessionId}")]
    internal static partial void SessionOverwriteDetectedForKeyKeyRevokingGrants(this ILogger logger, object key, object subjectId, object sessionId);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RemovingAuthenticationTicketFromStoreForKeyKey),
        Message = "Removing AuthenticationTicket from store for key {Key}")]
    internal static partial void RemovingAuthenticationTicketFromStoreForKeyKey(this ILogger logger, object key);
}
