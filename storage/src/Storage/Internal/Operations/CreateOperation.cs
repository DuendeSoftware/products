// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents a create operation for batch processing.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed class CreateOperation : IStoreOperation
{
    private CreateOperation(
        EntityType entityType,
        UuidV7 id,
        object value,
        DataStorageObjectVersion dsoVersion,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration expiration)
    {
        EntityType = entityType;
        Id = id;
        Value = value;
        DsoVersion = dsoVersion;
        Keys = keys;
        SearchFieldCollection = searchFieldCollection;
        Expiration = expiration;
    }

    /// <summary>
    /// Gets the entity type for this operation.
    /// </summary>
    public EntityType EntityType { get; }

    /// <summary>
    /// Gets the unique identifier for the entity to create.
    /// </summary>
    public UuidV7 Id { get; }

    /// <summary>
    /// Gets the DSO value to store.
    /// </summary>
    internal object Value { get; }

    /// <summary>
    /// Gets the DSO version for serialization.
    /// </summary>
    internal DataStorageObjectVersion DsoVersion { get; }

    /// <summary>
    /// Gets the collection of keys for alternate lookups.
    /// </summary>
    internal IReadOnlyCollection<DataStorageKey> Keys { get; }

    /// <summary>
    /// Gets the search field values for querying.
    /// </summary>
    internal SearchFieldCollection SearchFieldCollection { get; }

    /// <summary>
    /// Gets the expiration policy for the entity.
    /// </summary>
    internal Expiration Expiration { get; }

    /// <summary>
    /// Creates a new create operation for the specified DSO type.
    /// </summary>
    /// <param name="id">The unique identifier for the entity.</param>
    /// <param name="dso">The DSO value to store.</param>
    /// <param name="keys">The collection of keys for alternate lookups.</param>
    /// <param name="searchFieldCollection">The search field values for querying.</param>
    /// <param name="expiration">The expiration policy for the entity.</param>
    /// <returns>A new create operation.</returns>
    public static CreateOperation For(
        UuidV7 id,
        TypedDso dso,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration expiration) => new CreateOperation(
        dso.EntityType,
        id,
        dso.Value,
        dso.Version,
        keys,
        searchFieldCollection,
        expiration);

    /// <summary>
    /// Creates a new create operation for the specified DSO type.
    /// </summary>
    /// <typeparam name="TDso">The type of the DSO to create.</typeparam>
    /// <param name="id">The unique identifier for the entity.</param>
    /// <param name="value">The DSO value to store.</param>
    /// <param name="keys">The collection of keys for alternate lookups.</param>
    /// <param name="searchFieldCollection">The search field values for querying.</param>
    /// <param name="expiration">The expiration policy for the entity.</param>
    /// <returns>A new create operation.</returns>
    public static CreateOperation For<TDso>(
        UuidV7 id,
        TDso value,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration expiration) where TDso : IDataStorageObject => new CreateOperation(
        TDso.DsoVersion.EntityType,
        id,
        value,
        TDso.DsoVersion,
        keys,
        searchFieldCollection,
        expiration);
}
