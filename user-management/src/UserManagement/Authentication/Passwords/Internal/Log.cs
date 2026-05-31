// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Passwords.Internal;

internal static partial class Log
{
    [LoggerMessage(Message = $"Password authentication attempt started for user {{{LogParameters.AttributeCode}}}, {{{LogParameters.AttributeValue}}}")]
    internal static partial void PasswordAuthenticationStarted(this ILogger logger, LogLevel level, AttributeCode attributeCode, object attributeValue);

    [LoggerMessage(Message = $"Password authentication failed for user {{{LogParameters.AttributeCode}}}, {{{LogParameters.AttributeValue}}}: user profile not found")]
    internal static partial void PasswordAuthenticationUserProfileNotFound(this ILogger logger, LogLevel level, AttributeCode attributeCode, object attributeValue);

    [LoggerMessage(Message = $"Password authentication failed for user {{{LogParameters.AttributeCode}}}, {{{LogParameters.AttributeValue}}}: user authenticators not found")]
    internal static partial void PasswordAuthenticationUserAuthenticatorsNotFound(this ILogger logger, LogLevel level, AttributeCode attributeCode, object attributeValue);

    [LoggerMessage(Message = $"Password authentication rejected for user {{{LogParameters.AttributeCode}}}, {{{LogParameters.AttributeValue}}}: attempt policy throttled the request")]
    internal static partial void PasswordAuthenticationThrottled(this ILogger logger, LogLevel level, AttributeCode attributeCode, object attributeValue);

    [LoggerMessage(Message = $"Password authentication failed for user {{{LogParameters.AttributeCode}}}, {{{LogParameters.AttributeValue}}}: password verification failed")]
    internal static partial void PasswordAuthenticationFailed(this ILogger logger, LogLevel level, AttributeCode attributeCode, object attributeValue);

    [LoggerMessage(Message = $"Password authentication succeeded for user {{{LogParameters.AttributeCode}}}, {{{LogParameters.AttributeValue}}}")]
    internal static partial void PasswordAuthenticationSucceeded(this ILogger logger, LogLevel level, AttributeCode attributeCode, object attributeValue);

    [LoggerMessage(Message = $"Password re-hashed from algorithm '{{{Parameters.PreviousAlgorithmId}}}' to preferred algorithm '{{{Parameters.NewAlgorithmId}}}'")]
    internal static partial void PasswordRehashed(this ILogger logger, LogLevel level, string? previousAlgorithmId, string newAlgorithmId);

    [LoggerMessage(Message = "Authentication attempt update hit an optimistic concurrency conflict for password authentication; retrying once to record the failed attempt.")]
    internal static partial void OptimisticConcurrencyRetry(this ILogger logger, LogLevel level);

    private static class Parameters
    {
        internal const string PreviousAlgorithmId = nameof(PreviousAlgorithmId);
        internal const string NewAlgorithmId = nameof(NewAlgorithmId);
    }
}
