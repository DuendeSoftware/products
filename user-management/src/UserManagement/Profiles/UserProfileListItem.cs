// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Profiles;

/// <summary>
/// Represents a user profile in a list query result, containing the subject ID and all schema attribute values.
/// </summary>
public sealed record UserProfileListItem
{
    internal UserProfileListItem(UserSubjectId subjectId, IReadOnlyDictionary<string, object> attributes)
    {
        SubjectId = subjectId;
        Attributes = attributes;
    }

    /// <summary>
    /// The unique subject identifier of the user.
    /// </summary>
    public UserSubjectId SubjectId { get; }

    /// <summary>
    /// The schema attribute values, keyed by attribute name.
    /// </summary>
    public IReadOnlyDictionary<string, object> Attributes { get; }
}
