// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Import;

namespace PlatformUserManagementApp.Import;

/// <summary>
/// Resolves import conflicts by overwriting existing users. If the conflict is due to
/// the same subject ID already existing, the existing user is overwritten. For concurrency
/// conflicts, the operation is retried.
/// </summary>
internal sealed class OverwriteConflictResolver : IUserImportConflictResolver
{
    public Task<UserImportConflictResolution> ResolveAsync(UserImportConflict conflict, Ct ct) =>
        Task.FromResult<UserImportConflictResolution>(conflict.Reason switch
        {
            UserImportConflictReason.ConcurrencyConflict => new UserImportConflictResolution.Retry(),
            _ => new UserImportConflictResolution.Overwrite(conflict.Record.SubjectId)
        });
}
