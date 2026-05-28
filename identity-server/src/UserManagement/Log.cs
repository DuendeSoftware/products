// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.UserManagement;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(NoSubClaimPresent),
        Level = LogLevel.Warning,
        Message = "No sub claim present; no claims will be issued")]
    internal static partial void NoSubClaimPresent(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(SubjectIdNotValid),
        Level = LogLevel.Warning,
        Message = "Subject ID {Sub} is not a valid UserSubjectId; no claims will be issued")]
    internal static partial void SubjectIdNotValid(this ILogger logger, string sub);

    [LoggerMessage(
        EventName = nameof(NoUserProfileFound),
        Level = LogLevel.Warning,
        Message = "No user profile found for subject ID {Sub}")]
    internal static partial void NoUserProfileFound(this ILogger logger, string sub);

    [LoggerMessage(
        EventName = nameof(SubjectIdNotValidInactive),
        Level = LogLevel.Warning,
        Message = "Subject ID {Sub} is not a valid UserSubjectId; treating user as inactive")]
    internal static partial void SubjectIdNotValidInactive(this ILogger logger, string sub);
}
