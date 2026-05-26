// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Import.Internal;

internal static partial class Log
{
    [LoggerMessage(Message = $"Starting batch user import of {{{Parameters.RecordCount}}} record(s).")]
    internal static partial void BatchImportStarted(this ILogger logger, LogLevel level, int recordCount);

    [LoggerMessage(Message = $"Validation failed: {{{Parameters.ValidationError}}}")]
    internal static partial void RecordValidationFailed(this ILogger logger, LogLevel level, string validationError);

    [LoggerMessage(Message = $"Conflict detected: step={{{Parameters.ImportStep}}}, reason={{{Parameters.ConflictReason}}}")]
    internal static partial void ConflictDetected(this ILogger logger, LogLevel level, UserImportStep importStep, UserImportConflictReason conflictReason);

    [LoggerMessage(Message = $"Conflict resolution strategy applied: {{{Parameters.Resolution}}}")]
    internal static partial void ConflictResolutionApplied(this ILogger logger, LogLevel level, string resolution);

    [LoggerMessage(Message = $"Retry triggered, attempt {{{Parameters.Attempt}}} of {{{Parameters.MaxAttempts}}}.")]
    internal static partial void RetryTriggered(this ILogger logger, LogLevel level, int attempt, int maxAttempts);

    [LoggerMessage(Message = $"Batch import completed: {{{Parameters.SuccessCount}}} created, {{{Parameters.UpdatedCount}}} updated, {{{Parameters.SkippedCount}}} skipped, {{{Parameters.FailedCount}}} failed.")]
    internal static partial void BatchImportCompleted(this ILogger logger, LogLevel level, int successCount, int updatedCount, int skippedCount, int failedCount);

    private static class Parameters
    {
        internal const string RecordCount = nameof(RecordCount);
        internal const string ValidationError = nameof(ValidationError);
        internal const string ImportStep = nameof(ImportStep);
        internal const string ConflictReason = nameof(ConflictReason);
        internal const string Resolution = nameof(Resolution);
        internal const string Attempt = nameof(Attempt);
        internal const string MaxAttempts = nameof(MaxAttempts);
        internal const string SuccessCount = nameof(SuccessCount);
        internal const string UpdatedCount = nameof(UpdatedCount);
        internal const string SkippedCount = nameof(SkippedCount);
        internal const string FailedCount = nameof(FailedCount);
    }
}
