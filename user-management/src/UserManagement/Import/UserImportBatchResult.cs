// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// The aggregate result of a batch user import operation.
/// </summary>
public sealed record UserImportBatchResult
{
    private int? _createdCount;
    private int? _updatedCount;
    private int? _skippedCount;
    private int? _failedCount;

    /// <summary>Per-record outcomes, in the same order as the input records.</summary>
    public required IReadOnlyList<UserImportResult> Results { get; init; }

    /// <summary>Number of users successfully created.</summary>
    public int CreatedCount => _createdCount ??= CountByStatus(UserImportStatus.Created);

    /// <summary>Number of users successfully updated (overwrite).</summary>
    public int UpdatedCount => _updatedCount ??= CountByStatus(UserImportStatus.Updated);

    /// <summary>Number of users skipped due to conflict resolution.</summary>
    public int SkippedCount => _skippedCount ??= CountByStatus(UserImportStatus.Skipped);

    /// <summary>Number of users that failed to import.</summary>
    public int FailedCount => _failedCount ??= CountByStatus(UserImportStatus.Failed);

    private int CountByStatus(UserImportStatus status) => Results.Count(r => r.Status == status);
}
