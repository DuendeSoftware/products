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
    /// Restores a previously deleted space, making it active again.
    /// Calling this on a space that is already active is a no-op success: no write occurs
    /// and the same version is returned.
    /// </summary>
    /// <remarks>
    /// Fails with one <see cref="StorageError"/> per conflict when any of the space's
    /// remembered unique extended attribute values are currently claimed by another space
    /// (code <c>eav_conflict</c>, one entry per conflicting value, enumeration is exhaustive).
    /// Fails with a <see cref="StorageError"/> per missing attribute when the current schema
    /// no longer represents an attribute stored on the deleted space (code <c>schema_drift</c>).
    /// Fails with a <see cref="StorageError"/> of code <c>not_found</c> when the space was
    /// never created, or was already purged.
    /// </remarks>
    /// <param name="id">The storage identifier of the space to restore.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <seealso cref="DeleteAsync"/>
    Task<SaveResult<SpaceId>> UndeleteAsync(SpaceId id, Ct ct);

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
