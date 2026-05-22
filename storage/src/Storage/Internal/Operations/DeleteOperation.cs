// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents a delete operation for batch processing.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed class DeleteOperation : IStoreOperation
{
    private DeleteOperation(EntityType entityType, UuidV7? id, DataStorageKey? key)
    {
        EntityType = entityType;
        Id = id;
        Key = key;
    }

    /// <summary>
    /// Gets the entity type for this operation.
    /// </summary>
    public EntityType EntityType { get; }

    /// <summary>
    /// Gets the unique identifier for the entity to delete.
    /// </summary>
    public UuidV7? Id { get; }

    /// <summary>
    /// Gets the alternate key for the entity to delete.
    /// </summary>
    public DataStorageKey? Key { get; }

    /// <summary>
    /// Creates a delete operation that targets an entity by its primary ID.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <returns>A new delete operation.</returns>
    public static DeleteOperation ById(EntityType entityType, UuidV7 id) => new DeleteOperation(entityType, id, null);

    /// <summary>
    /// Creates a delete operation that targets an entity by an alternate key.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="key">The alternate key of the entity to delete.</param>
    /// <returns>A new delete operation.</returns>
    public static DeleteOperation ByKey(EntityType entityType, DataStorageKey key) => new DeleteOperation(entityType, null, key);
}
