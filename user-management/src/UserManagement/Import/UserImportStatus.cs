// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// The outcome status of a single user import record.
/// </summary>
public enum UserImportStatus
{
    /// <summary>The user was successfully created.</summary>
    Created,

    /// <summary>The user was successfully updated (overwrite conflict resolution).</summary>
    Updated,

    /// <summary>The user was skipped due to a conflict resolved as <see cref="UserImportConflictResolution.Skip"/>.</summary>
    Skipped,

    /// <summary>The user failed to import due to an error.</summary>
    Failed
}
