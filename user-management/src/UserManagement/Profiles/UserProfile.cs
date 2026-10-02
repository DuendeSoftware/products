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
        Schema = profile.Schema;
        Attributes = profile.Attributes;
    }

    /// <summary>
    /// Gets the subject identifier that uniquely identifies the user.
    /// </summary>
    public UserSubjectId SubjectId { get; }

    /// <summary>
    /// Gets the schema used to interpret and validate the profile's attributes.
    /// </summary>
    /// <remarks>
    /// The schema is a snapshot and is not refreshed automatically when schema definitions change.
    /// </remarks>
    public IReadOnlyAttributeSchema Schema { get; }

    /// <summary>
    /// Gets the full set of attribute values stored on this profile, keyed by attribute code.
    /// </summary>
    public IReadOnlyDictionary<AttributeCode, AttributeValue> Attributes { get; }

    /// <summary>
    /// Creates a new mutable attribute value collection, validated against the profile's <see cref="Schema"/>,
    /// independent of this profile, suitable for passing to <c>TryUpdateAsync</c> after making edits.
    /// </summary>
    /// <returns>A new <see cref="AttributeValueCollection"/> initialized from this profile's attributes.</returns>
    public AttributeValueCollection ToUpdate() => new(Schema, Attributes.Values);
}
