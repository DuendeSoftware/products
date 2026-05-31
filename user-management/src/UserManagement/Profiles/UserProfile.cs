// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

/// <summary>
/// Represents a user profile containing the subject identifier and a collection of attribute values.
/// </summary>
public sealed record UserProfile
{
    internal UserProfile(Internal.UserProfile profile)
    {
        SubjectId = profile.SubjectId;
        Attributes = profile.Attributes;
    }

    /// <summary>
    /// Gets the subject identifier that uniquely identifies the user.
    /// </summary>
    public UserSubjectId SubjectId { get; }

    /// <summary>
    /// Gets the full set of attribute values stored on this profile, keyed by attribute code.
    /// </summary>
    public IReadOnlyDictionary<AttributeCode, AttributeValue> Attributes { get; }
}
