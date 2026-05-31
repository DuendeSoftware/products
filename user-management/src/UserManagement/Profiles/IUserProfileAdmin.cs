// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Querying;

namespace Duende.UserManagement.Profiles;

/// <summary>
/// Provides administrative operations for managing user profiles, including creation, retrieval, and querying.
/// </summary>
public interface IUserProfileAdmin
{
    /// <summary>
    /// Retrieves the current attribute schema for user profiles.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The read-only attribute schema.</returns>
    Task<IReadOnlyAttributeSchema> GetSchemaAsync(Ct ct);

    /// <summary>
    /// Attempts to create a new user profile with the given subject identifier and attributes.
    /// Returns <c>null</c> if a profile for the subject already exists.
    /// </summary>
    /// <param name="subjectId">The subject identifier of the user to create.</param>
    /// <param name="attributes">The validated attribute values to store on the new profile.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The newly created <see cref="UserProfile"/>, or <c>null</c> if the subject already exists.</returns>
    Task<UserProfile?> TryAddAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct);

    /// <summary>
    /// Attempts to retrieve a user profile by subject identifier.
    /// Returns <c>null</c> if no profile is found.
    /// </summary>
    /// <param name="subjectId">The subject identifier to look up.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The matching <see cref="UserProfile"/>, or <c>null</c> if not found.</returns>
    Task<UserProfile?> TryGetAsync(UserSubjectId subjectId, Ct ct);

    /// <summary>
    /// Attempts to retrieve a user profile by matching a specific attribute value.
    /// Returns <c>null</c> if no profile is found.
    /// </summary>
    /// <param name="attributeCode">The attribute code to match against.</param>
    /// <param name="value">The value the attribute must equal.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The matching <see cref="UserProfile"/>, or <c>null</c> if not found.</returns>
    Task<UserProfile?> TryGetAsync(AttributeCode attributeCode, object value, Ct ct);

    /// <summary>
    /// Queries user profiles using the specified query request, returning full profile data.
    /// </summary>
    /// <param name="request">The query parameters including filters, sorting, and pagination.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A paged result of matching <see cref="UserProfile"/> records.</returns>
    Task<QueryResult<UserProfile>> QueryAsync(
        QueryRequest request,
        Ct ct);

    /// <summary>
    /// Queries user profiles using the specified query request, returning only the specified attributes.
    /// </summary>
    /// <param name="request">The query parameters including filters, sorting, and pagination.</param>
    /// <param name="attributes">The set of attribute codes to include in the projection.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A paged result of <see cref="UserProfileAttributeProjection"/> records containing only the requested attributes.</returns>
    Task<QueryResult<UserProfileAttributeProjection>> QueryAsync(
        QueryRequest request,
        HashSet<AttributeCode> attributes,
        Ct ct);
}
