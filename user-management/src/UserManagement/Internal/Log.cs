// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Internal;

internal static partial class Log
{
    // User delete (admin)
    [LoggerMessage(Message = $"Starting user delete for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserDeleteStarting(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User delete succeeded for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserDeleteSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User delete failed for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserDeleteFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // User deregister (self-service)
    [LoggerMessage(Message = $"Starting user deregister for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserDeregisterStarting(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User deregister succeeded for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserDeregisterSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User deregister failed for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserDeregisterFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // Username set
    [LoggerMessage(Message = $"Starting username set for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserNameSetStarting(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Username set succeeded for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserNameSetSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Username set failed for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserNameSetFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // Username remove
    [LoggerMessage(Message = $"Starting username removal for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserNameRemoveStarting(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Username removal succeeded for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserNameRemoveSucceeded(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"Username removal failed for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserNameRemoveFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);
}
