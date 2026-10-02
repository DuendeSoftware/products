// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.OutboxProcessor;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UnitTests.Common;

// This file's own namespace is also named OutboxProcessor, which shadows the Storage type of the
// same name, so it is aliased here.
using StorageOutboxProcessor = Duende.Storage.Internal.Outbox.OutboxProcessor;
using StorageOutboxProcessorOptions = Duende.Storage.Internal.Outbox.OutboxProcessorOptions;

namespace UnitTests.Hosting.OutboxProcessor;

/// <summary>
/// Covers the hosting concerns <see cref="OutboxProcessorHost"/> owns: whether the processor is
/// driven at all, how often, startup jitter, and clean shutdown. Processing semantics (batching,
/// retry, backoff, drop) belong to Duende.Storage's processor and are covered by its own suite.
/// </summary>
public class OutboxProcessorHostTests
{
    // Comfortably longer than any single loop iteration, but short enough to fail fast.
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly IdentityServerOptions _options = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly CountingCrossPartitionStorageFactory _crossPartitionStorageFactory = new();
    private readonly SingleInstanceRouter _storageInstanceRouter = new();

    [Fact]
    public async Task disabled_processor_never_runs_a_cycle()
    {
        _options.OutboxProcessor.EnableProcessor = false;
        _options.OutboxProcessor.FuzzStartup = false;
        _options.OutboxProcessor.ProcessInterval = TimeSpan.FromSeconds(30);

        var host = CreateHost();
        await StartAndSettleAsync(host);

        _time.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(50);
        _crossPartitionStorageFactory.CycleCount.ShouldBe(0, "the store must never be queried while the processor is disabled");

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task process_interval_below_one_second_is_clamped_to_one_second()
    {
        _options.OutboxProcessor.EnableProcessor = true;
        _options.OutboxProcessor.FuzzStartup = false;
        _options.OutboxProcessor.ProcessInterval = TimeSpan.FromMilliseconds(10);

        var host = CreateHost();
        await StartAndSettleAsync(host);

        _time.Advance(TimeSpan.FromMilliseconds(999));
        await Task.Delay(50);
        _crossPartitionStorageFactory.CycleCount.ShouldBe(0, "a sub-second interval must be clamped up to one second, not honoured as configured");

        _time.Advance(TimeSpan.FromMilliseconds(1));
        await WaitForCyclesAsync(1);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task unfuzzed_startup_waits_the_full_interval_before_the_first_cycle()
    {
        _options.OutboxProcessor.EnableProcessor = true;
        _options.OutboxProcessor.FuzzStartup = false;
        _options.OutboxProcessor.ProcessInterval = TimeSpan.FromSeconds(30);

        var host = CreateHost();
        await StartAndSettleAsync(host);

        _time.Advance(TimeSpan.FromSeconds(29));
        await Task.Delay(50);
        _crossPartitionStorageFactory.CycleCount.ShouldBe(0, "without startup fuzz the first cycle waits the whole interval");

        _time.Advance(TimeSpan.FromSeconds(1));
        await WaitForCyclesAsync(1);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task fuzzed_startup_runs_the_first_cycle_before_the_full_interval_elapses()
    {
        _options.OutboxProcessor.EnableProcessor = true;
        _options.OutboxProcessor.FuzzStartup = true;
        _options.OutboxProcessor.ProcessInterval = TimeSpan.FromSeconds(30);

        var host = CreateHost();
        await StartAndSettleAsync(host);

        // Startup fuzz picks a delay strictly shorter than the interval, so by one second short
        // of a full interval the first cycle has always run, whichever value was drawn.
        _time.Advance(TimeSpan.FromSeconds(29));
        await WaitForCyclesAsync(1);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task subsequent_cycles_run_once_per_interval()
    {
        _options.OutboxProcessor.EnableProcessor = true;
        _options.OutboxProcessor.FuzzStartup = false;
        _options.OutboxProcessor.ProcessInterval = TimeSpan.FromSeconds(5);

        var host = CreateHost();
        await StartAndSettleAsync(host);

        _time.Advance(TimeSpan.FromSeconds(5));
        await WaitForCyclesAsync(1);

        _time.Advance(TimeSpan.FromSeconds(5));
        await WaitForCyclesAsync(2);

        _time.Advance(TimeSpan.FromSeconds(5));
        await WaitForCyclesAsync(3);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task stopping_the_host_exits_the_delay_loop_and_stops_running_cycles()
    {
        _options.OutboxProcessor.EnableProcessor = true;
        _options.OutboxProcessor.FuzzStartup = false;
        _options.OutboxProcessor.ProcessInterval = TimeSpan.FromSeconds(5);

        var host = CreateHost();
        await StartAndSettleAsync(host);

        _time.Advance(TimeSpan.FromSeconds(5));
        await WaitForCyclesAsync(1);

        var stop = await Record.ExceptionAsync(() => host.StopAsync(CancellationToken.None));
        stop.ShouldBeNull("cancelling the delay is the normal shutdown path, not a failure");

        var executeTask = host.ExecuteTask.ShouldNotBeNull();
        executeTask.IsCompleted.ShouldBeTrue("the delay loop must have exited");
        executeTask.IsFaulted.ShouldBeFalse();

        var cyclesAtStop = _crossPartitionStorageFactory.CycleCount;
        _time.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(50);
        _crossPartitionStorageFactory.CycleCount.ShouldBe(cyclesAtStop, "no further cycles may run after the host is stopped");
    }

    [Fact]
    public async Task disabled_processor_does_not_start_the_background_loop()
    {
        _options.OutboxProcessor.EnableProcessor = false;

        var host = CreateHost();

        await host.StartAsync(CancellationToken.None);

        host.ExecuteTask.ShouldBeNull("StartAsync must short-circuit before BackgroundService starts executing");

        await host.StopAsync(CancellationToken.None);
    }

    private OutboxProcessorHost CreateHost()
    {
        var subscription = new TestSubscription();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedTransient<IOutboxSubscriptionHandler>(
            subscription.SubscriberName.Value,
            (_, _) => new TestHandler());

        var sp = services.BuildServiceProvider();

        var processor = new StorageOutboxProcessor(
            _crossPartitionStorageFactory,
            _storageInstanceRouter,
            [subscription],
            sp.GetRequiredService<IServiceScopeFactory>(),
            new DefaultAmbientOutboxProcessingContextProvider(),
            Options.Create(new StorageOutboxProcessorOptions()),
            _time,
            TestLogger.Create<StorageOutboxProcessor>());

        return new OutboxProcessorHost(
            processor,
            _options,
            _time,
            TestLogger.Create<OutboxProcessorHost>());
    }

    // Starts the host and gives its loop a moment to reach the first Task.Delay, so that advancing
    // the fake clock cannot race ahead of the timer the loop is about to register.
    private static async Task StartAndSettleAsync(OutboxProcessorHost host)
    {
        await host.StartAsync(CancellationToken.None);
        await Task.Delay(100);
    }

    private async Task WaitForCyclesAsync(int expected)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        while (_crossPartitionStorageFactory.CycleCount < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        _crossPartitionStorageFactory.CycleCount.ShouldBe(expected);
    }

    // Counts the processor's per-cycle storage resolution, which is the observable signal that the
    // host drove a cycle. Throwing afterwards keeps the cycle from touching a real store while
    // still exercising the processor's own per-subscription isolation.
    private sealed class CountingCrossPartitionStorageFactory : ICrossPartitionStorageFactory
    {
        private int _cycleCount;

        public int CycleCount => Volatile.Read(ref _cycleCount);

        public Task<ICrossPartitionStorage> GetCrossPartitionStorageAsync(StorageInstanceId storageInstanceId, Ct ct)
        {
            _ = Interlocked.Increment(ref _cycleCount);
            throw new InvalidOperationException("No store is configured for host timing tests.");
        }
    }

    // Resolves to a single storage instance, matching the single-database setups these host
    // timing tests exercise.
    private sealed class SingleInstanceRouter : IStorageInstanceRouter
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

    private sealed class TestSubscription : IOutboxSubscription
    {
        public SubscriberName SubscriberName { get; } = SubscriberName.Create("SessionExpiration");
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } =
            new HashSet<OutboxEventName> { OutboxEventName.EntityExpired };
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int> { 2107 };
    }

    private sealed class TestHandler : IOutboxSubscriptionHandler
    {
        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct) =>
            Task.FromResult(HandleOutcomeResult.Success());
    }
}

