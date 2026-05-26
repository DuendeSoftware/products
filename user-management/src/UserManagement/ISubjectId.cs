// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

/// <summary>
/// Represents a subject identifier — a way to identify a user or external principal.
/// Implemented by <see cref="EmailAddress"/>, <see cref="PhoneNumber"/>,
/// <see cref="OpaqueSubjectId"/>, and <see cref="UserSubjectId"/>.
/// </summary>
public interface ISubjectId
{
    /// <summary>
    /// The string representation of this subject identifier.
    /// </summary>
    string ToString();
}
