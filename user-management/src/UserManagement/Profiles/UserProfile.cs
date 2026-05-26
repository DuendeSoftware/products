// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

public sealed record UserProfile
{
    internal UserProfile(Internal.UserProfile profile)
    {
        SubjectId = profile.SubjectId;
        UserName = profile.UserName;
        Attributes = profile.Attributes;
    }

    public UserSubjectId SubjectId { get; }
    public UserName? UserName { get; }
    public IReadOnlyDictionary<AttributeCode, AttributeValue> Attributes { get; }
}
