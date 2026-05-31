// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

/// <summary>
/// Provides self-service operations that allow end-users to register, retrieve, and update their own profiles.
/// </summary>
public interface IUserProfileSelfService
{
    /// <summary>
    /// Retrieves the current attribute schema for user profiles.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The read-only attribute schema.</returns>
    Task<IReadOnlyAttributeSchema> GetSchemaAsync(Ct ct);

    /// <summary>
    /// Attempts to register a new user profile for the given subject identifier.
    /// Returns <c>null</c> if a profile for the subject already exists.
    /// </summary>
    /// <param name="subjectId">The subject identifier of the user to register.</param>
    /// <param name="attributes">The validated attribute values to store on the new profile.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The newly created <see cref="UserProfile"/>, or <c>null</c> if the subject already exists.</returns>
    Task<UserProfile?> TryRegisterAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct);

    /// <summary>
    /// Attempts to retrieve the profile for the given subject identifier.
    /// Returns <c>null</c> if no profile is found.
    /// </summary>
    /// <param name="subjectId">The subject identifier to look up.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The matching <see cref="UserProfile"/>, or <c>null</c> if not found.</returns>
    Task<UserProfile?> TryGetAsync(UserSubjectId subjectId, Ct ct);

    /// <summary>
    /// Attempts to update the profile for the given subject identifier with new attribute values.
    /// Returns <c>null</c> if no profile is found for the subject.
    /// </summary>
    /// <param name="subjectId">The subject identifier of the profile to update.</param>
    /// <param name="attributes">The validated attribute values to apply to the profile.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The updated <see cref="UserProfile"/>, or <c>null</c> if the subject was not found.</returns>
    Task<UserProfile?> TryUpdateAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct);
}
