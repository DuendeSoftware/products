// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Internal;

/// <summary>
/// Fake implementations of <see cref="OutboxProcessor"/>'s dependencies, shared by unit tests
/// that exercise the processor without real storage.
/// </summary>
internal sealed class FakeSubscription(SubscriberName subscriptionName) : IOutboxSubscription
{
    public SubscriberName SubscriberName { get; } = subscriptionName;
    public bool IsEnabled => true;
    public IReadOnlySet<OutboxEventName> EventNames { get; } = new HashSet<OutboxEventName>();
    public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int>();
}

internal sealed class FakeCrossPartitionStorage(IEnumerable<PersistedOutboxEvent> events) : ICrossPartitionStorage
{
    private readonly List<PersistedOutboxEvent> _events = [.. events];
    private readonly HashSet<OutboxEventId> _hiddenMessageIds = [];

    public IReadOnlyList<PersistedOutboxEvent> Events => _events;

    /// <summary>
    /// The <c>count</c> argument passed to every call of
    /// <see cref="GetOutboxEventsForSubscriptionAsync"/>, in call order.
    /// </summary>
    public List<int> ReadCounts { get; } = [];

    /// <summary>
    /// Hides the given message ids from the next page returned by
    /// <see cref="GetOutboxEventsForSubscriptionAsync"/>, without removing them from storage.
    /// </summary>
    public void HideFromNextPage(params IEnumerable<OutboxEventId> messageIds)
    {
        foreach (var messageId in messageIds)
        {
            _ = _hiddenMessageIds.Add(messageId);
        }
    }

    /// <summary>
    /// Reverses <see cref="HideFromNextPage"/>, so the given message ids are returned again by
    /// <see cref="GetOutboxEventsForSubscriptionAsync"/>, simulating a previously hidden row
    /// reappearing in the queue.
    /// </summary>
    public void RestoreToNextPage(params IEnumerable<OutboxEventId> messageIds)
    {
        foreach (var messageId in messageIds)
        {
            _ = _hiddenMessageIds.Remove(messageId);
        }
    }

    public Task<OutboxEventsPage> GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, Ct ct)
    {
        ReadCounts.Add(count);
        var page = _events.Where(e => !_hiddenMessageIds.Contains(e.MessageId)).Take(count).ToList();
        return Task.FromResult(new OutboxEventsPage(page, HasMore: false));
    }

    public Task DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct)
    {
        _ = _events.RemoveAll(e => ids.Contains(e.MessageId));
        return Task.CompletedTask;
    }

    public Task<int> PurgeExpiredAsync(int batchSize, Ct ct) => Task.FromResult(0);

    public Task<PurgeResult> PurgePoolAsync(PoolId poolId, Ct ct) => throw new NotSupportedException();

    public Task<PurgeResult> PurgePoolAsync(PoolId poolId, int batchSize, Ct ct) => throw new NotSupportedException();
}

internal sealed class TwoInstanceRouter(params StorageInstanceId[] instances) : IStorageInstanceRouter
{
    public void RegisterInstance(StorageInstanceId storageInstanceId)
    {
    }

    public void AddMapping(DataCategoryName dataCategory, StorageInstanceId storageInstanceId)
    {
    }

    public StorageInstanceId Resolve(DataCategoryName dataCategory) => instances[0];

    public IReadOnlyCollection<StorageInstanceId> GetAll() => instances;
}

internal sealed class MappedCrossPartitionStorageFactory : ICrossPartitionStorageFactory
{
    private readonly Dictionary<StorageInstanceId, ICrossPartitionStorage> _map;
    private readonly StorageInstanceId? _throwFor;

    public MappedCrossPartitionStorageFactory(params (StorageInstanceId StorageInstanceId, ICrossPartitionStorage CrossPartitionStorage)[] mappings) : this(throwFor: null, mappings)
    {
    }

    public MappedCrossPartitionStorageFactory(StorageInstanceId? throwFor, params (StorageInstanceId StorageInstanceId, ICrossPartitionStorage CrossPartitionStorage)[] mappings)
    {
        _throwFor = throwFor;
        _map = mappings.ToDictionary(m => m.StorageInstanceId, m => m.CrossPartitionStorage);
    }

    public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, Ct ct) =>
        storageInstanceId == _throwFor
            ? throw new InvalidOperationException($"Simulated failure for instance '{storageInstanceId.Value}'.")
            : Task.FromResult(_map[storageInstanceId]);
}

internal static class OutboxProcessorTestHelpers
{
    public static IServiceScopeFactory CreateScopeFactory(params (string Key, IOutboxSubscriptionHandler Handler)[] handlers)
    {
        var services = new ServiceCollection();
        foreach (var (key, handler) in handlers)
        {
            _ = services.AddKeyedSingleton(key, handler);
        }

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    public static PersistedOutboxEvent CreateEvent(SubscriberName subscriptionName, int poolId) =>
        new()
        {
            MessageId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            SequenceNumber = 1,
            EventName = OutboxEventName.Create("TestEvent"),
            SubjectId = UuidV7.New(),
            EntityTypeName = "TestEntity",
            EntityTypeId = 1,
            PoolId = poolId,
            Payload = "{}",
            SubscriberName = subscriptionName,
        };
}
