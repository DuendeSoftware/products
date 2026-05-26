// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// The resolution to apply when a conflict is detected during user import.
/// </summary>
public abstract record UserImportConflictResolution
{
    /// <summary>Skip the conflicting step. Existing data is left unchanged.</summary>
    public sealed record Skip : UserImportConflictResolution;

    /// <summary>
    /// Overwrite the existing user identified by <see cref="TargetSubjectId"/>.
    /// The importer will merge the incoming data into that user's profile and authenticators.
    /// Profile attributes are overlaid; authenticators are merged additively.
    /// </summary>
    public sealed record Overwrite(UserSubjectId TargetSubjectId) : UserImportConflictResolution;

    /// <summary>
    /// Retry the operation. Useful when the resolver has taken corrective action
    /// (e.g., deleted the conflicting record). Subject to a retry cap.
    /// </summary>
    public sealed record Retry : UserImportConflictResolution;
}
