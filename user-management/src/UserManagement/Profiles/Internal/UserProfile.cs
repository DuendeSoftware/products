// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles.Internal;

#pragma warning disable CS1573 // Parameter 'parameter' has no matching param tag in the XML comment for 'parameter' (but other parameters do)
internal sealed class UserProfile
{
    private readonly Dictionary<AttributeCode, AttributeValue> _attributes;

    private UserProfile(UserProfileId id, UserSubjectId subjectId, IEnumerable<AttributeValue> attributes)
    {
        Id = id;
        SubjectId = subjectId;
        _attributes = attributes.ToDictionary(a => a.Code, a => a);
    }

    internal UserProfile(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes) :
        this(UserProfileId.New(), subjectId, attributes)
    {
    }

    internal UserProfileId Id { get; }

    internal UserSubjectId SubjectId { get; }

    internal IReadOnlyDictionary<AttributeCode, AttributeValue> Attributes => _attributes;

    internal void ReplaceAttributes(ValidatedAttributeValueCollection attributes)
    {
        _attributes.Clear();
        foreach (var attribute in attributes)
        {
            _attributes[attribute.Code] = attribute;
        }
    }

    internal static UserProfile Load(UserProfileId id, UserSubjectId subjectId, IEnumerable<AttributeValue> attributes)
        => new(id, subjectId, attributes);
}
