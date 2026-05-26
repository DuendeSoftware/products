// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// The outcome of importing a single user record.
/// </summary>
public sealed record UserImportResult
{
    /// <summary>The subject ID of the user.</summary>
    public required UserSubjectId SubjectId { get; init; }

    /// <summary>The outcome status.</summary>
    public required UserImportStatus Status { get; init; }

    /// <summary>Error message when <see cref="Status"/> is <see cref="UserImportStatus.Failed"/>.</summary>
    public string? Error { get; init; }
}
