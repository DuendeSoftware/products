// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal static partial class Log
{
    [LoggerMessage(Message = $"Failed to delete consumed passkey authentication challenge {{{Parameters.ChallengeId}}}. Result: {{{Parameters.DeleteResult}}}")]
    internal static partial void FailedToDeletePasskeyAuthenticationChallenge(this ILogger logger, LogLevel level, PasskeyAuthenticationChallengeId challengeId, DeleteResult deleteResult);

    [LoggerMessage(Message = $"Failed to delete consumed passkey registration challenge {{{Parameters.ChallengeId}}}. Result: {{{Parameters.DeleteResult}}}")]
    internal static partial void FailedToDeletePasskeyRegistrationChallenge(this ILogger logger, LogLevel level, PasskeyRegistrationChallengeId challengeId, DeleteResult deleteResult);

    // Information (not Debug/Warning): Challenge expiry is expected behavior, but a high volume
    // of expired challenges may indicate misconfigured timeouts, UX issues, or probing attacks.
    // Keeping this at Information ensures it's visible in production logs for operational monitoring.
    [LoggerMessage(Message = $"Passkey authentication challenge {{{Parameters.ChallengeId}}} has expired.")]
    internal static partial void PasskeyAuthenticationChallengeExpired(this ILogger logger, LogLevel level, PasskeyAuthenticationChallengeId challengeId);

    // See comment above for log level rationale.
    [LoggerMessage(Message = $"Passkey registration challenge {{{Parameters.ChallengeId}}} has expired.")]
    internal static partial void PasskeyRegistrationChallengeExpired(this ILogger logger, LogLevel level, PasskeyRegistrationChallengeId challengeId);

    [LoggerMessage(Message = "Starting passkey authentication begin ceremony")]
    internal static partial void PasskeyAuthenticateBeginStarting(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = "Starting passkey discoverable authentication begin ceremony")]
    internal static partial void PasskeyAuthenticateDiscoverableBeginStarting(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = $"Passkey begin authentication ceremony failed: {{{LogParameters.Error}}}")]
    internal static partial void PasskeyAuthenticateBeginFailed(this ILogger logger, LogLevel level, AuthenticationBeginError error);

    [LoggerMessage(Message = $"Passkey complete authentication ceremony failed: {{{LogParameters.Error}}}")]
    internal static partial void PasskeyAuthenticateCompleteFailed(this ILogger logger, LogLevel level, AuthenticationCompleteError error);

    [LoggerMessage(Message = $"Passkey complete registration ceremony failed: {{{LogParameters.Error}}}")]
    internal static partial void PasskeyRegisterCompleteFailed(this ILogger logger, LogLevel level, RegistrationError error);

    [LoggerMessage(Message = $"Passkey authentication complete ceremony user {{{LogParameters.SubjectId}}} not found")]
    internal static partial void PasskeyAuthenticateCompleteUserNotFound(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"Passkey authentication complete ceremony succeeded for user {{{LogParameters.SubjectId}}}")]
    internal static partial void PasskeyAuthenticateCompleteSignedIn(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = "Passkey registration begin ceremony rejected, user is not authenticated")]
    internal static partial void PasskeyRegisterBeginUnauthenticated(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = $"Starting passkey registration begin ceremony for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void PasskeyRegisterBeginStarted(this ILogger logger, LogLevel level, Guid subjectId);

    [LoggerMessage(Message = "Passkey registration complete ceremony rejected, user is not authenticated")]
    internal static partial void PasskeyRegisterCompleteUnauthenticated(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = "Passkey registration complete ceremony failed to persist credential")]
    internal static partial void PasskeyRegisterCompletePersistFailed(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = "Passkey complete registration ceremony failed: challenge not found")]
    internal static partial void PasskeyRegistrationChallengeNotFound(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = "Passkey complete authentication ceremony failed: challenge not found")]
    internal static partial void PasskeyAuthenticationChallengeNotFound(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = "Passkey complete authentication ceremony failed: invalid Base64Url credential ID")]
    internal static partial void PasskeyAuthenticationInvalidCredentialIdEncoding(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = "Passkey complete authentication ceremony failed: credential ID is not a valid passkey credential ID")]
    internal static partial void PasskeyAuthenticationInvalidCredentialId(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = $"Passkey complete authentication ceremony failed: credential {{{Parameters.CredentialId}}} not found")]
    internal static partial void PasskeyAuthenticationCredentialNotFound(this ILogger logger, LogLevel level, PasskeyCredentialId credentialId);

    [LoggerMessage(Message = $"Passkey complete authentication ceremony failed: credential {{{Parameters.CredentialId}}} does not belong to the specified user")]
    internal static partial void PasskeyAuthenticationCredentialUserMismatch(this ILogger logger, LogLevel level, PasskeyCredentialId credentialId);

    [LoggerMessage(Message = $"Passkey complete authentication ceremony failed: credential {{{Parameters.CredentialId}}} not found on user")]
    internal static partial void PasskeyAuthenticationCredentialNotOnUser(this ILogger logger, LogLevel level, PasskeyCredentialId credentialId);

    [LoggerMessage(Message = $"Passkey complete authentication ceremony failed to update sign count for credential {{{Parameters.CredentialId}}}")]
    internal static partial void PasskeyAuthenticationSignCountUpdateFailed(this ILogger logger, LogLevel level, PasskeyCredentialId credentialId);

    [LoggerMessage(Message = $"Passkey begin authentication ceremony: no passkeys registered for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void PasskeyAuthenticationNoPasskeysRegistered(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Passkey complete registration ceremony succeeded for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void PasskeyRegisterCompleteSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    private static class Parameters
    {
        internal const string ChallengeId = nameof(ChallengeId);
        internal const string DeleteResult = nameof(DeleteResult);
        internal const string CredentialId = nameof(CredentialId);
    }
}
