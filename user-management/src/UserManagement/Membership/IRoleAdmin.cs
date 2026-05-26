// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;
using Duende.UserManagement.Admin;

namespace Duende.UserManagement.Membership;

/// <summary>
/// Provides administrative operations for roles.
/// </summary>
public interface IRoleAdmin
{
    /// <summary>
    /// Creates a new role.
    /// </summary>
    /// <param name="role">The role to create.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A result containing the created role ID with its version on success,
    /// or an error if creation failed (e.g., role name already exists).
    /// </returns>
    Task<SaveResult<RoleId>> CreateAsync(Role role, Ct ct);

    /// <summary>
    /// Gets a role by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the role.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A result containing the role if found, or a not found result.
    /// </returns>
    Task<GetResult<Role>> GetAsync(RoleId id, Ct ct);

    /// <summary>
    /// Updates an existing role.
    /// </summary>
    /// <param name="id">The unique identifier of the role to update.</param>
    /// <param name="role">The role with updated values.</param>
    /// <param name="expectedVersion">The expected version for optimistic concurrency.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A result containing the updated role ID with its new version on success,
    /// or an error if update failed (e.g., version conflict, not found).
    /// </returns>
    Task<SaveResult<RoleId>> UpdateAsync(RoleId id, Role role, Admin.DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Deletes a role by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the role to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A success result if deleted, or an error if deletion failed.
    /// </returns>
    Task<SaveResult<RoleId>> DeleteAsync(RoleId id, Ct ct);

    /// <summary>
    /// Queries roles with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A paged list of role summaries matching the criteria.
    /// </returns>
    Task<QueryResult<RoleListItem>> QueryAsync(
        QueryRequest<RoleFilter, RoleSortField> request,
        Ct ct);
}
