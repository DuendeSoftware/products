// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// Resolves conflicts that arise during user import. Register an implementation
/// in DI to customize conflict handling. The default implementation skips on
/// most conflicts and retries on concurrency conflicts.
/// </summary>
public interface IUserImportConflictResolver
{
    /// <summary>
    /// Called when a conflict is detected during import. Returns the resolution to apply.
    /// </summary>
    Task<UserImportConflictResolution> ResolveAsync(UserImportConflict conflict, Ct ct);
}
