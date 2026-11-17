// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.IdentityServer.Hosting.OutboxProcessor;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.Hosting.Coordination;

/// <summary>
/// Reproduces duplicate outbox dispatch across two nodes: two <see cref="ServiceProvider"/>
/// "nodes" running the real, internal <see cref="OutboxProcessorHost"/> against one shared
/// temp-file SQLite database. Today's <see cref="OutboxProcessorHost"/> has no cross-node
/// coordination, so both nodes can read and handle the same outbox events before either deletes
/// them. Also covers <see cref="OutboxProcessorHost"/>'s coordination: lost-lease behaviour,
/// in-memory retry state reset on a newly acquired lease, and release on shutdown.
/// </summary>
public sealed class MultiNodeOutboxTests
{
    private static readonly SubscriberName SubscriptionName = SubscriberName.Create("multi-node-outbox-test");
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task two_nodes_handle_the_same_outbox_events_exactly_once()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var gate = new RaceOrWindowGate(window: TimeSpan.FromSeconds(2));
        var recorder = new GatedRecordingHandler(gate);

        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster, recorder));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster, recorder));

        await cluster.MigrateAsync(_ct);

        var eventIds = await SeedOutboxEventsAsync(nodeA, 5, _ct);

        var hostA = GetOutboxProcessorHost(nodeA);
        var hostB = GetOutboxProcessorHost(nodeB);

        await hostA.StartAsync(_ct);
        await hostB.StartAsync(_ct);
        try
        {
            // Give each host's loop a moment to reach its first Task.Delay before advancing fake
            // time, matching the pattern in OutboxProcessorHostTests.
            await Task.Delay(TimeSpan.FromMilliseconds(100), _ct);

            // FuzzStartup is disabled, so each host's first cycle waits exactly ProcessInterval.
            // Advancing by that much wakes both nodes' loops at (approximately) the same time;
            // only one of them can hold the distributed lock's lease.
            cluster.TimeProvider.Advance(TimeSpan.FromSeconds(30));

            await WaitUntilOutboxQueueEmptyAsync(nodeA, TimeSpan.FromSeconds(10), _ct);
        }
        finally
        {
            await hostA.StopAsync(_ct);
            await hostB.StopAsync(_ct);
        }

        // Assert only the per-event handling count (never which node handled what; repo memory 464b6f7b).
        recorder.Counts.Keys.ShouldBe(eventIds, ignoreOrder: true);
        recorder.Counts.ShouldAllBe(pair => pair.Value == 1, "every outbox event must be handled exactly once across the cluster");
    }

    [Fact]
    public async Task lost_lease_means_the_losing_node_deletes_nothing_and_the_other_node_processes_the_events()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var recorder = new BlockUntilCancelledHandler();
        var interval = TimeSpan.FromSeconds(5);

        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster, recorder, interval));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster, recorder, interval));

        await cluster.MigrateAsync(_ct);
        await SeedOutboxEventsAsync(nodeA, 5, _ct);

        var hostA = GetOutboxProcessorHost(nodeA);
        var hostB = GetOutboxProcessorHost(nodeB);

        await hostA.StartAsync(_ct);
        await hostB.StartAsync(_ct);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), _ct);
            cluster.TimeProvider.Advance(interval);

            // Whichever node won the race for the lease is now blocked inside the handler for the
            // first event, with its whole batch still unread/undeleted from the store.
            await WaitUntilAsync(() => Task.FromResult(recorder.Counts.Count), count => count >= 1, _ct);
            (await CountOutboxEventsAsync(nodeA, _ct)).ShouldBe(5, "the holder must not have deleted anything yet while its run is still blocked");

            // Force the holder's lease to be lost: jump the shared clock in one big step, past the
            // lease's Duration, so the renewal loop never gets a chance to run before expiry (same
            // trick StorageDistributedLockTests uses for "renewal blocked" scenarios).
            cluster.TimeProvider.Advance(TimeSpan.FromMinutes(5));

            // The direct proof that the holder deletes nothing: its blocked handler call is itself
            // cancelled (by the lease's Lost token, linked into the run), so Storage's processor
            // rethrows the cancellation before it ever reaches the batch delete for this run.
            (await recorder.FirstCallWasCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), _ct)).ShouldBeTrue(
                "the node whose lease was lost must have its blocked handler call cancelled, not left to complete and delete the batch");

            // The other node's next cycle (triggered by the same jump) acquires the now-expired
            // lease and drains the queue.
            await WaitUntilOutboxQueueEmptyAsync(nodeA, TimeSpan.FromSeconds(10), _ct);
        }
        finally
        {
            await hostA.StopAsync(_ct);
            await hostB.StopAsync(_ct);
        }
    }

    [Fact]
    public async Task losing_and_reacquiring_the_lease_resets_in_memory_retry_state()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var handler = new AlwaysRetryHandler();
        var interval = TimeSpan.FromSeconds(5);
        var logSink = new CapturingLoggerProvider();

        var nodeA = cluster.AddNode(services =>
        {
            ConfigureNode(
                services,
                cluster,
                handler,
                interval,
                // High enough that MaxRetries never drops the event in either cycle: this test is
                // about the processor being recreated, not about retry-count exhaustion.
                configureStorage: s => s.ConfigureOutboxProcessor(opt => opt.MaxRetries = 100));
            services.AddLogging(b =>
            {
                b.AddProvider(logSink);
                b.SetMinimumLevel(LogLevel.Trace);
            });
        });

        await cluster.MigrateAsync(_ct);
        var eventIds = await SeedOutboxEventsAsync(nodeA, 1, _ct);
        var eventId = eventIds.Single();

        var hostA = GetOutboxProcessorHost(nodeA);
        await hostA.StartAsync(_ct);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), _ct);

            // Cycle 1: A acquires the lease for the first time; the event keeps failing and stays.
            cluster.TimeProvider.Advance(interval);
            await WaitUntilAsync(() => Task.FromResult(handler.Attempts), attempts => attempts >= 1, _ct);

            // Force the lease to be lost: jump the clock in one big step, past the lease's
            // Duration, so the renewal loop never gets a chance to run before expiry (same trick
            // StorageDistributedLockTests uses for "renewal blocked" scenarios). This also wakes
            // the host's own cycle 2, which may still run with the not-yet-forgotten lease; the
            // loss is only detected at the end of that run (or the start of the next one).
            cluster.TimeProvider.Advance(TimeSpan.FromMinutes(5));
            await WaitUntilAsync(() => Task.FromResult(logSink.CountOf("LeaseLost")), count => count >= 1, _ct);

            // Cycle 3: with the lease forgotten, a newly acquired lease means a fresh processor,
            // whose in-memory retry state for this event starts clean again.
            cluster.TimeProvider.Advance(interval + TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => Task.FromResult(logSink.CountOf("NewLeaseFreshProcessor")), count => count >= 2, _ct);
        }
        finally
        {
            await hostA.StopAsync(_ct);
        }

        // The event must still be pending: with MaxRetries effectively unreachable, dropping
        // would only ever indicate a processor bug unrelated to this test, not a retry-count
        // artifact either way.
        var remaining = await CountOutboxEventsAsync(nodeA, _ct);
        remaining.ShouldBe(1, "the still-failing event must still be pending after both cycles");
        handler.Counts[eventId].ShouldBeGreaterThanOrEqualTo(3);

        // The direct proof that a lease handover creates a fresh processor: the host logs this
        // once per newly acquired lease, so losing and reacquiring the lease once must log it
        // twice (the initial acquire, then the reacquire).
        logSink.CountOf("NewLeaseFreshProcessor").ShouldBe(2, "a fresh processor must be created exactly once for the initial acquire and once for the reacquire after the lease was lost");
    }

    [Fact]
    public async Task releasing_the_lease_on_shutdown_lets_the_other_node_acquire_without_waiting_for_expiry()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var gate = new RaceOrWindowGate(window: TimeSpan.FromSeconds(5));
        var recorder = new GatedRecordingHandler(gate);
        var interval = TimeSpan.FromSeconds(5);

        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster, recorder, interval));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster, recorder, interval));

        await cluster.MigrateAsync(_ct);
        await SeedOutboxEventsAsync(nodeA, 5, _ct);

        var hostA = GetOutboxProcessorHost(nodeA);
        var hostB = GetOutboxProcessorHost(nodeB);

        await hostA.StartAsync(_ct);
        await Task.Delay(TimeSpan.FromMilliseconds(100), _ct);
        cluster.TimeProvider.Advance(interval);

        // A is now inside the gate's real-time window, holding the lease.
        await WaitUntilAsync(() => Task.FromResult(recorder.Counts.Count), count => count >= 1, _ct);

        // Stopping A must release the lease, not leave it to sit until Duration (far longer than
        // one interval) elapses.
        await hostA.StopAsync(_ct);

        await hostB.StartAsync(_ct);
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), _ct);

            // Only one interval's worth of time, nowhere near Duration: B can only end up
            // draining the queue if A actually released the lease in StopAsync.
            cluster.TimeProvider.Advance(interval);
            await WaitUntilOutboxQueueEmptyAsync(nodeA, TimeSpan.FromSeconds(10), _ct);
        }
        finally
        {
            await hostB.StopAsync(_ct);
        }
    }

    private static void ConfigureNode(
        IServiceCollection services,
        MultiNodeTestCluster cluster,
        IOutboxSubscriptionHandler handler,
        TimeSpan? interval = null,
        Action<IStorageBuilder>? configureStorage = null)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(cluster.KeyRingDirectory));

        var builder = services.AddIdentityServer(options =>
        {
            options.OutboxProcessor.FuzzStartup = false;
            options.OutboxProcessor.ProcessInterval = interval ?? TimeSpan.FromSeconds(30);
            options.StoragePurge.EnablePurge = false;
        });

        builder.AddStorage(s =>
        {
            s.AddSqlite(o => o.ConnectionString = cluster.ConnectionString);
            configureStorage?.Invoke(s);
        });

        services.AddDsoRegistration<ServerSideSessionDso.V1>();
        services.AddSingleton<IOutboxSubscription>(
            new RecordingOutboxSubscription(SubscriptionName, ServerSideSessionDso.EntityType.Id));
        services.AddKeyedSingleton<IOutboxSubscriptionHandler>(SubscriptionName.Value, handler);
    }

    private static OutboxProcessorHost GetOutboxProcessorHost(ServiceProvider provider) =>
        provider.GetServices<IHostedService>().OfType<OutboxProcessorHost>().Single();

    private static async Task<HashSet<Guid>> SeedOutboxEventsAsync(ServiceProvider provider, int count, Ct ct)
    {
        var storage = await provider.GetRequiredService<IPartitionedStorageFactory>()
            .GetPartitionedStorageAsync(DataCategoryName.Operational, ct);

        var eventIds = new HashSet<Guid>();
        for (var i = 0; i < count; i++)
        {
            var entityId = UuidV7.New();
            var eventId = OutboxEventId.New();
            eventIds.Add(eventId.Value);

            var dso = new ServerSideSessionDso.V1
            {
                Key = $"multi-node-outbox_{Guid.NewGuid():N}",
                Scheme = "idsrv",
                SubjectId = $"sub_{i}",
                SessionId = $"sid_{i}",
                CreatedUtcTicks = DateTime.UtcNow.Ticks,
                RenewedUtcTicks = DateTime.UtcNow.Ticks,
                ExpiresUtcTicks = DateTime.UtcNow.AddHours(1).Ticks,
                Ticket = "unused"
            };

            await storage.CreateAsync(
                entityId,
                dso,
                [],
                new SearchFieldCollection([]),
                Expiration.NoExpiration,
                [
                    new OutboxEvent
                    {
                        Id = eventId,
                        Timestamp = DateTimeOffset.UtcNow,
                        EventName = OutboxEventName.EntityExpired,
                        SubjectId = entityId,
                        EntityTypeName = "ServerSideSessionDso",
                        EntityTypeId = (int)ServerSideSessionDso.EntityType.Id,
                        DsoTypeSchemaVersion = 1,
                        Payload = "{}"
                    }
                ],
                ct);
        }

        return eventIds;
    }

    private static async Task<int> CountOutboxEventsAsync(ServiceProvider provider, Ct ct)
    {
        var crossPartitionStorage = await provider.GetRequiredService<ICrossPartitionStorageFactory>()
            .GetCrossPartitionStorageAsync(StorageInstanceId.Default, ct);

        var page = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(SubscriptionName, 100, ct);
        return page.Events.Count;
    }

    private static async Task WaitUntilAsync(Func<Task<int>> getValue, Func<int, bool> isDone, Ct ct, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? WaitTimeout);
        while (!isDone(await getValue()))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), ct);
        }
    }

    private static async Task WaitUntilOutboxQueueEmptyAsync(ServiceProvider provider, TimeSpan timeout, Ct ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            while (true)
            {
                var count = await CountOutboxEventsAsync(provider, cts.Token);
                if (count == 0)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cts.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The outbox queue did not drain in time.");
        }
    }

    /// <summary>Matches only the entity type and events seeded by this test.</summary>
    private sealed class RecordingOutboxSubscription(SubscriberName name, uint entityTypeId) : IOutboxSubscription
    {
        public SubscriberName SubscriberName => name;
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } = new HashSet<OutboxEventName> { OutboxEventName.EntityExpired };
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int> { (int)entityTypeId };
    }

    /// <summary>
    /// Forces the overlap this test must catch, without assuming either outcome is deterministic
    /// by itself: the first call across the whole cluster waits until EITHER a second such call
    /// arrives concurrently (two nodes both inside the handler at once -- the bug a cross-node
    /// lock must prevent) OR a bounded real-time <paramref name="window"/> elapses. Without the
    /// lock, both nodes' hosts can reach the handler at the same time: the second entrant
    /// arrives, both proceed immediately, and the same events get handled twice. With the lock,
    /// only one node's host ever holds the lease, so no second entrant ever arrives; the lone
    /// entrant proceeds once the window elapses.
    /// </summary>
    private sealed class RaceOrWindowGate(TimeSpan window)
    {
        private readonly TaskCompletionSource _secondEntrantArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _entered;

        public async Task EnterAsync(Ct ct)
        {
            if (Interlocked.Increment(ref _entered) >= 2)
            {
                _secondEntrantArrived.TrySetResult();
            }

            await Task.WhenAny(_secondEntrantArrived.Task, Task.Delay(window, ct));
            ct.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Records how many times each outbox event was handled. Only the very first call across the
    /// whole cluster is gated by <see cref="RaceOrWindowGate"/>; later calls (including a second
    /// handling of the same event after a lease handover) return immediately.
    /// </summary>
    private sealed class GatedRecordingHandler(RaceOrWindowGate gate) : IOutboxSubscriptionHandler
    {
        private int _gated;

        public ConcurrentDictionary<Guid, int> Counts { get; } = new();

        public async Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            Counts.AddOrUpdate(item.EventId.Value, 1, (_, count) => count + 1);

            if (Interlocked.Exchange(ref _gated, 1) == 0)
            {
                await gate.EnterAsync(ct);
            }

            return HandleOutcomeResult.Success();
        }
    }

    /// <summary>
    /// Records how many times each outbox event was handled. The first call across the whole
    /// cluster blocks forever (until its token is cancelled), so a test can force a lease loss
    /// while that call is in flight and observe whether it was cancelled rather than completed.
    /// </summary>
    private sealed class BlockUntilCancelledHandler : IOutboxSubscriptionHandler
    {
        private int _firstCallEntered;

        public ConcurrentDictionary<Guid, int> Counts { get; } = new();

        public TaskCompletionSource<bool> FirstCallWasCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            Counts.AddOrUpdate(item.EventId.Value, 1, (_, count) => count + 1);

            if (Interlocked.Exchange(ref _firstCallEntered, 1) == 0)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }
                catch (OperationCanceledException)
                {
                    FirstCallWasCancelled.TrySetResult(true);
                    throw;
                }
            }

            return HandleOutcomeResult.Success();
        }
    }

    /// <summary>Always reports a transient failure, so the event keeps being retried.</summary>
    private sealed class AlwaysRetryHandler : IOutboxSubscriptionHandler
    {
        public ConcurrentDictionary<Guid, int> Counts { get; } = new();

        public int Attempts => Counts.Values.Sum();

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            Counts.AddOrUpdate(item.EventId.Value, 1, (_, count) => count + 1);
            return Task.FromResult(HandleOutcomeResult.Retry("always fails for this test"));
        }
    }

    /// <summary>
    /// Captures every log message's event name, so a test can assert a specific internal log
    /// (here, <c>OutboxProcessorLog.NewLeaseFreshProcessor</c>) fired a specific number of times.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _eventNames = new();

        public int CountOf(string eventName) => _eventNames.Count(n => n == eventName);

        public string[] All() => _eventNames.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_eventNames);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> eventNames) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (eventId.Name is not null)
                {
                    eventNames.Enqueue(eventId.Name);
                }
            }
        }
    }
}