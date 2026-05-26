// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// Provides administrative operations for bulk-importing users into the platform.
/// </summary>
public interface IUserImporter
{
    /// <summary>
    /// Imports a batch of user records. Each record is processed independently —
    /// a failure on one record does not affect others in the batch.
    /// Conflicts are resolved by the registered <see cref="IUserImportConflictResolver"/>.
    /// </summary>
    /// <param name="records">The user records to import.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing per-record outcomes.</returns>
    Task<UserImportBatchResult> ImportAsync(IReadOnlyList<UserImportRecord> records, Ct ct);
}
