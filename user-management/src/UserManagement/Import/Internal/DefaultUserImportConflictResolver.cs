// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import.Internal;

/// <summary>
/// Default conflict resolver: retries on concurrency conflicts, skips everything else.
/// </summary>
internal sealed class DefaultUserImportConflictResolver : IUserImportConflictResolver
{
    /// <inheritdoc />
    public Task<UserImportConflictResolution> ResolveAsync(UserImportConflict conflict, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        return Task.FromResult<UserImportConflictResolution>(conflict.Reason == UserImportConflictReason.ConcurrencyConflict
            ? new UserImportConflictResolution.Retry()
            : new UserImportConflictResolution.Skip());
    }
}
