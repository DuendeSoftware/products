// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Profiles.Internal;

internal static partial class Log
{
    // Profile Admin

    [LoggerMessage(Message = $"User profile created for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserProfileCreated(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User profile creation failed for subject {{{LogParameters.SubjectId}}} (conflict)")]
    internal static partial void UserProfileCreateFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User profile found for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserProfileFound(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User profile not found for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserProfileNotFound(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User profile found by attribute {{{Parameters.AttributeName}}}")]
    internal static partial void UserProfileFoundByAttribute(this ILogger logger, LogLevel level, string attributeName);

    [LoggerMessage(Message = $"User profile not found by attribute {{{Parameters.AttributeName}}}")]
    internal static partial void UserProfileNotFoundByAttribute(this ILogger logger, LogLevel level, string attributeName);

    // Profile Self-Service

    [LoggerMessage(Message = $"User profile registered for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserProfileRegistered(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User profile registration failed for subject {{{LogParameters.SubjectId}}} (conflict)")]
    internal static partial void UserProfileRegisterFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User profile updated for subject {{{LogParameters.SubjectId}}}")]
    internal static partial void UserProfileUpdated(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    [LoggerMessage(Message = $"User profile update failed for subject {{{LogParameters.SubjectId}}} (concurrency conflict)")]
    internal static partial void UserProfileUpdateFailed(this ILogger logger, LogLevel level, UserSubjectId subjectId);

    // Schema Freshness

    [LoggerMessage(Level = LogLevel.Warning,
        Message = $"Schema version mismatch: validated with {{{Parameters.SchemaId}}} v{{{Parameters.Version}}} but current is {{{Parameters.CurrentSchemaId}}} v{{{Parameters.CurrentVersion}}}")]
    internal static partial void SchemaMismatch(this ILogger logger, UuidV7 schemaId, int version, UuidV7 currentSchemaId, int currentVersion);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = $"Empty attribute collection rejected: schema {{{Parameters.SchemaId}}} has required attributes")]
    internal static partial void EmptyCollectionRejected(this ILogger logger, UuidV7 schemaId);

    private static class Parameters
    {
        internal const string AttributeName = nameof(AttributeName);
        internal const string SchemaId = nameof(SchemaId);
        internal const string Version = nameof(Version);
        internal const string CurrentSchemaId = nameof(CurrentSchemaId);
        internal const string CurrentVersion = nameof(CurrentVersion);
    }
}
