// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.OutboxProcessor;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace UnitTests.Hosting.OutboxProcessor;

public class OutboxProcessorHostTests
{
    private readonly IdentityServerOptions _options = new();
    private readonly ILogger<OutboxProcessorHost> _logger = TestLogger.Create<OutboxProcessorHost>();

    [Fact]
    public async Task disabled_processor_does_not_query_store()
    {
        _options.OutboxProcessor.EnableProcessor = false;

        var (storageFactory, storage, _) = CreateSeededStoreFactory();
        await SeedOutboxEventAsync(storage);
        var host = CreateHost(storageFactory);

        await host.RunProcessorAsync(CancellationToken.None);

        // Event should still exist as processor was disabled so store was never queried for processing
        var page = await storage.GetOutboxEventsForSubscriberAsync(
            SubscriberName.Create("SessionExpiration"), 100, CancellationToken.None);
        page.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task run_processor_against_empty_store_completes()
    {
        _options.OutboxProcessor.EnableProcessor = true;

        var factory = CreateStoreFactory();
        var host = CreateHost(factory);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunProcessorAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_processor_should_survive_store_exception()
    {
        _options.OutboxProcessor.EnableProcessor = true;

        var factory = new ThrowingStorageFactory();
        var host = CreateHost(factory);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunProcessorAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_processor_respects_cancellation()
    {
        _options.OutboxProcessor.EnableProcessor = true;

        var (storageFactory, storage, _) = CreateSeededStoreFactory();
        await SeedOutboxEventAsync(storage);
        var handler = new ConfigurableHandler([HandleOutcomeResult.Success()]);
        var host = CreateHostWithHandler(storageFactory, handler, TimeProvider.System);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunProcessorAsync(cts.Token);
        });

        exception.ShouldBeNull();

        // Event should NOT have been processed because the token was already cancelled
        var page = await storage.GetOutboxEventsForSubscriberAsync(
            SubscriberName.Create("SessionExpiration"), 100, CancellationToken.None);
        page.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task retry_result_stops_batch()
    {
        _options.OutboxProcessor.EnableProcessor = true;
        _options.OutboxProcessor.RetryDelay = TimeSpan.FromSeconds(1);

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (storageFactory, storage, _) = CreateSeededStoreFactory();
        await SeedOutboxEventAsync(storage);

        var handler = new ConfigurableHandler([HandleOutcomeResult.Retry("transient failure")]);
        var host = CreateHostWithHandler(storageFactory, handler, timeProvider);

        await host.RunProcessorAsync(CancellationToken.None);

        // Event should NOT be deleted; it remains in the outbox for retry
        var page = await storage.GetOutboxEventsForSubscriberAsync(
            SubscriberName.Create("SessionExpiration"), 100, CancellationToken.None);
        page.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task successive_retries_increment_attempts()
    {
        // Use MaxRetries=3 so the event is NOT force-dropped on 2nd attempt
        _options.OutboxProcessor.EnableProcessor = true;
        _options.OutboxProcessor.MaxRetries = 3;
        _options.OutboxProcessor.RetryDelay = TimeSpan.FromSeconds(1);

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (storageFactory, storage, _) = CreateSeededStoreFactory();
        await SeedOutboxEventAsync(storage);

        var handler = new ConfigurableHandler([
            HandleOutcomeResult.Retry("fail 1"),
            HandleOutcomeResult.Retry("fail 2")
        ]);
        var host = CreateHostWithHandler(storageFactory, handler, timeProvider);

        // First processor run: attempt 1
        await host.RunProcessorAsync(CancellationToken.None);

        // Advance time past the backoff delay
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second processor run: attempt 2 (still not at MaxRetries=3, so not force-dropped)
        await host.RunProcessorAsync(CancellationToken.None);

        // Event still present (not deleted)
        var page = await storage.GetOutboxEventsForSubscriberAsync(
            SubscriberName.Create("SessionExpiration"), 100, CancellationToken.None);
        page.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task force_drop_after_max_retries()
    {
        _options.OutboxProcessor.EnableProcessor = true;
        _options.OutboxProcessor.MaxRetries = 2;
        _options.OutboxProcessor.RetryDelay = TimeSpan.FromSeconds(1);

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (storageFactory, storage, _) = CreateSeededStoreFactory();
        await SeedOutboxEventAsync(storage);

        // Handler always returns Retry
        var handler = new ConfigurableHandler([
            HandleOutcomeResult.Retry("fail 1"),
            HandleOutcomeResult.Retry("fail 2")
        ]);
        var host = CreateHostWithHandler(storageFactory, handler, timeProvider);

        // First processor run: attempt 1
        await host.RunProcessorAsync(CancellationToken.None);

        // Advance time past backoff
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second processor run: attempt 2, reaches MaxRetries
        await host.RunProcessorAsync(CancellationToken.None);

        // Advance time past backoff for the third processor cycle
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        // Third processor run: force-drop (handler is NOT called, event is deleted)
        await host.RunProcessorAsync(CancellationToken.None);

        var page = await storage.GetOutboxEventsForSubscriberAsync(
            SubscriberName.Create("SessionExpiration"), 100, CancellationToken.None);
        page.Events.Count.ShouldBe(0);
    }

    [Fact]
    public async Task success_clears_retry_state()
    {
        _options.OutboxProcessor.EnableProcessor = true;
        _options.OutboxProcessor.MaxRetries = 3;
        _options.OutboxProcessor.RetryDelay = TimeSpan.FromSeconds(1);

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (storageFactory, storage, _) = CreateSeededStoreFactory();
        await SeedOutboxEventAsync(storage);

        // First call returns Retry, second returns Success
        var handler = new ConfigurableHandler([
            HandleOutcomeResult.Retry("transient"),
            HandleOutcomeResult.Success()
        ]);
        var host = CreateHostWithHandler(storageFactory, handler, timeProvider);

        // First processor: retry
        await host.RunProcessorAsync(CancellationToken.None);
        var pageBefore = await storage.GetOutboxEventsForSubscriberAsync(
            SubscriberName.Create("SessionExpiration"), 100, CancellationToken.None);
        pageBefore.Events.Count.ShouldBe(1);

        // Advance time past backoff
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Second processor run: success, event is deleted
        await host.RunProcessorAsync(CancellationToken.None);

        var pageAfter = await storage.GetOutboxEventsForSubscriberAsync(
            SubscriberName.Create("SessionExpiration"), 100, CancellationToken.None);
        pageAfter.Events.Count.ShouldBe(0);
    }

    [Fact]
    public async Task drop_result_removes_event()
    {
        _options.OutboxProcessor.EnableProcessor = true;

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (storageFactory, storage, _) = CreateSeededStoreFactory();
        await SeedOutboxEventAsync(storage);

        var handler = new ConfigurableHandler([HandleOutcomeResult.Drop("permanent failure")]);
        var host = CreateHostWithHandler(storageFactory, handler, timeProvider);

        await host.RunProcessorAsync(CancellationToken.None);

        var page = await storage.GetOutboxEventsForSubscriberAsync(
            SubscriberName.Create("SessionExpiration"), 100, CancellationToken.None);
        page.Events.Count.ShouldBe(0);
    }

    private OutboxProcessorHost CreateHost(IStorageFactory storageFactory)
    {
        var subscriber = new TestSubscriber();
        IEnumerable<IOutboxSubscriber> subscribers = [subscriber];

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedTransient<IOutboxSubscriberHandler>(
            subscriber.SubscriberName.Value,
            (_, _) => new TestHandler());

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return new OutboxProcessorHost(
            storageFactory,
            subscribers,
            scopeFactory,
            _options,
            TimeProvider.System,
            _logger);
    }

    private OutboxProcessorHost CreateHostWithHandler(
        IStorageFactory storageFactory,
        IOutboxSubscriberHandler handler,
        TimeProvider timeProvider)
    {
        var subscriber = new TestSubscriber();
        IEnumerable<IOutboxSubscriber> subscribers = [subscriber];

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeyedTransient<IOutboxSubscriberHandler>(
            subscriber.SubscriberName.Value,
            (_, _) => handler);

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return new OutboxProcessorHost(
            storageFactory,
            subscribers,
            scopeFactory,
            _options,
            timeProvider,
            _logger);
    }

    private static async Task SeedOutboxEventAsync(IStorage storage)
    {
        var entityId = UuidV7.New();
        var dso = new ServerSideSessionDso.V1
        {
            Key = $"test_{Guid.NewGuid():N}",
            Scheme = "idsrv",
            SubjectId = "sub_test",
            SessionId = "sid_test",
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            RenewedUtcTicks = DateTime.UtcNow.Ticks,
            ExpiresUtcTicks = DateTime.UtcNow.AddHours(1).Ticks,
            Ticket = "unused"
        };
        var outboxEvent = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = OutboxEventName.EntityExpired,
            SubjectId = entityId,
            EntityTypeName = "ServerSideSessionDso",
            EntityTypeId = (int)ServerSideSessionDso.EntityType.Id,
            Payload = "{}"
        };
        await storage.CreateAsync(
            entityId,
            dso,
            [],
            new SearchFieldCollection([]),
            Expiration.NoExpiration,
            [outboxEvent],
            CancellationToken.None);
    }

    private static IStorageFactory CreateStoreFactory()
    {
        var (factory, _, _) = CreateSeededStoreFactory();
        return factory;
    }

    // Returns the ServiceProvider so callers can hold a reference, preventing GC from
    // disposing the pooled SQLite connections that anchor the shared in-memory database.
    private static (IStorageFactory Factory, IStorage Storage, ServiceProvider Sp) CreateSeededStoreFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var dbName = $"processor_test_{Guid.NewGuid():N}";

        // Register the subscriber so the store's outbox fanout routes events to it
        services.AddSingleton<IOutboxSubscriber>(new TestSubscriber());

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        var sp = services.BuildServiceProvider();
        var pooledStore = sp.GetRequiredService<IPooledStore>();
        ((Duende.Storage.Schema.IDatabaseSchema)pooledStore).MigrateAsync(CancellationToken.None).GetAwaiter().GetResult();

        var storage = pooledStore.OpenPool(0);
        return (new SimpleStorageFactory(storage), storage, sp);
    }

    private sealed class TestSubscriber : IOutboxSubscriber
    {
        public SubscriberName SubscriberName { get; } = SubscriberName.Create("SessionExpiration");
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } =
            new HashSet<OutboxEventName> { OutboxEventName.EntityExpired };
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int> { 2107 };
    }

    private sealed class TestHandler : IOutboxSubscriberHandler
    {
        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct) =>
            Task.FromResult(HandleOutcomeResult.Success());
    }

    private sealed class ConfigurableHandler(IReadOnlyList<HandleOutcomeResult> outcomes) : IOutboxSubscriberHandler
    {
        private int _callIndex;

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            var index = _callIndex < outcomes.Count ? _callIndex : outcomes.Count - 1;
            _callIndex++;
            return Task.FromResult(outcomes[index]);
        }
    }
}
