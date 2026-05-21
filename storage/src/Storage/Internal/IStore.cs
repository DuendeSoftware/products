// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;

namespace Duende.Storage.Internal;

public interface IStore
{
    internal void SetPoolId(PoolId poolId);

    /// <summary>
    /// Creates a new entity in the store, writing outbox events atomically.
    /// If the resolved expiration is already in the past, the entity is not stored (noop) and
    /// <see cref="CreateResult.Success"/> is returned.
    /// </summary>
    /// <typeparam name="TDso">The type of the DSO to create.</typeparam>
    /// <param name="id">The unique identifier for the entity.</param>
    /// <param name="value">The DSO value to store.</param>
    /// <param name="keys">The collection of keys for alternate lookups.</param>
    /// <param name="searchFieldCollection">Optional search field values that can be used for querying.</param>
    /// <param name="expiration">The expiration policy for the entity.
    /// Use <see cref="Expiration.NoExpiration"/> for entities that should never expire.</param>
    /// <param name="outboxEvents">Outbox events to INSERT atomically within the same transaction.
    /// Pass <c>[]</c> when no events are needed.
    /// Silently ignored when the outbox is not enabled.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The result of the create operation.</returns>
    public Task<CreateResult> CreateAsync<TDso>(
        UuidV7 id,
        TDso value,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct) where TDso : IDataStorageObject;

    /// <summary>
    /// Attempts to retrieve an entity from the store by its unique identifier.
    /// </summary>
    /// <param name="type">The type of entity to retrieve.</param>
    /// <param name="id">The unique identifier of the entity.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="StoreGetResult"/> indicating whether the entity was found and, if so, its value and version.</returns>
    public Task<StoreGetResult> TryReadAsync(
        EntityType type,
        UuidV7 id,
        Ct ct);

    /// <summary>
    /// Attempts to retrieve an entity from the store by an alternate key.
    /// </summary>
    /// <param name="type">The type of entity to retrieve.</param>
    /// <param name="key">The alternate key to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="StoreGetResult"/> indicating whether the entity was found and, if so, its value and version.</returns>
    public Task<StoreGetResult> TryReadAsync(
        EntityType type,
        DataStorageKey key,
        Ct ct);

    /// <summary>
    /// Retrieves multiple entities from the store by their unique identifiers.
    /// Only entities that exist in the store are included in the result;
    /// IDs that do not match any stored entity are silently omitted.
    /// </summary>
    /// <param name="entityType">The type of entities to retrieve.</param>
    /// <param name="ids">The unique identifiers of the entities to retrieve. Using a set ensures no duplicate IDs are requested.</param>
    /// <param name="maximum">The maximum number of IDs allowed in a single request.
    /// An <see cref="InvalidOperationException"/> is thrown if the count of <paramref name="ids"/> exceeds this value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A list of <see cref="StoreGetResult"/> containing only the entities that were found.
    /// The result list may contain fewer items than <paramref name="ids"/> if some IDs
    /// do not exist in the store. Returns an empty list if no matching entities are found
    /// or if <paramref name="ids"/> is empty.
    /// </returns>
    public Task<IReadOnlyList<StoreGetResult>> TryReadManyAsync(
        EntityType entityType,
        IReadOnlySet<UuidV7> ids,
        int maximum,
        Ct ct);

    /// <summary>
    /// Updates an existing entity in the store, writing outbox events atomically.
    /// The expiration value is stored as provided. Expired records remain visible on reads
    /// (TTL is best-effort) and are removed by the background purge job.
    /// </summary>
    /// <typeparam name="TDso">The type of the DSO to update.</typeparam>
    /// <param name="id">The unique identifier for the entity.</param>
    /// <param name="dso">The new DSO value to store.</param>
    /// <param name="expectedEntityVersion">The expected version for optimistic concurrency control.</param>
    /// <param name="keys">The collection of keys for alternate lookups.</param>
    /// <param name="searchFieldCollection">Optional search field values that can be used for querying. 
    /// These values replace any existing search fields for this entity.</param>
    /// <param name="expiration">The expiration policy for the entity.
    /// <c>null</c> means "don't change existing expiration".
    /// <see cref="Expiration.NoExpiration"/> explicitly clears any existing expiration.
    /// <see cref="Expiration.AtAbsolute"/> or <see cref="Expiration.InRelative"/> sets a new expiration.</param>
    /// <param name="outboxEvents">Outbox events to INSERT atomically within the same transaction.
    /// Pass <c>[]</c> when no events are needed.
    /// Silently ignored when the outbox is not enabled.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The result of the update operation.</returns>
    public Task<UpdateResult> UpdateAsync<TDso>(
        UuidV7 id,
        TDso dso,
        int expectedEntityVersion,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration? expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct) where TDso : IDataStorageObject;

    /// <summary>
    /// Deletes an entity from the store by its unique identifier, writing outbox events atomically.
    /// </summary>
    /// <param name="entityType">The type of entity to delete.</param>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="outboxEvents">Outbox events to INSERT atomically within the same transaction.
    /// Pass <c>[]</c> when no events are needed.
    /// Silently ignored when the outbox is not enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the delete operation.</returns>
    public Task<DeleteResult> DeleteAsync(EntityType entityType, UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct);

    /// <summary>
    /// Deletes an entity from the store by an alternate key, writing outbox events atomically.
    /// </summary>
    /// <param name="entityType">The type of entity to delete.</param>
    /// <param name="key">The alternate key identifying the entity to delete.</param>
    /// <param name="outboxEvents">Outbox events to INSERT atomically within the same transaction.
    /// Pass <c>[]</c> when no events are needed.
    /// Silently ignored when the outbox is not enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the delete operation.</returns>
    public Task<DeleteResult> DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct);

    /// <summary>
    /// Creates a link between two entities, writing outbox events atomically.
    /// The link is unique per (LinkType, LeftId, RightId).
    /// No referential integrity check — the entities do not need to exist.
    /// </summary>
    /// <param name="definition">The link definition describing the relationship schema.</param>
    /// <param name="leftEntityId">The ID of the left-side entity.</param>
    /// <param name="rightEntityId">The ID of the right-side entity.</param>
    /// <param name="outboxEvents">Outbox events to INSERT atomically within the same transaction.
    /// Pass <c>[]</c> when no events are needed.
    /// Silently ignored when the outbox is not enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="LinkResult.Success"/> if created, <see cref="LinkResult.AlreadyLinked"/> if the exact link already exists.</returns>
    Task<LinkResult> LinkAsync(LinkDefinition definition, UuidV7 leftEntityId, UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct);

    /// <summary>
    /// Removes a link between two entities, writing outbox events atomically.
    /// Returns success even if the link did not exist (idempotent).
    /// </summary>
    /// <param name="definition">The link definition describing the relationship schema.</param>
    /// <param name="leftEntityId">The ID of the left-side entity.</param>
    /// <param name="rightEntityId">The ID of the right-side entity.</param>
    /// <param name="outboxEvents">Outbox events to INSERT atomically within the same transaction.
    /// Pass <c>[]</c> when no events are needed.
    /// Silently ignored when the outbox is not enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Always returns <see cref="UnlinkResult.Success"/>.</returns>
    Task<UnlinkResult> UnlinkAsync(LinkDefinition definition, UuidV7 leftEntityId, UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct);

    /// <summary>
    /// Purges a batch of expired entities atomically. Within a single transaction:
    /// locks expired rows, deletes associated entity links, and deletes expired entities.
    /// </summary>
    /// <param name="batchSize">Max expired entities to process (1–1000).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of entities deleted.</returns>
    Task<int> PurgeExpiredAsync(int batchSize, Ct ct);

    /// <summary>
    /// Executes multiple operations atomically in a single transaction, writing outbox events atomically.
    /// Operations are executed in order until completion or first failure.
    /// If any operation fails, execution stops immediately and all operations are rolled back.
    /// Outbox events are INSERTed after all operations succeed but before the transaction is committed.
    /// </summary>
    /// <param name="operations">The operations to execute.</param>
    /// <param name="outboxEvents">Outbox events to INSERT atomically within the same transaction,
    /// after all operations succeed. Pass <c>[]</c> when no events are needed.
    /// Silently ignored when the outbox is not enabled.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A BatchResult indicating overall success/failure with per-operation outcomes.
    /// When Success is false, Results contains outcomes only for operations attempted
    /// (up to and including the failed operation). No changes have been persisted.
    /// </returns>
    Task<BatchResult> ExecuteBatchAsync(IReadOnlyList<IStoreOperation> operations, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct);

    /// <summary>
    /// Retrieves the oldest page of outbox events for a specific subscriber, ordered by sequence number.
    /// The intended usage pattern is: get oldest page → process events → delete them → repeat.
    /// </summary>
    /// <param name="subscriberName">The subscriber name to filter events for.</param>
    /// <param name="count">The maximum number of events to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of outbox events and whether more events exist beyond this page.</returns>
    Task<OutboxEventsPage> GetOutboxEventsForSubscriberAsync(SubscriberName subscriberName, int count, Ct ct);

    /// <summary>
    /// Deletes outbox events by their message IDs (the store-generated per-row identifiers).
    /// </summary>
    /// <param name="ids">The message IDs of the outbox events to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct);

    /// <summary>
    /// Queries entities with the specified pagination strategy.
    /// </summary>
    /// <typeparam name="TDso">The type of the DSO to query.</typeparam>
    /// <param name="entityType">The entity type to query.</param>
    /// <param name="filter">The filter expression to apply.</param>
    /// <param name="sort">Sort parameter. Use SortParameter.Empty if no sorting is required.</param>
    /// <param name="dataRange">The pagination strategy (page, offset, or continuation token).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A query result containing items and pagination metadata.</returns>
    Task<QueryResult<MetadataEnvelope<TDso>>> QueryAsync<TDso>(
        EntityType entityType,
        IQueryExpression filter,
        SortParameter sort,
        DataRange dataRange,
        Ct ct) where TDso : IDataStorageObject;

    /// <summary>
    /// Queries for specific field values with the specified pagination strategy.
    /// Returns projected results instead of full entities.
    /// </summary>
    /// <param name="entityType">The entity type to query.</param>
    /// <param name="fields">The fields to project in the results.</param>
    /// <param name="filter">The filter expression to apply.</param>
    /// <param name="sort">Sort parameter. Use SortParameter.Empty if no sorting is required.</param>
    /// <param name="dataRange">The pagination strategy (page, offset, or continuation token).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A query result containing projected field values and pagination metadata.</returns>
    Task<QueryResult<ProjectedResult>> QueryFieldsAsync(
        EntityType entityType,
        IReadOnlyCollection<Field> fields,
        IQueryExpression filter,
        SortParameter sort,
        DataRange dataRange,
        Ct ct);

    /// <summary>
    /// Queries for entities reachable via a chain of link traversals.
    /// </summary>
    /// <typeparam name="TDso">The source entity type to return.</typeparam>
    /// <param name="query">The link query descriptor describing the traversal chain and filter.</param>
    /// <param name="dataRange">The pagination strategy.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A query result containing entities of the source type reachable via the link chain.</returns>
    Task<QueryResult<MetadataEnvelope<TDso>>> QueryLinksAsync<TDso>(
        LinkQueryDescriptor query,
        DataRange dataRange,
        Ct ct) where TDso : IDataStorageObject;

    /// <summary>
    /// Counts entities matching the specified filter, or all entities if no filter is provided.
    /// </summary>
    /// <param name="entityType">The entity type to count.</param>
    /// <param name="filter">Optional filter expression. If null, counts all entities of the specified type.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The number of matching entities.</returns>
    Task<long> CountAsync(
        EntityType entityType,
        IQueryExpression? filter,
        Ct ct);
}
