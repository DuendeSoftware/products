// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// Describes a conflict detected during user import.
/// </summary>
public sealed record UserImportConflict
{
    /// <summary>The incoming record that caused the conflict.</summary>
    public required UserImportRecord Record { get; init; }

    /// <summary>Which import step encountered the conflict.</summary>
    public required UserImportStep Step { get; init; }

    /// <summary>The reason for the conflict.</summary>
    public required UserImportConflictReason Reason { get; init; }

    /// <summary>The underlying exception that signaled the conflict.</summary>
    public required Exception Exception { get; init; }
}
