// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;

namespace Duende.Storage.Sqlite;

/// <summary>
/// Hands out storage that forwards every call to the real storage, except <c>UpdateAsync</c>, which throws
/// as a database would for a user without write permission.
/// </summary>
internal sealed class FailingUpdatesStorageFactory(IPartitionedStorageFactory inner) : IPartitionedStorageFactory
{
    public async Task<IPartitionedStorage> GetPartitionedStorageAsync(DataCategoryName dataCategory, Ct ct) =>
        new FailingUpdatesStorage(await inner.GetPartitionedStorageAsync(dataCategory, ct));

    private sealed class FailingUpdatesStorage(IPartitionedStorage inner) : IPartitionedStorage
    {
        public Task<UpdateResult> UpdateAsync<TDso>(
            UuidV7 id,
            TDso dso,
            int expectedEntityVersion,
            IReadOnlyCollection<DataStorageKey> keys,
            SearchFieldCollection searchFieldCollection,
            Expiration? expiration,
            IReadOnlyList<OutboxEvent> outboxEvents,
            Ct ct) where TDso : IDataStorageObject =>
            throw new InvalidOperationException("The database user can't write.");

        void IPartitionedStorage.SetPoolId(PoolId poolId) => inner.SetPoolId(poolId);

        public Task<CreateResult> CreateAsync<TDso>(
            UuidV7 id,
            TDso value,
            IReadOnlyCollection<DataStorageKey> keys,
            SearchFieldCollection searchFieldCollection,
            Expiration expiration,
            IReadOnlyList<OutboxEvent> outboxEvents,
            Ct ct) where TDso : IDataStorageObject =>
            inner.CreateAsync(id, value, keys, searchFieldCollection, expiration, outboxEvents, ct);

        public Task<StoreGetResult> TryReadAsync(EntityType type, UuidV7 id, Ct ct) => inner.TryReadAsync(type, id, ct);

        public Task<StoreGetResult> TryReadAsync(EntityType type, DataStorageKey key, Ct ct) => inner.TryReadAsync(type, key, ct);

        public Task<IReadOnlyList<StoreGetResult>> TryReadManyAsync(EntityType entityType, IReadOnlySet<UuidV7> ids, int maximum, Ct ct) =>
            inner.TryReadManyAsync(entityType, ids, maximum, ct);

        public Task<DeleteResult> DeleteAsync(EntityType entityType, UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) =>
            inner.DeleteAsync(entityType, id, outboxEvents, ct);

        public Task<DeleteResult> DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) =>
            inner.DeleteAsync(entityType, key, outboxEvents, ct);

        public Task<LinkResult> LinkAsync(LinkDefinition definition, UuidV7 leftEntityId, UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) =>
            inner.LinkAsync(definition, leftEntityId, rightEntityId, outboxEvents, ct);

        public Task<UnlinkResult> UnlinkAsync(LinkDefinition definition, UuidV7 leftEntityId, UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) =>
            inner.UnlinkAsync(definition, leftEntityId, rightEntityId, outboxEvents, ct);

        public Task<PurgeResult> PurgePoolAsync(Ct ct) => inner.PurgePoolAsync(ct);

        public Task<PurgeResult> PurgePoolAsync(int batchSize, Ct ct) => inner.PurgePoolAsync(batchSize, ct);

        public Task<BatchResult> ExecuteBatchAsync(IReadOnlyList<IStorageOperation> operations, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) =>
            inner.ExecuteBatchAsync(operations, outboxEvents, ct);

        public Task<QueryResult<MetadataEnvelope<TDso>>> QueryAsync<TDso>(
            EntityType entityType,
            IQueryExpression filter,
            SortParameter sort,
            DataRange dataRange,
            Ct ct) where TDso : IDataStorageObject =>
            inner.QueryAsync<TDso>(entityType, filter, sort, dataRange, ct);

        public Task<QueryResult<ProjectedResult>> QueryFieldsAsync(
            EntityType entityType,
            IReadOnlyCollection<Field> fields,
            IQueryExpression filter,
            SortParameter sort,
            DataRange dataRange,
            Ct ct) =>
            inner.QueryFieldsAsync(entityType, fields, filter, sort, dataRange, ct);

        public Task<QueryResult<MetadataEnvelope<TDso>>> QueryLinksAsync<TDso>(LinkQueryDescriptor query, DataRange dataRange, Ct ct)
            where TDso : IDataStorageObject =>
            inner.QueryLinksAsync<TDso>(query, dataRange, ct);

        public Task<long> CountAsync(EntityType entityType, IQueryExpression? filter, Ct ct) => inner.CountAsync(entityType, filter, ct);
    }
}
