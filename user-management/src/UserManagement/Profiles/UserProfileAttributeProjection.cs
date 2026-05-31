// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

/// <summary>
/// Represents a partial view of a user profile containing only a selected subset of attribute values.
/// </summary>
public sealed record UserProfileAttributeProjection
{
    internal UserProfileAttributeProjection(UserSubjectId subjectId, AttributeValueCollection attributes)
    {
        SubjectId = subjectId;
        Attributes = attributes;
    }

    /// <summary>
    /// Gets the subject identifier that uniquely identifies the user.
    /// </summary>
    public UserSubjectId SubjectId { get; }

    /// <summary>
    /// Gets the projected collection of attribute values included in this projection.
    /// </summary>
    public AttributeValueCollection Attributes { get; }

#pragma warning disable CA1043 // Use integral or string argument for indexers
    /// <summary>
    /// Gets the attribute value for the specified <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The attribute code to retrieve.</param>
    /// <returns>The <see cref="AttributeValue"/> for the given code.</returns>
    public AttributeValue this[AttributeCode code] => Attributes[code];
#pragma warning restore CA1043

    /// <summary>
    /// Determines whether this projection contains an attribute with the specified <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The attribute code to check.</param>
    /// <returns><c>true</c> if the attribute is present; otherwise, <c>false</c>.</returns>
    public bool Contains(AttributeCode code) => Attributes.Contains(code);

    /// <summary>
    /// Attempts to retrieve the attribute value for the specified <paramref name="code"/>.
    /// </summary>
    /// <param name="code">The attribute code to look up.</param>
    /// <param name="attribute">When this method returns, contains the attribute value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the attribute was found; otherwise, <c>false</c>.</returns>
    public bool TryGet(AttributeCode code, out AttributeValue? attribute) => Attributes.TryGet(code, out attribute);
}
