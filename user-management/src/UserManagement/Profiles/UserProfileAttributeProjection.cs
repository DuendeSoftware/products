// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

public sealed record UserProfileAttributeProjection
{
    internal UserProfileAttributeProjection(UserSubjectId subjectId, UserName? userName, AttributeValueCollection attributes)
    {
        SubjectId = subjectId;
        UserName = userName;
        Attributes = attributes;
    }

    public UserSubjectId SubjectId { get; }

    public UserName? UserName { get; }

    public AttributeValueCollection Attributes { get; }

#pragma warning disable CA1043 // Use integral or string argument for indexers
    public AttributeValue this[AttributeCode code] => Attributes[code];
#pragma warning restore CA1043

    public bool Contains(AttributeCode code) => Attributes.Contains(code);

    public bool TryGet(AttributeCode code, out AttributeValue? attribute) => Attributes.TryGet(code, out attribute);
}
