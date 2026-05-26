// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Scim.Internal;

internal static partial class Log
{
    // ── User CRUD ────────────────────────────────────────────────────────────

    [LoggerMessage(Message = $"SCIM create user validation failed: {{{Parameters.ScimDetail}}}")]
    internal static partial void ScimCreateUserValidationFailed(this ILogger logger, LogLevel level, string scimDetail);

    [LoggerMessage(Message = $"SCIM get user {{{LogParameters.SubjectId}}}: not found")]
    internal static partial void ScimGetUserNotFound(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM replace user {{{LogParameters.SubjectId}}}: not found")]
    internal static partial void ScimReplaceUserNotFound(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM replace user {{{LogParameters.SubjectId}}}: precondition failed (ETag mismatch)")]
    internal static partial void ScimReplaceUserPreconditionFailed(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM replace user {{{LogParameters.SubjectId}}}: validation failed: {{{Parameters.ScimDetail}}}")]
    internal static partial void ScimReplaceUserValidationFailed(this ILogger logger, LogLevel level, string subjectId, string scimDetail);

    [LoggerMessage(Message = $"SCIM replace user {{{LogParameters.SubjectId}}} succeeded")]
    internal static partial void ScimReplaceUserSucceeded(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM patch user {{{LogParameters.SubjectId}}}: not found")]
    internal static partial void ScimPatchUserNotFound(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM patch user {{{LogParameters.SubjectId}}}: precondition failed (ETag mismatch)")]
    internal static partial void ScimPatchUserPreconditionFailed(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM patch user {{{LogParameters.SubjectId}}}: validation failed: {{{Parameters.ScimDetail}}}")]
    internal static partial void ScimPatchUserValidationFailed(this ILogger logger, LogLevel level, string subjectId, string scimDetail);

    [LoggerMessage(Message = $"SCIM patch user {{{LogParameters.SubjectId}}} succeeded")]
    internal static partial void ScimPatchUserSucceeded(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM delete user {{{LogParameters.SubjectId}}}: not found")]
    internal static partial void ScimDeleteUserNotFound(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM delete user {{{LogParameters.SubjectId}}}: precondition failed (ETag mismatch)")]
    internal static partial void ScimDeleteUserPreconditionFailed(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM delete user {{{LogParameters.SubjectId}}} succeeded")]
    internal static partial void ScimDeleteUserSucceeded(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(Message = $"SCIM create user succeeded: subject {{{LogParameters.SubjectId}}}")]
    internal static partial void ScimCreateUserSucceeded(this ILogger logger, LogLevel level, string subjectId);

    // ── Group CRUD ───────────────────────────────────────────────────────────

    [LoggerMessage(Message = $"SCIM create group validation failed: {{{Parameters.ScimDetail}}}")]
    internal static partial void ScimCreateGroupValidationFailed(this ILogger logger, LogLevel level, string scimDetail);

    [LoggerMessage(Message = $"SCIM get group {{{LogParameters.GroupId}}}: not found")]
    internal static partial void ScimGetGroupNotFound(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM replace group {{{LogParameters.GroupId}}}: not found")]
    internal static partial void ScimReplaceGroupNotFound(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM replace group {{{LogParameters.GroupId}}}: precondition failed (ETag mismatch)")]
    internal static partial void ScimReplaceGroupPreconditionFailed(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM replace group {{{LogParameters.GroupId}}}: validation failed: {{{Parameters.ScimDetail}}}")]
    internal static partial void ScimReplaceGroupValidationFailed(this ILogger logger, LogLevel level, string groupId, string scimDetail);

    [LoggerMessage(Message = $"SCIM replace group {{{LogParameters.GroupId}}} succeeded")]
    internal static partial void ScimReplaceGroupSucceeded(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM patch group {{{LogParameters.GroupId}}}: not found")]
    internal static partial void ScimPatchGroupNotFound(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM patch group {{{LogParameters.GroupId}}}: precondition failed (ETag mismatch)")]
    internal static partial void ScimPatchGroupPreconditionFailed(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM patch group {{{LogParameters.GroupId}}}: validation failed: {{{Parameters.ScimDetail}}}")]
    internal static partial void ScimPatchGroupValidationFailed(this ILogger logger, LogLevel level, string groupId, string scimDetail);

    [LoggerMessage(Message = $"SCIM patch group {{{LogParameters.GroupId}}} succeeded")]
    internal static partial void ScimPatchGroupSucceeded(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM delete group {{{LogParameters.GroupId}}}: not found")]
    internal static partial void ScimDeleteGroupNotFound(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM delete group {{{LogParameters.GroupId}}}: precondition failed (ETag mismatch)")]
    internal static partial void ScimDeleteGroupPreconditionFailed(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM delete group {{{LogParameters.GroupId}}} succeeded")]
    internal static partial void ScimDeleteGroupSucceeded(this ILogger logger, LogLevel level, string groupId);

    [LoggerMessage(Message = $"SCIM create group succeeded: group {{{LogParameters.GroupId}}}")]
    internal static partial void ScimCreateGroupSucceeded(this ILogger logger, LogLevel level, string groupId);

    // ── Filter parsing ───────────────────────────────────────────────────────

    [LoggerMessage(Message = $"SCIM filter parse failure for resource type '{{{Parameters.ResourceType}}}': {{{Parameters.ScimDetail}}}")]
    internal static partial void ScimFilterParseFailure(this ILogger logger, LogLevel level, string resourceType, string scimDetail);

    // ── Bulk operations ──────────────────────────────────────────────────────

    [LoggerMessage(Message = $"SCIM bulk routing operation {{{Parameters.OperationIndex}}}: {{{Parameters.Method}}} {{{Parameters.Path}}}")]
    internal static partial void ScimBulkOperationRouting(this ILogger logger, LogLevel level, int operationIndex, string method, string path);

    [LoggerMessage(Message = $"SCIM bulk resolved bulkId '{{{Parameters.BulkId}}}' to resource '{{{Parameters.ResourceId}}}'")]
    internal static partial void ScimBulkIdResolved(this ILogger logger, LogLevel level, string bulkId, string resourceId);

    [LoggerMessage(Message = $"SCIM bulk failOnErrors threshold reached ({{{Parameters.ErrorCount}}} errors); skipping remaining operations")]
    internal static partial void ScimBulkFailOnErrorsThresholdHit(this ILogger logger, LogLevel level, int errorCount);

    [LoggerMessage(Message = $"SCIM bulk operation completed: {{{Parameters.OperationCount}}} operations, {{{Parameters.ErrorCount}}} errors")]
    internal static partial void ScimBulkCompleted(this ILogger logger, LogLevel level, int operationCount, int errorCount);

    [LoggerMessage(EventId = 1, Message = $"Unhandled exception processing bulk operation {{{Parameters.Method}}} {{{Parameters.Path}}}")]
    internal static partial void ScimBulkOperationError(this ILogger logger, LogLevel level, string method, string path, Exception ex);

    [LoggerMessage(EventId = 2, Message = "SCIM create user: repository store operation failed")]
    internal static partial void ScimCreateUserRepositoryFailed(this ILogger logger, LogLevel level);

    [LoggerMessage(EventId = 3, Message = $"SCIM update user {{{LogParameters.SubjectId}}}: repository store operation failed")]
    internal static partial void ScimUpdateUserRepositoryFailed(this ILogger logger, LogLevel level, string subjectId);

    [LoggerMessage(EventId = 4, Message = $"SCIM bulk operation {{{Parameters.OperationIndex}}}: returned error status")]
    internal static partial void ScimBulkOperationErrorStatus(this ILogger logger, LogLevel level, int operationIndex);

    // ── Metadata endpoints ───────────────────────────────────────────────────

    [LoggerMessage(Message = "SCIM ServiceProviderConfig accessed")]
    internal static partial void ScimServiceProviderConfigAccessed(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = "SCIM Schemas endpoint accessed")]
    internal static partial void ScimSchemasAccessed(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = "SCIM ResourceTypes endpoint accessed")]
    internal static partial void ScimResourceTypesAccessed(this ILogger logger, LogLevel level);

    // ── Content-type filter ──────────────────────────────────────────────────

    [LoggerMessage(Message = $"SCIM request rejected: unsupported content type '{{{Parameters.ContentType}}}'")]
    internal static partial void ScimContentTypeRejected(this ILogger logger, LogLevel level, string contentType);

    // ── Local parameter names ────────────────────────────────────────────────

    private static class Parameters
    {
        internal const string ScimDetail = nameof(ScimDetail);
        internal const string ResourceType = nameof(ResourceType);
        internal const string OperationCount = nameof(OperationCount);
        internal const string OperationIndex = nameof(OperationIndex);
        internal const string Method = nameof(Method);
        internal const string Path = nameof(Path);
        internal const string BulkId = nameof(BulkId);
        internal const string ResourceId = nameof(ResourceId);
        internal const string ErrorCount = nameof(ErrorCount);
        internal const string ContentType = nameof(ContentType);
    }
}
