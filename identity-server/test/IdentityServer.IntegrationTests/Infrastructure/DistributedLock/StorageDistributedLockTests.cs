// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.Stores.Storage;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Duende.Spaces;

namespace Duende.IdentityServer.IntegrationTests.Infrastructure.DistributedLock;

/// <summary>
/// Exercises <see cref="StorageDistributedLock"/> end to end against a real SQLite database shared by
/// several "nodes" (independent <see cref="ServiceProvider"/>s), all driven by a single
/// <see cref="FakeTimeProvider"/> so renewal, expiry, backoff and shutdown timing is deterministic.
/// </summary>
/// <remarks>
/// Decorating the underlying store for the tests that need a blocked, failing, or counting store
/// goes through <see cref="IDistributedLockStorage"/>: tests construct <see cref="StorageDistributedLock"/>
/// directly with a decorator in place of <see cref="DefaultDistributedLockStorage"/>. This indirection
/// exists because Duende.Storage's <c>IPartitionedStorage</c> declares an <c>internal</c> member
/// (<c>SetPoolId</c>) that only Duende.Storage's own assembly can implement, and IdentityServer
/// consumes Duende.Storage as a NuGet package rather than a project reference, so a hand-written
/// decorator for the full interface isn't possible from this test assembly.
/// </remarks>
public sealed class StorageDistributedLockTests : IAsyncDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"distributed-lock-{Guid.NewGuid():N}.db");
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly List<ServiceProvider> _providers = [];
    private bool _migrated;

    public async ValueTask DisposeAsync()
    {
        foreach (var provider in _providers)
        {
            await provider.DisposeAsync();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        TryDelete(_dbPath);
        TryDelete($"{_dbPath}-wal");
        TryDelete($"{_dbPath}-shm");
        TryDelete($"{_dbPath}-journal");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    /// <summary>
    /// Builds a node's own <see cref="ManagementStorageAccessor"/> sharing the test's SQLite file,
    /// migrating the schema once. Storage only — no Spaces — so lock records land in the Default
    /// instance's Management pool.
    /// </summary>
    private async Task<ManagementStorageAccessor> BuildNodeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        services.AddStorageInternal(storage => storage.AddSqlite(o => o.ConnectionString = $"Data Source={_dbPath}"));
        services.AddDsoRegistration<DistributedLockDso.V1>();
        services.TryAddTransient<DefaultPartitionedStorageFactory>();
        services.TryAddTransient<ManagementStorageAccessor>();

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        if (!_migrated)
        {
            await provider.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(_ct);
            _migrated = true;
        }

        return provider.GetRequiredService<ManagementStorageAccessor>();
    }

    private async Task<StorageDistributedLock> BuildLockAsync(IHostApplicationLifetime? lifetime = null, IDistributedLockStorage? storageOverride = null)
    {
        var accessor = await BuildNodeAsync();
        var storage = storageOverride ?? new DefaultDistributedLockStorage(accessor);
        return new StorageDistributedLock(storage, _time, NullLogger<StorageDistributedLock>.Instance, lifetime);
    }

    private static LeaseTiming ShortTiming(TimeSpan duration) =>
        new(duration, duration / 3, TimeSpan.FromSeconds(1));

    // Acquire race: 2 providers x 10 parallel TryAcquireAsync(name, 0) -> exactly one non-null.
    [Fact]
    public async Task Acquire_race_across_two_nodes_yields_exactly_one_holder()
    {
        await using var lockA = await BuildLockAsync();
        await using var lockB = await BuildLockAsync();
        var name = LockName.OutboxProcessor;

        var tasks = new List<Task<ILockLease?>>();
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(lockA.TryAcquireAsync(name, TimeSpan.Zero, _ct));
            tasks.Add(lockB.TryAcquireAsync(name, TimeSpan.Zero, _ct));
        }

        var results = await Task.WhenAll(tasks);
        var held = results.Where(r => r is not null).ToArray();
        held.Length.ShouldBe(1);

        await held[0]!.DisposeAsync();
    }

    // Busy until expiry, then takeover, and A's Lost fires. Renewal is "blocked" by using a very
    // short lease duration and simply never letting the owner's renewal loop run (we never advance
    // less than RenewalInterval, we jump straight past ExpiresAtUtc).
    [Fact]
    public async Task Busy_until_expiry_then_takeover_and_lost_fires()
    {
        await using var lockA = await BuildLockAsync();
        await using var lockB = await BuildLockAsync();
        var name = LockName.SigningKeys(SpaceId.Default);
        var timing = ShortTiming(TimeSpan.FromSeconds(5));

        var leaseA = await lockA.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        leaseA.ShouldNotBeNull();

        var busy = await lockB.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        busy.ShouldBeNull();

        // Advance past expiry without letting A renew: stop A's renewal by never giving it a chance
        // to run (jump the whole duration in one hop; FakeTimeProvider fires due timers synchronously
        // on Advance, so give the renewal loop no interval boundary to land on before expiry: advance
        // past ExpiresAtUtc - CancelBeforeExpiry first to also observe Lost).
        _time.Advance(timing.Duration + TimeSpan.FromSeconds(1));
        await Task.Delay(50, _ct); // let background loops observe the advanced time

        var leaseB = await lockB.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        leaseB.ShouldNotBeNull();

        await leaseB!.DisposeAsync();
    }

    // Renewal keeps the lease past several Durations; B stays busy; the record is still readable by
    // key after renewals.
    [Fact]
    public async Task Renewal_keeps_lease_alive_and_record_remains_readable_by_key()
    {
        await using var lockA = await BuildLockAsync();
        await using var lockB = await BuildLockAsync();
        var name = LockName.StoragePurge;
        var timing = ShortTiming(TimeSpan.FromSeconds(3));

        var leaseA = await lockA.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        leaseA.ShouldNotBeNull();

        for (var i = 0; i < 4; i++)
        {
            _time.Advance(timing.RenewalInterval);
            await Task.Delay(30, _ct);
        }

        leaseA!.Lost.IsCancellationRequested.ShouldBeFalse();

        var busy = await lockB.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        busy.ShouldBeNull();

        var accessor = await BuildNodeAsync();
        var storage = accessor.GetManagementStorage();
        var found = await storage.TryReadAsync(
            DistributedLockDso.EntityType,
            DataStorageKey.Create(LockNameDskV1.Create(name)),
            _ct);
        found.Found.ShouldBeTrue();
        DistributedLockDso.ToCurrent(found.Dso!).HolderId.ShouldNotBeNull();

        await leaseA.DisposeAsync();
    }

    // Dispose releases: B acquires at once without advancing time; disposing twice is safe.
    [Fact]
    public async Task Dispose_releases_and_is_idempotent()
    {
        await using var lockA = await BuildLockAsync();
        await using var lockB = await BuildLockAsync();
        var name = LockName.ServerSideSessionCleanup;

        var leaseA = await lockA.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        leaseA.ShouldNotBeNull();

        await leaseA!.DisposeAsync();
        await leaseA.DisposeAsync(); // idempotent

        var leaseB = await lockB.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        leaseB.ShouldNotBeNull();
        await leaseB!.DisposeAsync();
    }

    // Waiting acquire succeeds once the holder disposes; returns null at the deadline.
    [Fact]
    public async Task Waiting_acquire_succeeds_after_release_or_times_out()
    {
        await using var lockA = await BuildLockAsync();
        await using var lockB = await BuildLockAsync();
        var name = LockName.OutboxProcessor;

        var leaseA = await lockA.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        leaseA.ShouldNotBeNull();

        var waitTask = lockB.TryAcquireAsync(name, TimeSpan.FromSeconds(10), _ct);
        await Task.Delay(50, _ct);
        waitTask.IsCompleted.ShouldBeFalse();

        await leaseA!.DisposeAsync();

        // The retry loop is parked on a FakeTimeProvider-based backoff delay; advance past it so the
        // loop wakes up and re-reads the now-free record.
        _time.Advance(TimeSpan.FromSeconds(1));

        var leaseB = await waitTask.WaitAsync(TimeSpan.FromSeconds(10), _ct);
        leaseB.ShouldNotBeNull();
        await leaseB!.DisposeAsync();
    }

    [Fact]
    public async Task Waiting_acquire_returns_null_at_deadline()
    {
        await using var lockA = await BuildLockAsync();
        await using var lockB = await BuildLockAsync();
        var name = LockName.OutboxProcessor;

        var leaseA = await lockA.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        leaseA.ShouldNotBeNull();

        var waitTimeout = TimeSpan.FromSeconds(5);
        var waitTask = lockB.TryAcquireAsync(name, waitTimeout, _ct);
        await Task.Delay(30, _ct);

        _time.Advance(waitTimeout + TimeSpan.FromSeconds(1));

        var result = await waitTask.WaitAsync(TimeSpan.FromSeconds(5), _ct);
        result.ShouldBeNull();

        await leaseA!.DisposeAsync();
    }

    private async Task<StorageDistributedLock> BuildLockWithCountingStorageAsync(Func<Task> onRead)
    {
        var accessor = await BuildNodeAsync();
        var storage = new CountingDistributedLockStorage(new DefaultDistributedLockStorage(accessor), onRead);
        return new StorageDistributedLock(storage, _time, NullLogger<StorageDistributedLock>.Instance);
    }

    // Local gate: with 10 local callers, the database sees one acquire attempt at a time.
    [Fact]
    public async Task Local_gate_serializes_concurrent_callers_on_the_same_lock()
    {
        var concurrentReadCalls = 0;
        var maxObservedConcurrency = 0;
        var gate = new object();

        await using var @lock = await BuildLockWithCountingStorageAsync(async () =>
        {
            int current;
            lock (gate)
            {
                current = ++concurrentReadCalls;
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, current);
            }

            await Task.Delay(10, _ct);

            lock (gate)
            {
                concurrentReadCalls--;
            }
        });

        var name = LockName.OutboxProcessor;
        var tasks = Enumerable.Range(0, 10).Select(_ => @lock.TryAcquireAsync(name, TimeSpan.FromSeconds(5), _ct)).ToArray();

        // The callers queued behind the first one wait on the fake clock, so expire their wait.
        _time.Advance(TimeSpan.FromSeconds(6));
        var results = await Task.WhenAll(tasks);

        maxObservedConcurrency.ShouldBe(1);
        results.Count(r => r is not null).ShouldBe(1);

        foreach (var lease in results.Where(r => r is not null))
        {
            await lease!.DisposeAsync();
        }
    }

    // Different names are independent; two Spaces' SigningKeys names don't block each other.
    [Fact]
    public async Task Different_lock_names_are_independent()
    {
        await using var lockA = await BuildLockAsync();
        var nameOne = LockName.SigningKeys(SpaceId.New());
        var nameTwo = LockName.SigningKeys(SpaceId.New());

        var leaseOne = await lockA.TryAcquireAsync(nameOne, TimeSpan.Zero, _ct);
        var leaseTwo = await lockA.TryAcquireAsync(nameTwo, TimeSpan.Zero, _ct);

        leaseOne.ShouldNotBeNull();
        leaseTwo.ShouldNotBeNull();

        await leaseOne!.DisposeAsync();
        await leaseTwo!.DisposeAsync();
    }

    // Purge leaves the record and emits no outbox event to an unfiltered subscription.
    [Fact]
    public async Task Purge_leaves_lock_record_and_emits_no_outbox_event()
    {
        await using var lockA = await BuildLockAsync();
        var name = LockName.StoragePurge;
        var lease = await lockA.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        lease.ShouldNotBeNull();
        await lease!.DisposeAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        var subscriptionName = SubscriberName.Create("UnfilteredTestSubscription");
        services.AddSingleton<IOutboxSubscription>(new UnfilteredSubscription(subscriptionName));
        services.AddStorageInternal(storage => storage.AddSqlite(o => o.ConnectionString = $"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        var crossPartitionStorage = await provider.GetRequiredService<ICrossPartitionStorageFactory>()
            .GetCrossPartitionStorageAsync(StorageInstanceId.Default, _ct);

        _time.Advance(TimeSpan.FromDays(30));
        await crossPartitionStorage.PurgeExpiredAsync(1000, _ct);

        var accessor = await BuildNodeAsync();
        var found = await accessor.GetManagementStorage().TryReadAsync(
            DistributedLockDso.EntityType,
            DataStorageKey.Create(LockNameDskV1.Create(name)),
            _ct);
        found.Found.ShouldBeTrue();

        var page = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriptionName, 100, _ct);
        page.Events.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Lost_is_cancelled_at_the_cancel_before_expiry_cutoff_when_renewals_fail()
    {
        var accessor = await BuildNodeAsync();
        await using var lockA = await BuildLockAsync(storageOverride: new FailingUpdateDistributedLockStorage(new DefaultDistributedLockStorage(accessor)));
        var timing = new LeaseTiming(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));

        var lease = await lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, timing, _ct);
        lease.ShouldNotBeNull();

        await AdvanceAsync(TimeSpan.FromSeconds(24));
        lease.Lost.IsCancellationRequested.ShouldBeFalse();

        await AdvanceAsync(TimeSpan.FromSeconds(2));
        lease.Lost.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Lost_is_cancelled_at_the_cutoff_when_a_renewal_never_completes()
    {
        var accessor = await BuildNodeAsync();
        await using var lockA = await BuildLockAsync(storageOverride: new HangingFirstUpdateDistributedLockStorage(new DefaultDistributedLockStorage(accessor)));
        var timing = new LeaseTiming(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));

        var lease = await lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, timing, _ct);
        lease.ShouldNotBeNull();

        await AdvanceAsync(TimeSpan.FromSeconds(26));
        lease.Lost.IsCancellationRequested.ShouldBeTrue();
    }

    private async Task AdvanceAsync(TimeSpan total)
    {
        var step = TimeSpan.FromMilliseconds(500);
        for (var elapsed = TimeSpan.Zero; elapsed < total; elapsed += step)
        {
            _time.Advance(step);
            await Task.Delay(5, _ct);
        }
    }

    [Fact]
    public async Task Waiting_acquire_returns_null_once_the_budget_is_spent_after_queueing_behind_a_local_holder()
    {
        var accessor = await BuildNodeAsync();
        var real = new DefaultDistributedLockStorage(accessor);
        var start = _time.GetUtcNow();
        var waitTimeout = TimeSpan.FromSeconds(10);
        var queuedFor = TimeSpan.FromSeconds(9);
        var readFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var storage = new ObservingReadDistributedLockStorage(real, count =>
        {
            if (count == 2)
            {
                _time.Advance(waitTimeout - queuedFor);
                readFinished.SetResult();
            }
        });
        await using var lockA = await BuildLockAsync(storageOverride: storage);
        var name = LockName.OutboxProcessor;
        var timing = new LeaseTiming(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(5));

        var localLease = await lockA.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        localLease.ShouldNotBeNull();

        // Another node takes over the record behind the local holder's back, so the queued waiter
        // finds it held remotely once it gets through the local gate.
        var key = DataStorageKey.Create(LockNameDskV1.Create(name));
        var found = await real.TryReadAsync(key, _ct);
        var remote = new DistributedLockDso.V1
        {
            Name = name.Value,
            HolderId = Guid.NewGuid(),
            HolderMachine = "remote",
            ExpiresAtUtc = start + TimeSpan.FromHours(1),
        };
        (await real.UpdateAsync(UuidV7.From(found.Id!.Value), remote, found.Version!.Value, key, _ct)).ShouldBe(UpdateResult.Success);

        // Cancelled once the budget is spent, so a waiter that is still waiting fails the test instead of hanging.
        using var budgetSpent = new CancellationTokenSource();
        var waiter = lockA.TryAcquireAsync(name, waitTimeout, budgetSpent.Token);

        _time.Advance(queuedFor);
        waiter.IsCompleted.ShouldBeFalse();
        await localLease!.DisposeAsync();

        await readFinished.Task;
        await budgetSpent.CancelAsync();

        (await waiter).ShouldBeNull();
    }

    [Fact]
    public async Task Acquire_returns_null_when_application_stops_during_a_storage_call()
    {
        var lifetime = new TestHostApplicationLifetime();
        var storage = new BlockingReadDistributedLockStorage(new DefaultDistributedLockStorage(await BuildNodeAsync()));
        await using var lockA = await BuildLockAsync(lifetime, storage);

        var acquire = lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, _ct);
        await storage.ReadEntered.WaitAsync(_ct);

        lifetime.StopApplication();
        storage.ReleaseRead();

        (await acquire).ShouldBeNull();

        await using var lockB = await BuildLockAsync();
        var leaseB = await lockB.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, _ct);
        leaseB.ShouldNotBeNull("a refused acquire must not leave the record held until it expires");
        await leaseB!.DisposeAsync();
    }

    [Fact]
    public async Task Acquire_returns_null_without_throwing_when_the_lock_is_disposed_during_a_storage_call()
    {
        var storage = new BlockingReadDistributedLockStorage(new DefaultDistributedLockStorage(await BuildNodeAsync()));
        var lockA = await BuildLockAsync(storageOverride: storage);

        var acquire = lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, _ct);
        await storage.ReadEntered.WaitAsync(_ct);

        await lockA.DisposeAsync();
        storage.ReleaseRead();

        (await acquire).ShouldBeNull();

        await using var lockB = await BuildLockAsync();
        var leaseB = await lockB.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, _ct);
        leaseB.ShouldNotBeNull("a refused acquire must not leave the record held until it expires");
        await leaseB!.DisposeAsync();
    }

    [Fact]
    public async Task Acquire_propagates_a_cancellation_that_comes_from_storage()
    {
        var storage = new CancellingReadDistributedLockStorage();
        await using var lockA = await BuildLockAsync(storageOverride: storage);

        await Should.ThrowAsync<OperationCanceledException>(
            () => lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, _ct));
    }

    [Fact]
    public async Task Acquire_does_not_return_a_lease_when_storage_calls_outlast_the_safe_window()
    {
        var accessor = await BuildNodeAsync();
        await using var lockA = await BuildLockAsync(storageOverride: new ClockAdvancingCreateDistributedLockStorage(new DefaultDistributedLockStorage(accessor), _time, TimeSpan.FromSeconds(26)));
        var timing = new LeaseTiming(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));

        var lease = await lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, timing, _ct);

        lease.ShouldBeNull();

        await using var lockB = await BuildLockAsync();
        var leaseB = await lockB.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, timing, _ct);
        leaseB.ShouldNotBeNull("a refused acquire must not leave the record held until it expires");
        await leaseB!.DisposeAsync();
    }

    // Shutdown, waiting acquire: triggering ApplicationStopping makes B's call return null at once.
    [Fact]
    public async Task Shutdown_waiting_acquire_returns_null_without_advancing_time()
    {
        var lifetimeA = new TestHostApplicationLifetime();
        await using var lockA = await BuildLockAsync();
        await using var lockB = await BuildLockAsync(lifetimeA);
        var name = LockName.OutboxProcessor;

        var leaseA = await lockA.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        leaseA.ShouldNotBeNull();

        var waitTask = lockB.TryAcquireAsync(name, TimeSpan.FromSeconds(60), _ct);
        await Task.Delay(30, _ct);
        waitTask.IsCompleted.ShouldBeFalse();

        lifetimeA.StopApplication();

        var result = await waitTask.WaitAsync(TimeSpan.FromSeconds(5), _ct);
        result.ShouldBeNull();

        await leaseA!.DisposeAsync();
    }

    // Shutdown, new acquire: after ApplicationStopping, TryAcquireAsync on a free name returns null
    // and writes nothing to storage.
    [Fact]
    public async Task Shutdown_new_acquire_returns_null_and_writes_nothing()
    {
        var lifetime = new TestHostApplicationLifetime();
        await using var lockA = await BuildLockAsync(lifetime);
        var name = LockName.ServerSideSessionCleanup;

        lifetime.StopApplication();

        var result = await lockA.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        result.ShouldBeNull();

        var accessor = await BuildNodeAsync();
        var found = await accessor.GetManagementStorage().TryReadAsync(
            DistributedLockDso.EntityType,
            DataStorageKey.Create(LockNameDskV1.Create(name)),
            _ct);
        found.Found.ShouldBeFalse();
    }

    // Shutdown, renewal continues: a held lease still renews until it is disposed.
    [Fact]
    public async Task Shutdown_renewal_continues_until_lease_disposed()
    {
        var lifetime = new TestHostApplicationLifetime();
        await using var lockA = await BuildLockAsync(lifetime);
        await using var lockB = await BuildLockAsync();
        var name = LockName.SigningKeys(SpaceId.Default);
        var timing = ShortTiming(TimeSpan.FromSeconds(3));

        var leaseA = await lockA.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        leaseA.ShouldNotBeNull();

        lifetime.StopApplication();

        for (var i = 0; i < 3; i++)
        {
            _time.Advance(timing.RenewalInterval);
            await Task.Delay(30, _ct);
        }

        leaseA!.Lost.IsCancellationRequested.ShouldBeFalse();
        var busy = await lockB.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        busy.ShouldBeNull();

        await leaseA.DisposeAsync();
    }

    // Leaked lease: acquire on node A and never dispose the lease; disposing node A's lock releases
    // it, logs the Warning, and node B acquires at once without advancing time.
    [Fact]
    public async Task Leaked_lease_is_released_by_lock_disposal()
    {
        var lockA = await BuildLockAsync();
        await using var lockB = await BuildLockAsync();
        var name = LockName.OutboxProcessor;

        var leaseA = await lockA.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        leaseA.ShouldNotBeNull();

        // Deliberately leak: dispose the lock, not the lease.
        await lockA.DisposeAsync();

        var leaseB = await lockB.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        leaseB.ShouldNotBeNull();
        await leaseB!.DisposeAsync();
    }

    [Fact]
    public async Task Disposing_a_lease_after_its_lock_is_disposed_does_not_throw()
    {
        var lockA = await BuildLockAsync();
        var lease = await lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, _ct);
        lease.ShouldNotBeNull();

        await lockA.DisposeAsync();

        await Should.NotThrowAsync(async () => await lease!.DisposeAsync());
    }

    [Fact]
    public async Task Disposing_a_lease_after_its_lock_is_disposed_synchronously_does_not_throw()
    {
        var lockA = await BuildLockAsync();
        var lease = await lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, _ct);
        lease.ShouldNotBeNull();

        lockA.Dispose();

        await Should.NotThrowAsync(async () => await lease!.DisposeAsync());
    }

    // Bounded release: a store decorator whose UpdateAsync hangs completes within ShutdownReleaseTimeout
    // and doesn't throw; the record then expires normally and B acquires after ExpiresAtUtc.
    [Fact]
    public async Task Bounded_release_with_hanging_store_completes_and_does_not_throw()
    {
        var accessor = await BuildNodeAsync();
        var real = new DefaultDistributedLockStorage(accessor);
        var hanging = new HangingUpdateDistributedLockStorage(real);
        var lockA = new StorageDistributedLock(hanging, _time, NullLogger<StorageDistributedLock>.Instance);
        await using var lockB = await BuildLockAsync();

        var name = LockName.StoragePurge;
        var timing = ShortTiming(TimeSpan.FromSeconds(5));
        var lease = await lockA.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        lease.ShouldNotBeNull();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var disposeTask = lockA.DisposeAsync().AsTask();
        await Task.Delay(50, _ct);
        _time.Advance(StorageDistributedLock.ShutdownReleaseTimeout + TimeSpan.FromSeconds(1));
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(10), _ct);
        sw.Stop();
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(9));

        _time.Advance(timing.Duration + TimeSpan.FromSeconds(1));
        await Task.Delay(30, _ct);

        var leaseB = await lockB.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        leaseB.ShouldNotBeNull();
        await leaseB!.DisposeAsync();
    }

    // Disposing the lock twice is safe; TryAcquireAsync after dispose returns null.
    [Fact]
    public async Task Double_dispose_is_safe_and_acquire_after_dispose_returns_null()
    {
        var lockA = await BuildLockAsync();
        await lockA.DisposeAsync();
        await lockA.DisposeAsync();

        var result = await lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, _ct);
        result.ShouldBeNull();
    }

    // Synchronous Dispose() must not throw (regression: DI containers/tests disposed synchronously
    // used to throw "type only implements IAsyncDisposable"), must stop renewal (so B can take over
    // after expiry, without the store ever being released), and must leave TryAcquireAsync returning
    // null afterwards, same as DisposeAsync.
    [Fact]
    public async Task Synchronous_dispose_with_held_lease_does_not_throw_and_stops_renewal()
    {
        var lockA = await BuildLockAsync();
        await using var lockB = await BuildLockAsync();
        var name = LockName.OutboxProcessor;
        var timing = ShortTiming(TimeSpan.FromSeconds(5));

        var leaseA = await lockA.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        leaseA.ShouldNotBeNull();

        Should.NotThrow(lockA.Dispose);

        // Renewal must have stopped: advancing past expiry lets B take over without B ever observing
        // a release (the record is simply stale), proving Dispose() never touched the store.
        _time.Advance(timing.Duration + TimeSpan.FromSeconds(1));
        await Task.Delay(50, _ct);

        var leaseB = await lockB.TryAcquireAsync(name, TimeSpan.Zero, timing, _ct);
        leaseB.ShouldNotBeNull();
        await leaseB!.DisposeAsync();

        var afterDispose = await lockA.TryAcquireAsync(name, TimeSpan.Zero, _ct);
        afterDispose.ShouldBeNull();
    }

    // Dispose() then DisposeAsync(), and the reverse order, must both be safe: whichever runs first
    // performs the cleanup and the other becomes a no-op.
    [Fact]
    public async Task Dispose_then_DisposeAsync_is_safe()
    {
        var lockA = await BuildLockAsync();
        var lease = await lockA.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.Zero, _ct);
        lease.ShouldNotBeNull();

        Should.NotThrow(lockA.Dispose);
        await Should.NotThrowAsync(async () => await lockA.DisposeAsync());
    }

    [Fact]
    public async Task DisposeAsync_then_Dispose_is_safe()
    {
        var lockA = await BuildLockAsync();
        var lease = await lockA.TryAcquireAsync(LockName.ServerSideSessionCleanup, TimeSpan.Zero, _ct);
        lease.ShouldNotBeNull();

        await Should.NotThrowAsync(async () => await lockA.DisposeAsync());
        Should.NotThrow(lockA.Dispose);
    }

    private sealed class UnfilteredSubscription(SubscriberName name) : IOutboxSubscription
    {
        public SubscriberName SubscriberName => name;
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } = new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int>();
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public Ct ApplicationStarted => _started.Token;
        public Ct ApplicationStopping => _stopping.Token;
        public Ct ApplicationStopped => _stopped.Token;

        public void StopApplication() => _stopping.Cancel();

        public void StartApplication() => _started.Cancel();
    }

    /// <summary>
    /// Decorates <see cref="IDistributedLockStorage"/> so a test can observe concurrency of reads
    /// (the local gate test) without implementing Duende.Storage's own internal-member interface.
    /// </summary>
    private sealed class CountingDistributedLockStorage(IDistributedLockStorage inner, Func<Task> onRead) : IDistributedLockStorage
    {
        public async Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct)
        {
            await onRead();
            return await inner.TryReadAsync(key, ct);
        }

        public Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct) => inner.CreateAsync(id, dso, key, ct);

        public Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct) =>
            inner.UpdateAsync(id, dso, expectedVersion, key, ct);
    }

    private sealed class FailingUpdateDistributedLockStorage(IDistributedLockStorage inner) : IDistributedLockStorage
    {
        public Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct) => inner.TryReadAsync(key, ct);

        public Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct) => inner.CreateAsync(id, dso, key, ct);

        public Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct) =>
            throw new InvalidOperationException("Simulated renewal failure.");
    }

    private sealed class HangingFirstUpdateDistributedLockStorage(IDistributedLockStorage inner) : IDistributedLockStorage
    {
        private int _updates;

        public Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct) => inner.TryReadAsync(key, ct);

        public Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct) => inner.CreateAsync(id, dso, key, ct);

        public async Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct)
        {
            if (Interlocked.Increment(ref _updates) == 1)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            return await inner.UpdateAsync(id, dso, expectedVersion, key, ct);
        }
    }

    private sealed class ObservingReadDistributedLockStorage(IDistributedLockStorage inner, Action<int> onRead) : IDistributedLockStorage
    {
        private int _reads;

        public async Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct)
        {
            var count = Interlocked.Increment(ref _reads);
            var result = await inner.TryReadAsync(key, ct);
            onRead(count);
            return result;
        }

        public Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct) => inner.CreateAsync(id, dso, key, ct);

        public Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct) =>
            inner.UpdateAsync(id, dso, expectedVersion, key, ct);
    }

    private sealed class BlockingReadDistributedLockStorage(IDistributedLockStorage inner) : IDistributedLockStorage
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadEntered => _entered.Task;

        public void ReleaseRead() => _release.TrySetResult();

        public async Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct)
        {
            _entered.TrySetResult();
            await _release.Task;
            return await inner.TryReadAsync(key, Ct.None);
        }

        public Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct) => inner.CreateAsync(id, dso, key, Ct.None);

        public Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct) =>
            inner.UpdateAsync(id, dso, expectedVersion, key, Ct.None);
    }

    private sealed class CancellingReadDistributedLockStorage : IDistributedLockStorage
    {
        public Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct) => throw new TaskCanceledException("Simulated provider timeout.");

        public Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct) => throw new NotSupportedException();

        public Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct) => throw new NotSupportedException();
    }

    private sealed class ClockAdvancingCreateDistributedLockStorage(IDistributedLockStorage inner, FakeTimeProvider time, TimeSpan advance) : IDistributedLockStorage
    {
        public Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct) => inner.TryReadAsync(key, ct);

        public async Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct)
        {
            var result = await inner.CreateAsync(id, dso, key, ct);
            time.Advance(advance);
            return result;
        }

        public Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct) =>
            inner.UpdateAsync(id, dso, expectedVersion, key, ct);
    }

    private sealed class HangingUpdateDistributedLockStorage(IDistributedLockStorage inner) : IDistributedLockStorage
    {
        public Task<StoreGetResult> TryReadAsync(DataStorageKey key, Ct ct) => inner.TryReadAsync(key, ct);

        public Task<CreateResult> CreateAsync(UuidV7 id, DistributedLockDso.V1 dso, DataStorageKey key, Ct ct) => inner.CreateAsync(id, dso, key, ct);

        public async Task<UpdateResult> UpdateAsync(UuidV7 id, DistributedLockDso.V1 dso, int expectedVersion, DataStorageKey key, Ct ct)
        {
            // Simulate a store that never responds. Only release attempts call UpdateAsync with a
            // ct bounded by ShutdownReleaseTimeout, so this resolves once that token fires, never
            // throwing on the way out (the caller catches everything).
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return await inner.UpdateAsync(id, dso, expectedVersion, key, ct);
        }
    }
}
