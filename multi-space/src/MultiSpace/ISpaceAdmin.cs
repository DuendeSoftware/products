// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;

namespace Duende.MultiSpace;

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
    /// Updates an existing space. The model is mutable — callers can Get, modify, and Update.
    /// Note: <see cref="SpaceConfiguration.PoolId"/> cannot be changed via this method.
    /// Use <see cref="ChangePoolIdAsync"/> instead.
    /// </summary>
    /// <param name="id">The storage identifier.</param>
    /// <param name="space">The updated space configuration.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<SpaceId>> UpdateAsync(SpaceId id, SpaceConfiguration space, DataVersion expectedVersion, Ct ct);

    /// <summary>
    /// Changes the pool ID for the specified space. This is a potentially dangerous operation
    /// that busts all relevant caches to prevent stale routing.
    /// </summary>
    /// <param name="id">The space to change the pool for.</param>
    /// <param name="newPoolId">The new pool identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<SpaceId>> ChangePoolIdAsync(SpaceId id, PoolId newPoolId, Ct ct);

    /// <summary>
    /// Logically deletes the space with the specified ID.
    /// </summary>
    /// <param name="id">The storage identifier of the space to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SaveResult<SpaceId>> DeleteAsync(SpaceId id, Ct ct);

    /// <summary>
    /// Queries spaces with optional filtering, sorting, and pagination.
    /// </summary>
    /// <param name="request">The query request with filter, sort, and pagination options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<QueryResult<SpaceListItem>> QueryAsync(QueryRequest<SpaceFilter, SpaceSortField> request, Ct ct);
}
