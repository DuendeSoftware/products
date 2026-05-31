// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

/// <summary>
/// Represents a subject identifier for a user, used as a stable, unique identifier across sessions.
/// </summary>
[StringValue]
public partial record UserSubjectId : ISubjectId
{
    internal const int MaxLength = 200;

    /// <summary>
    /// Creates a new <see cref="UserSubjectId"/> with a randomly generated GUID value.
    /// </summary>
    /// <returns>A new <see cref="UserSubjectId"/> instance.</returns>
    public static UserSubjectId New() => Create(Guid.NewGuid().ToString());
}
