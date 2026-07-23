// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Internal.Telemetry;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;

namespace Duende.Storage.Internal;

/// <summary>
/// Decorates an <see cref="IStore"/> with tracing and metrics instrumentation.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal sealed class InstrumentedStore(IStore inner, StorageMetrics metrics, string dbSystem) : IStore
{
    /// <summary>
    /// Gets the inner store being decorated.
    /// </summary>
    public IStore Inner => inner;

    /// <inheritdoc />
    public void SetPoolId(PoolId poolId) => inner.SetPoolId(poolId);

    public async Task<CreateResult> CreateAsync<TDso>(
        UuidV7 id,
        TDso value,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct) where TDso : IDataStorageObject
    {
        var entityType = TDso.DsoVersion.EntityType.Name;
        using var activity = StartActivity("Store.Create", entityType, StorageTelemetryConstants.Operations.Create);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.CreateAsync(id, value, keys, searchFieldCollection, expiration, outboxEvents, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Create, dbSystem, entityType);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Create, dbSystem, ex, entityType);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Create, Elapsed(start), dbSystem, result, entityType);
        }
    }

    public async Task<StoreGetResult> TryReadAsync(EntityType type, UuidV7 id, Ct ct)
    {
        var entityType = type.Name;
        using var activity = StartActivity("Store.Read", entityType, StorageTelemetryConstants.Operations.Read);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.TryReadAsync(type, id, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Read, dbSystem, entityType);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Read, dbSystem, ex, entityType);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Read, Elapsed(start), dbSystem, result, entityType);
        }
    }

    public async Task<StoreGetResult> TryReadAsync(EntityType type, DataStorageKey key, Ct ct)
    {
        var entityType = type.Name;
        using var activity = StartActivity("Store.Read", entityType, StorageTelemetryConstants.Operations.Read);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.TryReadAsync(type, key, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Read, dbSystem, entityType);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Read, dbSystem, ex, entityType);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Read, Elapsed(start), dbSystem, result, entityType);
        }
    }

    public async Task<IReadOnlyList<StoreGetResult>> TryReadManyAsync(
        EntityType entityType,
        IReadOnlySet<UuidV7> ids,
        int maximum,
        Ct ct)
    {
        var entityTypeName = entityType.Name;
        using var activity = StartActivity("Store.ReadMany", entityTypeName, StorageTelemetryConstants.Operations.ReadMany);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.TryReadManyAsync(entityType, ids, maximum, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.ReadMany, dbSystem, entityTypeName);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.ReadMany, dbSystem, ex, entityTypeName);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.ReadMany, Elapsed(start), dbSystem, result, entityTypeName);
        }
    }

    public async Task<UpdateResult> UpdateAsync<TDso>(
        UuidV7 id,
        TDso dso,
        int expectedEntityVersion,
        IReadOnlyCollection<DataStorageKey> keys,
        SearchFieldCollection searchFieldCollection,
        Expiration? expiration,
        IReadOnlyList<OutboxEvent> outboxEvents,
        Ct ct) where TDso : IDataStorageObject
    {
        var entityType = TDso.DsoVersion.EntityType.Name;
        using var activity = StartActivity("Store.Update", entityType, StorageTelemetryConstants.Operations.Update);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.UpdateAsync(id, dso, expectedEntityVersion, keys, searchFieldCollection, expiration, outboxEvents, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Update, dbSystem, entityType);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Update, dbSystem, ex, entityType);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Update, Elapsed(start), dbSystem, result, entityType);
        }
    }

    public async Task<DeleteResult> DeleteAsync(EntityType entityType, UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        var entityTypeName = entityType.Name;
        using var activity = StartActivity("Store.Delete", entityTypeName, StorageTelemetryConstants.Operations.Delete);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.DeleteAsync(entityType, id, outboxEvents, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Delete, dbSystem, entityTypeName);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Delete, dbSystem, ex, entityTypeName);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Delete, Elapsed(start), dbSystem, result, entityTypeName);
        }
    }

    public async Task<DeleteResult> DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        var entityTypeName = entityType.Name;
        using var activity = StartActivity("Store.Delete", entityTypeName, StorageTelemetryConstants.Operations.Delete);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.DeleteAsync(entityType, key, outboxEvents, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Delete, dbSystem, entityTypeName);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Delete, dbSystem, ex, entityTypeName);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Delete, Elapsed(start), dbSystem, result, entityTypeName);
        }
    }

    public async Task<LinkResult> LinkAsync(LinkDefinition definition, UuidV7 leftEntityId, UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        using var activity = StartActivity("Store.Link", null, StorageTelemetryConstants.Operations.Link);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.LinkAsync(definition, leftEntityId, rightEntityId, outboxEvents, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Link, dbSystem, null);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Link, dbSystem, ex, null);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Link, Elapsed(start), dbSystem, result, null);
        }
    }

    public async Task<UnlinkResult> UnlinkAsync(LinkDefinition definition, UuidV7 leftEntityId, UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        using var activity = StartActivity("Store.Unlink", null, StorageTelemetryConstants.Operations.Unlink);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.UnlinkAsync(definition, leftEntityId, rightEntityId, outboxEvents, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Unlink, dbSystem, null);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Unlink, dbSystem, ex, null);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Unlink, Elapsed(start), dbSystem, result, null);
        }
    }

    public async Task<int> PurgeExpiredAsync(int batchSize, Ct ct)
    {
        using var activity = StartActivity("Store.PurgeExpired", null, StorageTelemetryConstants.Operations.PurgeExpired);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.PurgeExpiredAsync(batchSize, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.PurgeExpired, dbSystem, null);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.PurgeExpired, dbSystem, ex, null);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.PurgeExpired, Elapsed(start), dbSystem, result, null);
        }
    }

    public Task<PurgeResult> PurgePoolAsync(Ct ct) => PurgePoolAsync(StorageConstants.PurgePoolDefaultBatchSize, ct);

    public async Task<PurgeResult> PurgePoolAsync(int batchSize, Ct ct)
    {
        using var activity = StartActivity("Store.PurgePool", null, StorageTelemetryConstants.Operations.PurgePool);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.PurgePoolAsync(batchSize, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.PurgePool, dbSystem, null);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.PurgePool, dbSystem, ex, null);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.PurgePool, Elapsed(start), dbSystem, result, null);
        }
    }

    public async Task<BatchResult> ExecuteBatchAsync(IReadOnlyList<IStoreOperation> operations, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct)
    {
        using var activity = StartActivity("Store.ExecuteBatch", null, StorageTelemetryConstants.Operations.Batch);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.ExecuteBatchAsync(operations, outboxEvents, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Batch, dbSystem, null);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Batch, dbSystem, ex, null);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Batch, Elapsed(start), dbSystem, result, null);
        }
    }

    public async Task<OutboxEventsPage> GetOutboxEventsForSubscriberAsync(SubscriberName subscriberName, int count, Ct ct)
    {
        using var activity = StartActivity("Store.GetOutboxEvents", null, StorageTelemetryConstants.Operations.OutboxGet);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.GetOutboxEventsForSubscriberAsync(subscriberName, count, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.OutboxGet, dbSystem, null);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.OutboxGet, dbSystem, ex, null);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.OutboxGet, Elapsed(start), dbSystem, result, null);
        }
    }

    public async Task DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct)
    {
        using var activity = StartActivity("Store.DeleteOutboxEvents", null, StorageTelemetryConstants.Operations.OutboxDelete);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            await inner.DeleteOutboxEventsAsync(ids, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.OutboxDelete, dbSystem, null);
            succeeded = true;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.OutboxDelete, dbSystem, ex, null);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.OutboxDelete, Elapsed(start), dbSystem, result, null);
        }
    }

    public async Task<QueryResult<MetadataEnvelope<TDso>>> QueryAsync<TDso>(
       EntityType entityType,
       IQueryExpression filter,
       SortParameter sort,
       DataRange dataRange,
       Ct ct) where TDso : IDataStorageObject
    {
        var entityTypeName = entityType.Name;
        using var activity = StartActivity("QueryStore.Query", entityTypeName, StorageTelemetryConstants.Operations.Query);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.QueryAsync<TDso>(entityType, filter, sort, dataRange, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Query, dbSystem, entityTypeName);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Query, dbSystem, ex, entityTypeName);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Query, Elapsed(start), dbSystem, result, entityTypeName);
        }
    }

    public async Task<QueryResult<ProjectedResult>> QueryFieldsAsync(
        EntityType entityType,
        IReadOnlyCollection<Field> fields,
        IQueryExpression filter,
        SortParameter sort,
        DataRange dataRange,
        Ct ct)
    {
        var entityTypeName = entityType.Name;
        using var activity = StartActivity("QueryStore.QueryFields", entityTypeName, StorageTelemetryConstants.Operations.QueryFields);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.QueryFieldsAsync(entityType, fields, filter, sort, dataRange, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.QueryFields, dbSystem, entityTypeName);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.QueryFields, dbSystem, ex, entityTypeName);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.QueryFields, Elapsed(start), dbSystem, result, entityTypeName);
        }
    }

    public async Task<QueryResult<MetadataEnvelope<TDso>>> QueryLinksAsync<TDso>(
        LinkQueryDescriptor query,
        DataRange dataRange,
        Ct ct) where TDso : IDataStorageObject
    {
        using var activity = StartActivity("QueryStore.QueryLinks", null, StorageTelemetryConstants.Operations.QueryLinks);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.QueryLinksAsync<TDso>(query, dataRange, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.QueryLinks, dbSystem, null);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.QueryLinks, dbSystem, ex, null);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.QueryLinks, Elapsed(start), dbSystem, result, null);
        }
    }

    public async Task<long> CountAsync(EntityType entityType, IQueryExpression? filter, Ct ct)
    {
        var entityTypeName = entityType.Name;
        using var activity = StartActivity("QueryStore.Count", entityTypeName, StorageTelemetryConstants.Operations.Count);
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var result = await inner.CountAsync(entityType, filter, ct);
            metrics.RecordSuccess(StorageTelemetryConstants.Operations.Count, dbSystem, entityTypeName);
            succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            metrics.RecordError(StorageTelemetryConstants.Operations.Count, dbSystem, ex, entityTypeName);
            throw;
        }
        finally
        {
            var result = succeeded ? StorageTelemetryConstants.TagValues.Success : StorageTelemetryConstants.TagValues.Error;
            metrics.RecordDuration(StorageTelemetryConstants.Operations.Count, Elapsed(start), dbSystem, result, entityTypeName);
        }
    }

    private Activity? StartActivity(string name, string? entityType, string operation)
    {
        var activity = StorageTracing.ActivitySource.StartActivity(name);
        if (activity is not null)
        {
            _ = activity.SetTag(StorageTelemetryConstants.Tags.DbSystem, dbSystem);
            _ = activity.SetTag(StorageTelemetryConstants.Tags.Operation, operation);
            if (entityType is not null)
            {
                _ = activity.SetTag(StorageTelemetryConstants.Tags.EntityType, entityType);
            }
        }

        return activity;
    }

    private static void RecordException(Activity? activity, Exception ex)
    {
        if (activity is not null)
        {
            _ = activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            _ = activity.SetTag(StorageTelemetryConstants.Tags.ErrorType, ex.GetType().Name);
        }
    }

    private static double Elapsed(long start) => Stopwatch.GetElapsedTime(start).TotalSeconds;
}
