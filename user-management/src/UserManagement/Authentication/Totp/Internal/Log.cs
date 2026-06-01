// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Totp.Internal;

internal static partial class Log
{
    [LoggerMessage(Message = $"TOTP authentication attempt started for subject {{{LogParameters.SubjectId}}} device {{{Parameters.DeviceName}}}")]
    internal static partial void TotpAuthenticationStarted(this ILogger logger, LogLevel level, UserSubjectId subjectId, TotpDeviceName deviceName);

    // Information (not Warning): user not found is an expected flow (e.g., unknown username); dummy auth is performed to prevent timing attacks.
    [LoggerMessage(Message = $"TOTP authentication: user {{{LogParameters.SubjectId}}} not found; performing dummy authentication to prevent timing attacks")]
    internal static partial void TotpAuthenticationUserNotFound(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // Information (not Warning): throttling is an expected security control, not an error condition.
    [LoggerMessage(Message = $"TOTP authentication rejected for subject {{{LogParameters.SubjectId}}}: attempt policy rejected the request (throttled)")]
    internal static partial void TotpAuthenticationThrottled(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"TOTP authentication failed for subject {{{LogParameters.SubjectId}}} device {{{Parameters.DeviceName}}}: TOTP verification failed")]
    internal static partial void TotpAuthenticationFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId, TotpDeviceName deviceName);

    [LoggerMessage(Message = $"TOTP authentication succeeded for subject {{{LogParameters.SubjectId}}} device {{{Parameters.DeviceName}}}")]
    internal static partial void TotpAuthenticationSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId, TotpDeviceName deviceName);

    [LoggerMessage(Message = "TOTP authentication hit an optimistic concurrency conflict; retrying once to record the failed attempt")]
    internal static partial void OptimisticConcurrencyRetry(this ILogger logger, LogLevel level);

    private static class Parameters
    {
        internal const string DeviceName = nameof(DeviceName);
    }
}
