// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Querying;

namespace Duende.Spaces;

/// <summary>
/// Provides administrative operations for managing spaces.
/// </summary>
public interface ISpaceAdmin
{
    /// <summary>
    /// Creates a new space.
    /// </summary>
    /// <param name="configuration">The space configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The space ID and version on success, or validation/conflict errors.</returns>
    Task<SaveResult<SpaceId>> CreateAsync(CreateSpaceConfiguration configuration, Ct ct);

    /// <summary>
    /// Gets a space by its storage identifier.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GetResult<SpaceConfiguration>> GetAsync(SpaceId id, Ct ct);

    /// <summary>
    /// Updates an existing space. The model is mutable: callers can Get, modify, and Update.
    /// Note: <see cref="SpaceConfiguration.PoolId"/> cannot be changed after creation.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="space">The updated space configuration.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<SpaceId>> UpdateAsync(SpaceId id, SpaceConfiguration space, DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Logically deletes the space with the specified ID.
    /// </summary>
    /// <param name="id">The storage identifier of the space to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<SpaceId>> DeleteAsync(SpaceId id, Ct ct);

    /// <summary>
    /// Permanently purges a previously deleted space. This first purges all data in the
    /// space's storage pool, then removes the space record itself. The space must have
    /// been logically deleted via <see cref="DeleteAsync"/> before it can be purged.
    /// This operation is irreversible.
    /// </summary>
    /// <param name="id">The storage identifier of the space to purge.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<SpaceId>> PurgeAsync(SpaceId id, Ct ct);

    /// <summary>
    /// Queries spaces with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request with filter, sort, and pagination options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult<SpaceListItem>> QueryAsync(QueryRequest<SpaceFilter, SpaceSortField> request, Ct ct);
}
