// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Internal.Licensing;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(UserCountCheckCompleted),
        Level = LogLevel.Debug,
        Message = "Licensed user count check counted {UserCount} user(s)")]
    internal static partial void UserCountCheckCompleted(this ILogger logger, long userCount);

    [LoggerMessage(
        EventName = nameof(UserCountCheckFailed),
        Level = LogLevel.Warning,
        Message = "Licensed user count check failed; the user count was not validated and will be retried after the next check interval")]
    internal static partial void UserCountCheckFailed(this ILogger logger, Exception exception);
}
