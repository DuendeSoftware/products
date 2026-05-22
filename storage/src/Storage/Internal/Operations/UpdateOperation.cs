// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents an update operation for batch processing.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed class UpdateOperation : IStoreOperation
{
    private UpdateOperation(
        EntityType entityType,
        UuidV7 id,
        object value,
        DataStorageObjectVersion dsoVersion,
        int expectedEntityVersion,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration? expiration)
    {
        EntityType = entityType;
        Id = id;
        Value = value;
        DsoVersion = dsoVersion;
        ExpectedEntityVersion = expectedEntityVersion;
        Keys = keys;
        SearchFieldCollection = searchFieldCollection;
        Expiration = expiration;
    }

    /// <summary>
    /// Gets the entity type for this operation.
    /// </summary>
    public EntityType EntityType { get; }

    /// <summary>
    /// Gets the unique identifier for the entity to update.
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
    /// Gets the expected version for optimistic concurrency control.
    /// </summary>
    internal int ExpectedEntityVersion { get; }

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
    /// <c>null</c> means "don't change existing expiration".
    /// </summary>
    internal Expiration? Expiration { get; }

    /// <summary>
    /// Creates a new update operation for the specified DSO type.
    /// </summary>
    /// <param name="id">The unique identifier for the entity.</param>
    /// <param name="dso">The typed DSO wrapper containing the value to update.</param>
    /// <param name="expectedEntityVersion">The expected entity version for optimistic concurrency.</param>
    /// <param name="keys">The collection of keys for alternate lookups.</param>
    /// <param name="searchFieldCollection">The search field values for querying.</param>
    /// <param name="expiration">The expiration policy. <c>null</c> means "don't change".</param>
    /// <returns>A new update operation.</returns>
    public static UpdateOperation For(
        UuidV7 id,
        TypedDso dso,
        int expectedEntityVersion,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration? expiration) => new UpdateOperation(
        dso.EntityType,
        id,
        dso.Value,
        dso.Version,
        expectedEntityVersion,
        keys,
        searchFieldCollection,
        expiration);

    /// <summary>
    /// Creates a new update operation for the specified DSO type.
    /// </summary>
    /// <typeparam name="TDso">The type of the DSO to update.</typeparam>
    /// <param name="id">The unique identifier for the entity.</param>
    /// <param name="value">The new DSO value to store.</param>
    /// <param name="expectedEntityVersion">The expected version for optimistic concurrency control.</param>
    /// <param name="keys">The collection of keys for alternate lookups.</param>
    /// <param name="searchFieldCollection">The search field values for querying.</param>
    /// <param name="expiration">The expiration policy. <c>null</c> means "don't change".</param>
    /// <returns>A new update operation.</returns>
    public static UpdateOperation For<TDso>(
        UuidV7 id,
        TDso value,
        int expectedEntityVersion,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration? expiration) where TDso : IDataStorageObject => new UpdateOperation(
        TDso.DsoVersion.EntityType,
        id,
        value,
        TDso.DsoVersion,
        expectedEntityVersion,
        keys,
        searchFieldCollection,
        expiration);
}
