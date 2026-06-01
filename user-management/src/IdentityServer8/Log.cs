// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.UserManagement;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(NoSubClaimPresent),
        Message = "No sub claim present; no claims will be issued")]
    internal static partial void NoSubClaimPresent(this ILogger logger, LogLevel level);

    [LoggerMessage(
        EventName = nameof(SubjectIdNotValid),
        Message = "Subject ID {Sub} is not a valid UserSubjectId; no claims will be issued")]
    internal static partial void SubjectIdNotValid(this ILogger logger, LogLevel level, string sub);

    [LoggerMessage(
        EventName = nameof(NoUserProfileFound),
        Message = "No user profile found for subject ID {Sub}")]
    internal static partial void NoUserProfileFound(this ILogger logger, LogLevel level, UserSubjectId sub);

    [LoggerMessage(
        EventName = nameof(SubjectIdNotValidInactive),
        Message = "Subject ID {Sub} is not a valid UserSubjectId; treating user as inactive")]
    internal static partial void SubjectIdNotValidInactive(this ILogger logger, LogLevel level, string sub);

    [LoggerMessage(
        EventName = nameof(IsActiveCalled),
        Message = "IsActive called from: {Caller}")]
    internal static partial void IsActiveCalled(this ILogger logger, LogLevel level, string caller);

    [LoggerMessage(
        EventName = nameof(UserHasNoAuthenticator),
        Message = "User {SubjectId} does not have authenticators. Returning IsActive = false")]
    internal static partial void UserHasNoAuthenticator(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(
        EventName = nameof(ComplexAttributeTypeNotSupported),
        Message = "Attribute {AttributeCode} has a complex type and cannot be mapped to a claim; skipping")]
    internal static partial void ComplexAttributeTypeNotSupported(this ILogger logger, LogLevel level, string attributeCode);
}
