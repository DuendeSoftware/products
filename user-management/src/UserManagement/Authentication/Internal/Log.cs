// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Internal;

internal static partial class Log
{
    // User not found
    [LoggerMessage(Message = $"User not found for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void UserNotFound(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // TOTP
    [LoggerMessage(Message = $"TOTP authenticator add rejected for subject {{{LogParameters.SubjectId}}}: duplicate authenticator name.")]
    internal static partial void TotpAddDuplicate(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // Passkeys
    [LoggerMessage(Message = $"Passkey add rejected for subject {{{LogParameters.SubjectId}}}: duplicate credential.")]
    internal static partial void PasskeyAddDuplicate(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Passkey remove rejected for subject {{{LogParameters.SubjectId}}}: credential not found.")]
    internal static partial void PasskeyRemoveNotFound(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // Password — set
    [LoggerMessage(Message = $"Password set succeeded for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void PasswordSetSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Password set failed for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void PasswordSetFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // Password — change
    [LoggerMessage(Message = $"Password change rejected for subject {{{LogParameters.SubjectId}}}: throttled.")]
    internal static partial void PasswordChangeThrottled(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Password change rejected for subject {{{LogParameters.SubjectId}}}: old password incorrect.")]
    internal static partial void PasswordChangeOldPasswordIncorrect(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Password change succeeded for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void PasswordChangeSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Password change failed for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void PasswordChangeFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(
        EventId = 1,
        Message = "Authentication attempt update hit an optimistic concurrency conflict while changing a password; retrying once to record the failed attempt.")]
    internal static partial void PasswordChangeOptimisticConcurrencyRetry(this ILogger logger, LogLevel level);

    // Password — reset
    [LoggerMessage(Message = $"Password reset succeeded for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void PasswordResetSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Password reset failed for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void PasswordResetFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // OTP address — self-service
    [LoggerMessage(Message = $"OTP address added for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void OtpAddressAdded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"OTP address removed for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void OtpAddressRemoved(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"OTP verification failed for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void OtpVerificationFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // Recovery codes — self-service
    [LoggerMessage(Message = $"Recovery codes created for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void RecoveryCodesCreated(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Recovery codes creation failed for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void RecoveryCodesCreateFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // External authenticator — self-service
    [LoggerMessage(Message = $"External authenticator added for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void ExternalAuthenticatorAdded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"External authenticator removed for subject {{{LogParameters.SubjectId}}}.")]
    internal static partial void ExternalAuthenticatorRemoved(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // Optimistic concurrency — failure recorder
    [LoggerMessage(
        EventId = 2,
        Message = "Authentication failed-attempt retry also hit an optimistic concurrency conflict. The failed attempt was not persisted on the retry path.")]
    internal static partial void FailedAttemptRetryOptimisticConcurrencyConflict(this ILogger logger, LogLevel level);
}
