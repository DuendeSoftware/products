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
}
