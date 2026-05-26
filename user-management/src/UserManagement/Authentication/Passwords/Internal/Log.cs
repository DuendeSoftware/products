// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Passwords.Internal;

internal static partial class Log
{
    [LoggerMessage(Message = $"Password authentication attempt started for user {{{LogParameters.UserName}}}")]
    internal static partial void PasswordAuthenticationStarted(this ILogger logger, LogLevel level, UserName userName);

    [LoggerMessage(Message = $"Password authentication failed: user {{{LogParameters.UserName}}} not found")]
    internal static partial void PasswordAuthenticationUserNotFound(this ILogger logger, LogLevel level, UserName userName);

    [LoggerMessage(Message = $"Password authentication rejected for user {{{LogParameters.UserName}}}: attempt policy throttled the request")]
    internal static partial void PasswordAuthenticationThrottled(this ILogger logger, LogLevel level, UserName userName);

    [LoggerMessage(Message = $"Password authentication failed for user {{{LogParameters.UserName}}}: password verification failed")]
    internal static partial void PasswordAuthenticationFailed(this ILogger logger, LogLevel level, UserName userName);

    [LoggerMessage(Message = $"Password authentication succeeded for user {{{LogParameters.UserName}}}")]
    internal static partial void PasswordAuthenticationSucceeded(this ILogger logger, LogLevel level, UserName userName);

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
