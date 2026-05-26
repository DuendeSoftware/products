// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.RecoveryCodes.Internal;

internal static partial class Log
{
    [LoggerMessage(Message = $"Recovery code authentication attempt started for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void RecoveryCodeAuthenticationStarted(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Recovery code authentication failed: user {{{LogParameters.SubjectId}}} not found")]
    internal static partial void RecoveryCodeAuthenticationUserNotFound(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Recovery code authentication rejected for subject {{{LogParameters.SubjectId}}}: attempt policy throttled the request")]
    internal static partial void RecoveryCodeAuthenticationThrottled(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Recovery code authentication failed for subject {{{LogParameters.SubjectId}}}: recovery code verification failed")]
    internal static partial void RecoveryCodeAuthenticationFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Recovery code authentication succeeded for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void RecoveryCodeAuthenticationSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = "Authentication attempt update hit an optimistic concurrency conflict for recovery-code authentication; retrying once to record the failed attempt.")]
    internal static partial void OptimisticConcurrencyRetry(this ILogger logger, LogLevel level);
}
