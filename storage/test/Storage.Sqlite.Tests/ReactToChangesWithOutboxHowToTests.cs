// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying.SearchFields;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Storage.Sqlite;

public static class InvoiceDataCategories
{
    extension(DataCategoryName)
    {
        /// <summary>
        /// The storage category for invoice-related data, including the processed-event
        /// markers this guide's handler creates. Modeled on IdentityServer's and User
        /// Management's own <c>DataCategoryName</c> extension properties.
        /// </summary>
        public static DataCategoryName Invoices => DataCategoryName.Create("invoices");
    }
}

public sealed class ReactToChangesWithOutboxHowToTests
{
    // begin-snippet: outbox-invoice-dso
    public static class InvoiceDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(9500, nameof(InvoiceDso));

        public sealed record V1(string CustomerName, decimal Total) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    // begin-snippet: outbox-processed-event-dso
    // A durable, crash-safe idempotency marker: one row per handled event id. Creating it in
    // the same ExecuteBatchAsync call as the handler's side effect means a redelivered event
    // either persists both or neither, never the side effect without the marker.
    public static class ProcessedInvoiceEventDso
    {
        internal static readonly EntityType EntityType = new(9502, nameof(ProcessedInvoiceEventDso));

        public sealed record V1 : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    // begin-snippet: outbox-event-name
    public static class InvoiceEvents
    {
        // Event names are persisted alongside every delivery row and read back to route
        // messages to handlers, so once a product ships with this name it must stay stable.
        public static readonly OutboxEventName Issued = OutboxEventName.Create("InvoiceIssued");
    }
    // end-snippet

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Publishing_a_single_write_delivers_the_event_to_a_subscription()
    {
        var subscriberName = SubscriberName.Create("invoice-notifications");
        var subscription = new InvoiceIssuedSubscription(subscriberName);
        var recordingState = new RecordingState();

        await using var fixture = await CreateFixtureAsync(subscription, recordingState);
        var storage = fixture.PartitionedStorage;

        var invoiceId = UuidV7.New();
        var invoice = new InvoiceDso.V1("Contoso Ltd", 249.00m);

        // begin-snippet: outbox-construct-event
        var outboxEvent = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = InvoiceEvents.Issued,
            SubjectId = invoiceId,
            EntityTypeName = nameof(InvoiceDso),
            EntityTypeId = (int)InvoiceDso.EntityType.Id,
            Payload = JsonSerializer.Serialize(invoice),
            DsoTypeSchemaVersion = (int)InvoiceDso.V1.DsoVersion.SchemaVersion,
        };
        // end-snippet

        // begin-snippet: outbox-publish-single-write
        var createResult = await storage.CreateAsync(
            invoiceId,
            invoice,
            [],
            SearchFieldCollection.Empty,
            Expiration.NoExpiration,
            [outboxEvent],
            _ct);
        // end-snippet

        createResult.ShouldBe(CreateResult.Success);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        recordingState.HandledInvoices.ShouldContain(i => i.CustomerName == "Contoso Ltd" && i.Total == 249.00m);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriberName, 10, _ct);
        page.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Redelivering_the_same_event_does_not_repeat_the_side_effect()
    {
        var subscriberName = SubscriberName.Create("invoice-notifications-redelivery");
        var subscription = new InvoiceIssuedSubscription(subscriberName);
        var recordingState = new RecordingState();

        await using var fixture = await CreateFixtureAsync(subscription, recordingState);
        var storage = fixture.PartitionedStorage;

        var invoiceId = UuidV7.New();
        var invoice = new InvoiceDso.V1("Contoso Ltd", 249.00m);
        var eventId = OutboxEventId.New();
        var outboxEvent = new OutboxEvent
        {
            Id = eventId,
            Timestamp = DateTimeOffset.UtcNow,
            EventName = InvoiceEvents.Issued,
            SubjectId = invoiceId,
            EntityTypeName = nameof(InvoiceDso),
            EntityTypeId = (int)InvoiceDso.EntityType.Id,
            Payload = JsonSerializer.Serialize(invoice),
            DsoTypeSchemaVersion = (int)InvoiceDso.V1.DsoVersion.SchemaVersion,
        };

        (await storage.CreateAsync(invoiceId, invoice, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [outboxEvent], _ct))
            .ShouldBe(CreateResult.Success);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        recordingState.HandledInvoices.Count.ShouldBe(1);

        // Simulate redelivery of the same logical event (same event id): the processing marker
        // stays in storage even though the delivery row is gone, so re-insert a delivery row
        // with the same event id to stand in for "a crash redelivered this message".
        var redeliveredInvoice = new InvoiceDso.V1("Contoso Ltd (redelivery)", 249.00m);
        var redeliveredOutboxEvent = new OutboxEvent
        {
            Id = eventId,
            Timestamp = DateTimeOffset.UtcNow,
            EventName = InvoiceEvents.Issued,
            SubjectId = invoiceId,
            EntityTypeName = nameof(InvoiceDso),
            EntityTypeId = (int)InvoiceDso.EntityType.Id,
            Payload = JsonSerializer.Serialize(redeliveredInvoice),
            DsoTypeSchemaVersion = (int)InvoiceDso.V1.DsoVersion.SchemaVersion,
        };
        (await storage.CreateAsync(UuidV7.New(), redeliveredInvoice, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [redeliveredOutboxEvent], _ct))
            .ShouldBe(CreateResult.Success);
        await processor.RunProcessorAsync(_ct);

        // The marker created on the first delivery makes the handler's durable idempotency
        // check reject the second attempt at the side effect: HandledInvoices is unchanged.
        recordingState.HandledInvoices.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Publishing_from_a_batch_commits_the_event_with_the_other_operations()
    {
        var subscriberName = SubscriberName.Create("invoice-notifications-batch");
        var subscription = new InvoiceIssuedSubscription(subscriberName);
        var recordingState = new RecordingState();

        await using var fixture = await CreateFixtureAsync(subscription, recordingState);
        var storage = fixture.PartitionedStorage;

        var invoiceId = UuidV7.New();
        var invoice = new InvoiceDso.V1("Fabrikam Inc", 99.50m);

        var outboxEvent = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = InvoiceEvents.Issued,
            SubjectId = invoiceId,
            EntityTypeName = nameof(InvoiceDso),
            EntityTypeId = (int)InvoiceDso.EntityType.Id,
            Payload = JsonSerializer.Serialize(invoice),
            DsoTypeSchemaVersion = (int)InvoiceDso.V1.DsoVersion.SchemaVersion,
        };

        // begin-snippet: outbox-publish-batch
        var operations = new IStorageOperation[]
        {
            CreateOperation.For(invoiceId, invoice, [], SearchFieldCollection.Empty, Expiration.NoExpiration),
        };

        var batchResult = await storage.ExecuteBatchAsync(operations, [outboxEvent], _ct);
        // end-snippet

        batchResult.Success.ShouldBeTrue();

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        recordingState.HandledInvoices.ShouldContain(i => i.CustomerName == "Fabrikam Inc");
    }

    [Fact]
    public async Task A_handler_that_throws_is_retried_and_eventually_succeeds()
    {
        var subscriberName = SubscriberName.Create("invoice-notifications-retry");
        var subscription = new InvoiceIssuedSubscription(subscriberName);
        var flakyState = new FlakyHandlerState(failFirstAttempts: 1, throwInsteadOfRetry: true);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        await using var fixture = await CreateFlakyFixtureAsync(subscription, flakyState, timeProvider);
        var storage = fixture.PartitionedStorage;

        var invoiceId = UuidV7.New();
        var invoice = new InvoiceDso.V1("Northwind Traders", 10m);
        var outboxEvent = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = InvoiceEvents.Issued,
            SubjectId = invoiceId,
            EntityTypeName = nameof(InvoiceDso),
            EntityTypeId = (int)InvoiceDso.EntityType.Id,
            Payload = JsonSerializer.Serialize(invoice),
            DsoTypeSchemaVersion = (int)InvoiceDso.V1.DsoVersion.SchemaVersion,
        };

        (await storage.CreateAsync(invoiceId, invoice, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [outboxEvent], _ct))
            .ShouldBe(CreateResult.Success);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();

        // First pass: the handler throws, so the event is retained for retry and the batch
        // stops here to preserve delivery order for this subscription.
        await processor.RunProcessorAsync(_ct);
        flakyState.Attempts.ShouldBe(1);
        (await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriberName, 10, _ct))
            .Events.ShouldNotBeEmpty();

        // A pass before the backoff delay elapses is a no-op.
        await processor.RunProcessorAsync(_ct);
        flakyState.Attempts.ShouldBe(1);

        // Advance past the base retry delay: the handler now succeeds and the row is deleted.
        timeProvider.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));
        await processor.RunProcessorAsync(_ct);

        flakyState.Attempts.ShouldBe(2);
        flakyState.HandledInvoices.ShouldContain(i => i.CustomerName == "Northwind Traders");
        (await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriberName, 10, _ct))
            .Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_handler_that_keeps_failing_is_dropped_after_max_retries()
    {
        var subscriberName = SubscriberName.Create("invoice-notifications-drop");
        var subscription = new InvoiceIssuedSubscription(subscriberName);
        var flakyState = new FlakyHandlerState(failFirstAttempts: int.MaxValue);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        await using var fixture = await CreateFlakyFixtureAsync(
            subscription,
            flakyState,
            timeProvider,
            configureOutboxProcessor: options =>
            {
                options.MaxRetries = 2;
                options.RetryDelay = TimeSpan.FromSeconds(1);
            });
        var storage = fixture.PartitionedStorage;

        var invoiceId = UuidV7.New();
        var invoice = new InvoiceDso.V1("Poison Pill Co", 1m);
        var outboxEvent = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = InvoiceEvents.Issued,
            SubjectId = invoiceId,
            EntityTypeName = nameof(InvoiceDso),
            EntityTypeId = (int)InvoiceDso.EntityType.Id,
            Payload = JsonSerializer.Serialize(invoice),
            DsoTypeSchemaVersion = (int)InvoiceDso.V1.DsoVersion.SchemaVersion,
        };

        (await storage.CreateAsync(invoiceId, invoice, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [outboxEvent], _ct))
            .ShouldBe(CreateResult.Success);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();

        await processor.RunProcessorAsync(_ct); // attempt 1
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        await processor.RunProcessorAsync(_ct); // attempt 2, reaches MaxRetries
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await processor.RunProcessorAsync(_ct); // force-dropped: deleted without ever succeeding

        (await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriberName, 10, _ct))
            .Events.ShouldBeEmpty();
        flakyState.HandledInvoices.ShouldBeEmpty("the event was dropped, not handled");
    }

    [Fact]
    public async Task A_subscription_with_no_filters_receives_every_event_but_a_typed_filter_narrows_delivery()
    {
        var wildcardName = SubscriberName.Create("all-events");
        var wildcardSubscription = new InvoiceIssuedSubscription(wildcardName, entityTypeIds: new HashSet<int>(), eventNames: new HashSet<OutboxEventName>());
        var typedName = SubscriberName.Create("invoices-only");
        var typedSubscription = new InvoiceIssuedSubscription(typedName, entityTypeIds: new HashSet<int> { (int)InvoiceDso.EntityType.Id });

        await using var fixture = await StorageFixtureFactory.CreateAsync(services =>
        {
            _ = services.AddSingleton<IOutboxSubscription>(wildcardSubscription);
            _ = services.AddSingleton<IOutboxSubscription>(typedSubscription);
            services.AddDsoRegistration<InvoiceDso.V1>();
            services.AddDsoRegistration<OtherDso.V1>();
        }, _ct);
        var storage = fixture.PartitionedStorage;

        var otherId = UuidV7.New();
        var otherEvent = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = OutboxEventName.Create("OtherThingHappened"),
            SubjectId = otherId,
            EntityTypeName = nameof(OtherDso),
            EntityTypeId = (int)OtherDso.EntityType.Id,
            Payload = JsonSerializer.Serialize(new OtherDso.V1("n/a")),
            DsoTypeSchemaVersion = (int)OtherDso.V1.DsoVersion.SchemaVersion,
        };
        (await storage.CreateAsync(otherId, new OtherDso.V1("n/a"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [otherEvent], _ct))
            .ShouldBe(CreateResult.Success);

        var wildcardPage = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(wildcardName, 10, _ct);
        var typedPage = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(typedName, 10, _ct);

        wildcardPage.Events.ShouldContain(e => e.EventId == otherEvent.Id);
        typedPage.Events.ShouldNotContain(e => e.EventId == otherEvent.Id);
    }

    private static Task<IStorageFixture> CreateFixtureAsync(
        IOutboxSubscription subscription,
        RecordingState recordingState,
        FakeTimeProvider? timeProvider = null,
        Action<OutboxProcessorOptions>? configureOutboxProcessor = null) =>
        StorageFixtureFactory.CreateAsync(services =>
        {
            // begin-snippet: outbox-register-subscription-and-handler
            _ = services.AddSingleton<IOutboxSubscription>(subscription);
            // The handler is registered by TYPE, not by capturing an instance: a new handler
            // instance is constructed by DI on every resolution, the same way production code
            // registers subscription handlers (see OutboxProcessorBuilderExtensions usages such
            // as IdentityServerStorageBuilderExtensions). Anything the handler needs to report
            // back to the caller (like RecordingState here) is its own separately registered
            // service, not state captured in a closure.
            services.TryAddKeyedTransient<IOutboxSubscriptionHandler, RecordingInvoiceHandler>(
                subscription.SubscriberName.Value);
            // end-snippet

            _ = services.AddSingleton(recordingState);
            services.AddDsoRegistration<InvoiceDso.V1>();
            services.AddDsoRegistration<ProcessedInvoiceEventDso.V1>();

            if (timeProvider is not null)
            {
                _ = services.AddSingleton<TimeProvider>(timeProvider);
            }

            if (configureOutboxProcessor is not null)
            {
                _ = services.Configure(configureOutboxProcessor);
            }
        }, TestContext.Current.CancellationToken);

    private static Task<IStorageFixture> CreateFlakyFixtureAsync(
        IOutboxSubscription subscription,
        FlakyHandlerState flakyState,
        FakeTimeProvider? timeProvider = null,
        Action<OutboxProcessorOptions>? configureOutboxProcessor = null) =>
        StorageFixtureFactory.CreateAsync(services =>
        {
            _ = services.AddSingleton<IOutboxSubscription>(subscription);
            services.TryAddKeyedTransient<IOutboxSubscriptionHandler, FlakyInvoiceHandler>(
                subscription.SubscriberName.Value);
            _ = services.AddSingleton(flakyState);
            services.AddDsoRegistration<InvoiceDso.V1>();

            if (timeProvider is not null)
            {
                _ = services.AddSingleton<TimeProvider>(timeProvider);
            }

            if (configureOutboxProcessor is not null)
            {
                _ = services.Configure(configureOutboxProcessor);
            }
        }, TestContext.Current.CancellationToken);

    // begin-snippet: outbox-subscription
    internal sealed class InvoiceIssuedSubscription(
        SubscriberName subscriberName,
        IReadOnlySet<int>? entityTypeIds = null,
        IReadOnlySet<OutboxEventName>? eventNames = null) : IOutboxSubscription
    {
        public SubscriberName SubscriberName { get; } = subscriberName;

        public bool IsEnabled => true;

        public IReadOnlySet<OutboxEventName> EventNames { get; } =
            eventNames ?? new HashSet<OutboxEventName> { InvoiceEvents.Issued };

        public IReadOnlySet<int> EntityTypeIds { get; } =
            entityTypeIds ?? new HashSet<int> { (int)InvoiceDso.EntityType.Id };
    }
    // end-snippet

    /// <summary>
    /// Separate, independently-registered service that collects what the handler processed,
    /// so tests can assert on it across however many short-lived handler instances DI creates.
    /// </summary>
    internal sealed class RecordingState
    {
        public List<InvoiceDso.V1> HandledInvoices { get; } = [];
    }

    // begin-snippet: outbox-handler
    internal sealed class RecordingInvoiceHandler(IPartitionedStorageFactory partitionedStorageFactory, RecordingState state) : IOutboxSubscriptionHandler
    {
        // The processed-event marker never expires on its own. A queued delivery row has no
        // bounded lifetime: MaxRetries and the retry backoff only bound how long a message is
        // retried while a processor instance keeps running, and that retry count lives in
        // memory, so it resets to zero on every restart. A delivery row can therefore sit in
        // the queue, surviving any number of restarts, for as long as the processor is down or
        // the subscription falls behind; there's no "worst-case redelivery window" to size an
        // expiration against. Purging this marker on a guessed schedule risks deleting it while
        // the row it guards is still queued, which would let the side effect run again on that
        // redelivery. Only remove the marker once redelivery is actually impossible, for example
        // during a maintenance pass after the subscription's queue has been drained or the
        // subscription itself has been removed.
        private static readonly Expiration MarkerExpiration = Expiration.NoExpiration;

        public async Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            if (item.Dso is not InvoiceDso.V1 invoice)
            {
                return HandleOutcomeResult.Drop(
                    $"Expected {nameof(InvoiceDso)}.V1, got {item.Dso?.GetType().Name ?? "null"}");
            }

            // Idempotency guard: at-least-once delivery means the same message can be handed to
            // HandleAsync more than once (e.g. a crash between processing and deleting the row).
            // An in-memory set of handled ids is NOT crash-safe or replica-safe: it's reset on
            // restart and not shared across instances. Instead, create a durable marker DSO keyed
            // on the event's own id. If a real side effect is itself a storage write, create the
            // marker in the SAME ExecuteBatchAsync call as that write, so a redelivery either
            // commits both or neither. Here the "side effect" is only an in-memory recording for
            // the test, so the marker commits first and the side effect only runs once it has.
            //
            // A handler gets its store from IPartitionedStorageFactory, never from the keyed
            // IPartitionedStorage service directly: the factory is what applies DataCategory
            // routing and pool selection, and resolving the keyed service bypasses both.
            var storage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Invoices, ct);
            var markerId = UuidV7.From(item.EventId.Value);
            var markerResult = await storage.CreateAsync(
                markerId, new ProcessedInvoiceEventDso.V1(), [], SearchFieldCollection.Empty, MarkerExpiration, [], ct);

            if (markerResult != CreateResult.Success)
            {
                // AlreadyExists: this event id was already processed by an earlier delivery
                // (possibly one that crashed after creating the marker but before this row was
                // deleted). Treat the redelivery as a no-op instead of repeating the side effect.
                return HandleOutcomeResult.Success();
            }

            state.HandledInvoices.Add(invoice);
            return HandleOutcomeResult.Success();
        }
    }
    // end-snippet

    /// <summary>
    /// Separate, independently-registered state for the flaky handler, mirroring how a real
    /// handler's test-visible results are reported through their own service rather than a
    /// captured instance (see <see cref="RecordingState"/>).
    /// </summary>
    internal sealed class FlakyHandlerState(int failFirstAttempts, bool throwInsteadOfRetry = false)
    {
        public int FailFirstAttempts { get; } = failFirstAttempts;
        public bool ThrowInsteadOfRetry { get; } = throwInsteadOfRetry;
        public int Attempts { get; set; }
        public List<InvoiceDso.V1> HandledInvoices { get; } = [];
    }

    private sealed class FlakyInvoiceHandler(FlakyHandlerState state) : IOutboxSubscriptionHandler
    {
        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            state.Attempts++;

            if (state.Attempts <= state.FailFirstAttempts)
            {
                if (state.ThrowInsteadOfRetry)
                {
                    throw new InvalidOperationException($"simulated failure, attempt {state.Attempts}");
                }

                return Task.FromResult(HandleOutcomeResult.Retry($"simulated failure, attempt {state.Attempts}"));
            }

            if (item.Dso is InvoiceDso.V1 invoice)
            {
                state.HandledInvoices.Add(invoice);
            }

            return Task.FromResult(HandleOutcomeResult.Success());
        }
    }

    internal static class OtherDso
    {
        internal static readonly EntityType EntityType = new(9501, nameof(OtherDso));

        public sealed record V1(string Note) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }

    private static class StorageFixtureFactory
    {
        public static Task<IStorageFixture> CreateAsync(Action<IServiceCollection> configureServices, Ct ct) =>
            new SqliteStorageEngineFixtureFactory().CreateAsync(ct, configureServices);
    }
}
