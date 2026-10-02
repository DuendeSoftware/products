// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace UnitTests.Hosting;

public class StoragePurgeHostTests
{
    private readonly IdentityServerOptions _options = new();
    private readonly ILogger<StoragePurgeHost> _logger = TestLogger.Create<StoragePurgeHost>();

    [Fact]
    public async Task disabled_should_not_start()
    {
        _options.StoragePurge.EnablePurge = false;

        var (storageInstanceRouter, crossPartitionStorageFactory) = CreateStoreFactory();
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // StartAsync returns Task.CompletedTask when disabled
        await host.StartAsync(cts.Token);
        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task run_purge_against_empty_store_completes_without_error()
    {
        _options.StoragePurge.EnablePurge = true;
        _options.StoragePurge.BatchSize = 100;

        var (storageInstanceRouter, crossPartitionStorageFactory) = CreateStoreFactory();
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_disabled_should_short_circuit()
    {
        _options.StoragePurge.EnablePurge = false;

        var (storageInstanceRouter, crossPartitionStorageFactory) = CreateStoreFactory();
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_should_survive_store_factory_exception()
    {
        _options.StoragePurge.EnablePurge = true;

        var storageInstanceRouter = new SimpleStorageInstanceRouter();
        var crossPartitionStorageFactory = new ThrowingCrossPartitionStorageFactory();
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_respects_cancellation()
    {
        _options.StoragePurge.EnablePurge = true;

        var (storageInstanceRouter, crossPartitionStorageFactory) = CreateStoreFactory();
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(cts.Token);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_clamps_invalid_batch_size()
    {
        _options.StoragePurge.EnablePurge = true;
        _options.StoragePurge.BatchSize = 0; // Below minimum — should be clamped to 1

        var (storageInstanceRouter, crossPartitionStorageFactory) = CreateStoreFactory();
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_clamps_oversized_batch()
    {
        _options.StoragePurge.EnablePurge = true;
        _options.StoragePurge.BatchSize = 5000; // Above maximum — should be clamped to 1000

        var (storageInstanceRouter, crossPartitionStorageFactory) = CreateStoreFactory();
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_continues_to_the_next_instance_when_an_earlier_instance_throws()
    {
        _options.StoragePurge.EnablePurge = true;
        _options.StoragePurge.BatchSize = 100;

        var failingStorageInstanceId = StorageInstanceId.Create("failing-instance");
        var healthyStorageInstanceId = StorageInstanceId.Create("healthy-instance");
        var healthyStorage = new CountingCrossPartitionStorage();
        var storageInstanceRouter = new TwoInstanceRouter(failingStorageInstanceId, healthyStorageInstanceId);
        var crossPartitionStorageFactory = new SelectivelyThrowingCrossPartitionStorageFactory(
            throwFor: failingStorageInstanceId,
            (healthyStorageInstanceId, healthyStorage));
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull("a failing instance must not abort purging of the remaining instances");
        healthyStorage.PurgeCallCount.ShouldBe(1, "the instance after the failing one must still be purged");
    }

    [Fact]
    public async Task run_purge_continues_to_the_next_instance_when_an_earlier_instance_fails_during_purge()
    {
        _options.StoragePurge.EnablePurge = true;
        _options.StoragePurge.BatchSize = 100;

        var failingStorageInstanceId = StorageInstanceId.Create("purge-failing-instance");
        var healthyStorageInstanceId = StorageInstanceId.Create("purge-healthy-instance");
        var failingStorage = new ThrowingPurgeCrossPartitionStorage();
        var healthyStorage = new CountingCrossPartitionStorage();
        var storageInstanceRouter = new TwoInstanceRouter(failingStorageInstanceId, healthyStorageInstanceId);
        var crossPartitionStorageFactory = new MappedCrossPartitionStorageFactory(
            (failingStorageInstanceId, failingStorage),
            (healthyStorageInstanceId, healthyStorage));
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull("a failing instance must not abort purging of the remaining instances");
        healthyStorage.PurgeCallCount.ShouldBe(1, "the instance after the failing one must still be purged");
    }

    [Fact]
    public async Task run_purge_continues_to_the_next_instance_when_an_earlier_instance_throws_an_unrequested_operation_canceled_exception()
    {
        _options.StoragePurge.EnablePurge = true;
        _options.StoragePurge.BatchSize = 100;

        // Simulates a store-internal cancellation (e.g. a command timeout using its own linked
        // token) that is unrelated to the caller-supplied ct. This must not be mistaken for a
        // requested shutdown: it must be logged as a failure and purging must continue with the
        // remaining instances, not silently return.
        var failingStorageInstanceId = StorageInstanceId.Create("oce-failing-instance");
        var healthyStorageInstanceId = StorageInstanceId.Create("oce-healthy-instance");
        var failingStorage = new UnrequestedCancellationCrossPartitionStorage();
        var healthyStorage = new CountingCrossPartitionStorage();
        var storageInstanceRouter = new TwoInstanceRouter(failingStorageInstanceId, healthyStorageInstanceId);
        var crossPartitionStorageFactory = new MappedCrossPartitionStorageFactory(
            (failingStorageInstanceId, failingStorage),
            (healthyStorageInstanceId, healthyStorage));
        var host = new StoragePurgeHost(storageInstanceRouter, crossPartitionStorageFactory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull("an unrequested OperationCanceledException must not abort purging of the remaining instances");
        healthyStorage.PurgeCallCount.ShouldBe(1, "the instance after the one throwing an unrequested OperationCanceledException must still be purged");
    }

    private static (IStorageInstanceRouter storageInstanceRouter, ICrossPartitionStorageFactory crossPartitionStorageFactory) CreateStoreFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var dbName = $"purge_test_{Guid.NewGuid():N}";
        services.AddStorageInternal(storage =>
            storage.AddSqlite(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        var sp = services.BuildServiceProvider();
        var schema = sp.GetRequiredKeyedService<Duende.Storage.Schema.IStorageInstanceSchema>(StorageInstanceId.Default);
        schema.MigrateAsync(CancellationToken.None).GetAwaiter().GetResult();

        return (new SimpleStorageInstanceRouter(), new SimpleCrossPartitionStorageFactory(sp));
    }

    private sealed class SimpleStorageInstanceRouter : IStorageInstanceRouter
    {
        public void RegisterInstance(StorageInstanceId storageInstanceId)
        {
        }

        public void AddMapping(DataCategoryName dataCategory, StorageInstanceId storageInstanceId)
        {
        }

        public StorageInstanceId Resolve(DataCategoryName dataCategory) => StorageInstanceId.Default;

        public IReadOnlyCollection<StorageInstanceId> GetAll() => [StorageInstanceId.Default];
    }

    private sealed class SimpleCrossPartitionStorageFactory(IServiceProvider services) : ICrossPartitionStorageFactory
    {
        public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, CancellationToken ct) =>
            Task.FromResult(services.GetRequiredKeyedService<ICrossPartitionStorage>(storageInstanceId));
    }

    private sealed class ThrowingCrossPartitionStorageFactory : ICrossPartitionStorageFactory
    {
        public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, CancellationToken ct) =>
            throw new InvalidOperationException("Simulated factory failure");
    }

    private sealed class TwoInstanceRouter(params StorageInstanceId[] instances) : IStorageInstanceRouter
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

    private sealed class SelectivelyThrowingCrossPartitionStorageFactory(
        StorageInstanceId throwFor,
        params (StorageInstanceId StorageInstanceId, ICrossPartitionStorage CrossPartitionStorage)[] mappings) : ICrossPartitionStorageFactory
    {
        private readonly Dictionary<StorageInstanceId, ICrossPartitionStorage> _map = mappings.ToDictionary(m => m.StorageInstanceId, m => m.CrossPartitionStorage);

        public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, CancellationToken ct) =>
            storageInstanceId == throwFor
                ? throw new InvalidOperationException($"Simulated factory failure for instance '{storageInstanceId.Value}'.")
                : Task.FromResult(_map[storageInstanceId]);
    }

    private sealed class MappedCrossPartitionStorageFactory(params (StorageInstanceId StorageInstanceId, ICrossPartitionStorage CrossPartitionStorage)[] mappings) : ICrossPartitionStorageFactory
    {
        private readonly Dictionary<StorageInstanceId, ICrossPartitionStorage> _map = mappings.ToDictionary(m => m.StorageInstanceId, m => m.CrossPartitionStorage);

        public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, CancellationToken ct) =>
            Task.FromResult(_map[storageInstanceId]);
    }

    private sealed class CountingCrossPartitionStorage : ICrossPartitionStorage
    {
        private int _purgeCallCount;

        public int PurgeCallCount => Volatile.Read(ref _purgeCallCount);

        public Task<OutboxEventsPage> GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, CancellationToken ct) =>
            Task.FromResult(new OutboxEventsPage([], HasMore: false));

        public Task DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, CancellationToken ct) => Task.CompletedTask;

        public Task<int> PurgeExpiredAsync(int batchSize, CancellationToken ct)
        {
            _ = Interlocked.Increment(ref _purgeCallCount);
            return Task.FromResult(0);
        }
    }

    private sealed class ThrowingPurgeCrossPartitionStorage : ICrossPartitionStorage
    {
        public Task<OutboxEventsPage> GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, CancellationToken ct) =>
            Task.FromResult(new OutboxEventsPage([], HasMore: false));

        public Task DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, CancellationToken ct) => Task.CompletedTask;

        public Task<int> PurgeExpiredAsync(int batchSize, CancellationToken ct) =>
            throw new InvalidOperationException("Simulated purge failure");
    }

    private sealed class UnrequestedCancellationCrossPartitionStorage : ICrossPartitionStorage
    {
        public Task<OutboxEventsPage> GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, CancellationToken ct) =>
            Task.FromResult(new OutboxEventsPage([], HasMore: false));

        public Task DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, CancellationToken ct) => Task.CompletedTask;

        public Task<int> PurgeExpiredAsync(int batchSize, CancellationToken ct)
        {
            // Throws an OperationCanceledException whose token is unrelated to (and not
            // cancelled via) the caller-supplied ct, simulating a store-internal timeout. The
            // passed-in ct is deliberately left untouched/uncancelled.
            using var unrelatedCts = new CancellationTokenSource();
            unrelatedCts.Cancel();
            throw new OperationCanceledException("Simulated store-internal timeout", unrelatedCts.Token);
        }
    }
}



