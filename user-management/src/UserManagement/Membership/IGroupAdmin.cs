// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;
using Duende.UserManagement.Admin;

namespace Duende.UserManagement.Membership;

/// <summary>
/// Provides administrative operations for groups.
/// </summary>
public interface IGroupAdmin
{
    /// <summary>
    /// Creates a new group.
    /// </summary>
    /// <param name="group">The group to create.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A result containing the created group ID with its version on success,
    /// or an error if creation failed (e.g., group name already exists).
    /// </returns>
    Task<SaveResult<GroupId>> CreateAsync(Group group, Ct ct);

    /// <summary>
    /// Gets a group by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the group.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A result containing the group if found, or a not found result.
    /// </returns>
    Task<GetResult<Group>> GetAsync(GroupId id, Ct ct);

    /// <summary>
    /// Updates an existing group.
    /// </summary>
    /// <param name="id">The unique identifier of the group to update.</param>
    /// <param name="group">The group with updated values.</param>
    /// <param name="expectedVersion">The expected version for optimistic concurrency.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A result containing the updated group ID with its new version on success,
    /// or an error if update failed (e.g., version conflict, not found).
    /// </returns>
    Task<SaveResult<GroupId>> UpdateAsync(GroupId id, Group group, Admin.DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Deletes a group by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the group to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A success result if deleted, or an error if deletion failed.
    /// </returns>
    Task<SaveResult<GroupId>> DeleteAsync(GroupId id, Ct ct);

    /// <summary>
    /// Queries groups with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A paged list of group summaries matching the criteria.
    /// </returns>
    Task<QueryResult<GroupListItem>> QueryAsync(
        QueryRequest<GroupFilter, GroupSortField> request,
        Ct ct);
}
