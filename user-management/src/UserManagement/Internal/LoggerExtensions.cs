// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Internal;

internal static class LoggerExtensions
{
    internal static IDisposable? BeginSubjectScope(this ILogger logger, UserSubjectId subjectId)
        => logger.BeginScope(new[] { new KeyValuePair<string, object?>(LogParameters.SubjectId, subjectId.Value) });

    internal static IDisposable? BeginUserNameScope(this ILogger logger, UserName userName)
        => logger.BeginScope(new[] { new KeyValuePair<string, object?>(LogParameters.UserName, userName.Value) });
}
