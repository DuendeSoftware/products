// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.AspNetIdentity;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(CredentialsValidated),
        Level = LogLevel.Information,
        Message = "Credentials validated for username: {Username}")]
    internal static partial void CredentialsValidated(this ILogger logger, string username);

    [LoggerMessage(
        EventName = nameof(AuthenticationFailedLockedOut),
        Level = LogLevel.Information,
        Message = "Authentication failed for username: {Username}, reason: locked out")]
    internal static partial void AuthenticationFailedLockedOut(this ILogger logger, string username);

    [LoggerMessage(
        EventName = nameof(AuthenticationFailedNotAllowed),
        Level = LogLevel.Information,
        Message = "Authentication failed for username: {Username}, reason: not allowed")]
    internal static partial void AuthenticationFailedNotAllowed(this ILogger logger, string username);

    [LoggerMessage(
        EventName = nameof(AuthenticationFailedInvalidCredentials),
        Level = LogLevel.Information,
        Message = "Authentication failed for username: {Username}, reason: invalid credentials")]
    internal static partial void AuthenticationFailedInvalidCredentials(this ILogger logger, string username);

    [LoggerMessage(
        EventName = nameof(UserNotFoundByUsername),
        Level = LogLevel.Information,
        Message = "No user found matching username: {Username}")]
    internal static partial void UserNotFoundByUsername(this ILogger logger, string username);

    [LoggerMessage(
        EventName = nameof(UserNotFoundBySubjectId),
        Level = LogLevel.Warning,
        Message = "No user found matching subject Id: {SubjectId}")]
    internal static partial void UserNotFoundBySubjectId(this ILogger logger, string subjectId);
}
