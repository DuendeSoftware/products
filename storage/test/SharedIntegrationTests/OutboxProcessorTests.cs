// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

//using System.Text.Json;
//using System.Text.Json.Nodes;
//using Duende.Storage.Internal;
//using Duende.Storage.Internal.Builder;
//using Duende.Storage.Internal.Operations;
//using Duende.Storage.Internal.Outbox;
//using Duende.Storage.Internal.Querying;
//using Duende.Storage.Internal.Querying.Fields;
//using Duende.Storage.Internal.Querying.SearchFields;
//using Duende.Storage.Internal.Querying.Sorting;
//using Duende.Storage.Pagination;
//using Duende.Storage.Querying;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Logging.Abstractions;
//using Microsoft.Extensions.Options;
//using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
//using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
//using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

//namespace Duende.Storage.IntegrationTests;

//public partial class OutboxProcessorTests
//{
//    private readonly Ct _ct = TestContext.Current.CancellationToken;

//    [Fact]
//    public async Task handler_not_invoked_when_outbox_is_empty()
//    {
//        var subscription = new TestSubscription("sub-empty");
//        await using var fixture = await CreateFixtureAsync(subscription);

//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));

//        exception.ShouldBeNull();
//        handler.CallCount.ShouldBe(0);
//    }

//    [Fact]
//    public async Task handler_not_invoked_again_when_outbox_already_drained()
//    {
//        var subscription = new TestSubscription("sub-drained");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(1);

//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));

//        exception.ShouldBeNull();
//        handler.CallCount.ShouldBe(1);
//    }

//    [Fact]
//    public async Task disabled_subscription_is_skipped_when_enabled_subscription_processes()
//    {
//        var disabled = new TestSubscription("sub-disabled", isEnabled: false);
//        var enabled = new TestSubscription("sub-enabled");
//        await using var fixture = await CreateFixtureAsync(disabled, enabled);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var disabledHandler = new RecordingHandler();
//        var enabledHandler = new RecordingHandler();
//        var processor = CreateProcessor(
//            fixture.PartitionedStorage,
//            [disabled, enabled],
//            CreateScopeFactory((disabled.SubscriberName.Value, disabledHandler), (enabled.SubscriberName.Value, enabledHandler)));

//        await processor.RunProcessorAsync(_ct);

//        disabledHandler.CallCount.ShouldBe(0, "a disabled subscription must never dispatch");
//        enabledHandler.CallCount.ShouldBe(1);
//    }

//    [Fact]
//    public async Task subscription_is_skipped_when_disabled_after_construction()
//    {
//        // IOutboxSubscription.IsEnabled can be a computed property (e.g. backed by DI/feature-flag
//        // state) that changes after the processor is constructed. OutboxSubscriptions snapshots
//        // enabled subscriptions once, at construction, so this asserts the processor still
//        // re-checks IsEnabled on every RunProcessorAsync pass, matching main's host.
//        var subscription = new TestSubscription("sub-dynamic-enabled");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(1);

//        subscription.IsEnabled = false;
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        await processor.RunProcessorAsync(_ct);

//        handler.CallCount.ShouldBe(1);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(1);
//    }

//    [Fact]
//    public async Task subscription_is_processed_when_enabled_after_construction()
//    {
//        // A subscription disabled at construction time must still be re-evaluated on every
//        // pass and picked up once it becomes enabled, rather than being permanently
//        // excluded by a construction-time snapshot.
//        // Seeded while enabled so the write path's own subscription matching accepts the
//        // event, then disabled before the processor is constructed: the processor must
//        // still re-check IsEnabled every pass rather than excluding it permanently based
//        // on the state at construction time.
//        var subscription = new TestSubscription("sub-dynamic-disabled");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        subscription.IsEnabled = false;
//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(0);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(1);

//        subscription.IsEnabled = true;
//        await processor.RunProcessorAsync(_ct);

//        handler.CallCount.ShouldBe(1);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.ShouldBeEmpty();
//    }

//    [Fact]
//    public async Task all_enabled_subscriptions_handle_event_in_single_pass()
//    {
//        var subA = new TestSubscription("sub-multi-a");
//        var subB = new TestSubscription("sub-multi-b");
//        var subC = new TestSubscription("sub-multi-c");
//        await using var fixture = await CreateFixtureAsync(subA, subB, subC);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handlerA = new RecordingHandler();
//        var handlerB = new RecordingHandler();
//        var handlerC = new RecordingHandler();
//        var processor = CreateProcessor(
//            fixture.PartitionedStorage,
//            [subA, subB, subC],
//            CreateScopeFactory(
//                (subA.SubscriberName.Value, handlerA),
//                (subB.SubscriberName.Value, handlerB),
//                (subC.SubscriberName.Value, handlerC)));

//        await processor.RunProcessorAsync(_ct);

//        handlerA.CallCount.ShouldBe(1);
//        handlerB.CallCount.ShouldBe(1);
//        handlerC.CallCount.ShouldBe(1);
//    }

//    [Fact]
//    public async Task event_is_deleted_when_handler_succeeds()
//    {
//        var subscription = new TestSubscription("sub-success");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(_ct);

//        handler.CallCount.ShouldBe(1);
//        var page = await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct);
//        page.Events.ShouldBeEmpty();
//    }

//    [Fact]
//    public async Task delivered_event_matches_what_was_written()
//    {
//        var subscription = new TestSubscription("sub-fidelity");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        var (subjectId, written) = await SeedEventAsync(fixture.PartitionedStorage);

//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(_ct);

//        handler.Received.Count.ShouldBe(1);
//        var delivered = handler.Received[0];
//        delivered.EventId.ShouldBe(written.Id);
//        delivered.EventName.ShouldBe(written.EventName);
//        delivered.EntityTypeName.ShouldBe(written.EntityTypeName);
//        delivered.EntityTypeId.ShouldBe(written.EntityTypeId);
//        delivered.SubjectId.ShouldBe(subjectId);
//        // Compared as parsed JSON rather than raw string, since a round trip through
//        // PostgreSQL's jsonb column can reformat (e.g. re-order or re-space) the text
//        // without changing its meaning.
//        JsonNode.DeepEquals(JsonNode.Parse(delivered.Payload), JsonNode.Parse(written.Payload)).ShouldBeTrue();
//        delivered.SubscriberName.ShouldBe(subscription.SubscriberName);
//        delivered.MessageId.ShouldNotBe(default);
//        delivered.SequenceNumber.ShouldBeGreaterThan(0);
//    }

//    [Fact]
//    public async Task delivered_event_has_hydrated_dso()
//    {
//        var subscription = new TestSubscription("sub-dso");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(_ct);

//        handler.Received.Count.ShouldBe(1);
//        _ = handler.Received[0].Dso.ShouldBeOfType<TestDso>();
//    }

//    [Fact]
//    public async Task each_subscription_receives_only_its_own_row_when_fanning_out()
//    {
//        var subA = new TestSubscription("sub-fanout-a");
//        var subB = new TestSubscription("sub-fanout-b");
//        await using var fixture = await CreateFixtureAsync(subA, subB);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handlerA = new RecordingHandler();
//        var handlerB = new RecordingHandler();
//        var processor = CreateProcessor(
//            fixture.PartitionedStorage,
//            [subA, subB],
//            CreateScopeFactory((subA.SubscriberName.Value, handlerA), (subB.SubscriberName.Value, handlerB)));

//        await processor.RunProcessorAsync(_ct);

//        handlerA.Received.Count.ShouldBe(1);
//        handlerB.Received.Count.ShouldBe(1);
//        handlerA.Received[0].MessageId.ShouldNotBe(handlerB.Received[0].MessageId);
//        handlerA.Received[0].EventId.ShouldBe(handlerB.Received[0].EventId);
//    }

//    [Fact]
//    public async Task only_matching_subscription_receives_event_for_entity_type_id()
//    {
//        var subA = new TestSubscription("sub-type-a", entityTypeIds: new HashSet<int> { 99 });
//        var subB = new TestSubscription("sub-type-b", entityTypeIds: new HashSet<int> { 100 });
//        await using var fixture = await CreateFixtureAsync(subA, subB);
//        _ = await SeedEventAsync(fixture.PartitionedStorage, entityTypeId: 99);

//        var handlerA = new RecordingHandler();
//        var handlerB = new RecordingHandler();
//        var processor = CreateProcessor(
//            fixture.PartitionedStorage,
//            [subA, subB],
//            CreateScopeFactory((subA.SubscriberName.Value, handlerA), (subB.SubscriberName.Value, handlerB)));

//        await processor.RunProcessorAsync(_ct);

//        handlerA.Received.Count.ShouldBe(1);
//        handlerB.Received.Count.ShouldBe(0);
//    }

//    [Fact]
//    public async Task all_events_handled_in_sequence_order_and_deleted()
//    {
//        var subscription = new TestSubscription("sub-batch");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        for (var i = 0; i < 3; i++)
//        {
//            _ = await SeedEventAsync(fixture.PartitionedStorage);
//        }

//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(_ct);

//        handler.Received.Count.ShouldBe(3);
//        handler.Received.Select(e => e.SequenceNumber).ShouldBe(handler.Received.Select(e => e.SequenceNumber).OrderBy(n => n));
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.ShouldBeEmpty();
//    }

//    [Fact]
//    public async Task all_events_are_drained_across_two_passes_when_exceeding_batch_size()
//    {
//        var subscription = new TestSubscription("sub-overflow");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        for (var i = 0; i < 5; i++)
//        {
//            _ = await SeedEventAsync(fixture.PartitionedStorage);
//        }

//        var handler = new RecordingHandler();
//        var options = new OutboxProcessorOptions { BatchSize = 3 };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options);

//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(3);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(2);

//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(5);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.ShouldBeEmpty();
//    }

//    [Fact]
//    public async Task no_exception_propagates_when_storage_factory_throws()
//    {
//        var subscription = new TestSubscription("SessionExpiration");
//        var processor = CreateProcessor(new ThrowingPartitionedStorageFactory(), [subscription], CreateScopeFactory());

//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));

//        exception.ShouldBeNull();
//    }

//    [Fact]
//    public async Task event_is_not_deleted_when_no_keyed_handler_registered()
//    {
//        var subscription = new TestSubscription("sub-no-handler");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        // No handler registered for this subscription's key.
//        var processor = CreateProcessor(fixture.PartitionedStorage, [subscription], CreateScopeFactory());

//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));

//        exception.ShouldBeNull();
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(1, "an event with no registered handler must not be deleted");
//    }

//    [Fact]
//    public async Task event_is_retried_and_attempt_is_counted_when_handler_throws()
//    {
//        var subscription = new TestSubscription("sub-throws");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var handler = new ThrowingHandler(_ => new InvalidOperationException("boom"));
//        var options = new OutboxProcessorOptions { MaxRetries = 5, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options, timeProvider);

//        await processor.RunProcessorAsync(_ct);
//        // Event survives (treated as Retry, not Drop), and the handler was invoked.
//        handler.CallCount.ShouldBe(1);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(1);

//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct);
//        // Attempt counting: still below MaxRetries=5, so it's retried again (not force-dropped).
//        handler.CallCount.ShouldBe(2);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(1);
//    }

//    [Fact]
//    public async Task later_events_in_batch_are_left_untouched_when_first_event_returns_retry()
//    {
//        var subscription = new TestSubscription("sub-stop-batch");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handler = new ConfigurableHandler([HandleOutcomeResult.Retry("stop here")]);
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(_ct);

//        // Only the first event in the batch was handled; the rest were never reached.
//        handler.CallCount.ShouldBe(1);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(3);
//    }

//    [Fact]
//    public async Task other_subscriptions_still_process_when_one_subscription_throws()
//    {
//        var throwingSub = new TestSubscription("sub-isolated-throws");
//        var healthySub = new TestSubscription("sub-isolated-healthy");
//        await using var fixture = await CreateFixtureAsync(throwingSub, healthySub);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var throwingHandler = new ThrowingHandler(_ => new InvalidOperationException("subscription-level failure"));
//        var healthyHandler = new RecordingHandler();
//        var processor = CreateProcessor(
//            fixture.PartitionedStorage,
//            [throwingSub, healthySub],
//            CreateScopeFactory((throwingSub.SubscriberName.Value, throwingHandler), (healthySub.SubscriberName.Value, healthyHandler)));

//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));

//        exception.ShouldBeNull();
//        healthyHandler.CallCount.ShouldBe(1);
//    }

//    [Fact]
//    public async Task event_is_retained_when_handler_returns_retry()
//    {
//        var subscription = new TestSubscription("SessionExpiration");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var handler = new ConfigurableHandler([HandleOutcomeResult.Retry("transient failure")]);
//        var options = new OutboxProcessorOptions { RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options, timeProvider);

//        await processor.RunProcessorAsync(_ct);

//        var page = await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct);
//        page.Events.Count.ShouldBe(1, "a retried event must not be deleted");
//    }

//    [Fact]
//    public async Task call_count_and_retained_event_reflect_each_attempt_across_successive_retries()
//    {
//        var subscription = new TestSubscription("SessionExpiration");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var handler = new ConfigurableHandler([
//            HandleOutcomeResult.Retry("fail 1"),
//            HandleOutcomeResult.Retry("fail 2")
//        ]);
//        // MaxRetries=3 so the event is NOT force-dropped on the 2nd attempt.
//        var options = new OutboxProcessorOptions { MaxRetries = 3, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options, timeProvider);

//        await processor.RunProcessorAsync(_ct);
//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct);

//        handler.CallCount.ShouldBe(2);
//        var page = await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct);
//        page.Events.Count.ShouldBe(1);
//    }

//    [Fact]
//    public async Task event_is_force_dropped_when_retries_exceed_max_retries()
//    {
//        var subscription = new TestSubscription("SessionExpiration");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var handler = new ConfigurableHandler([
//            HandleOutcomeResult.Retry("fail 1"),
//            HandleOutcomeResult.Retry("fail 2")
//        ]);
//        var options = new OutboxProcessorOptions { MaxRetries = 2, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options, timeProvider);

//        await processor.RunProcessorAsync(_ct); // attempt 1
//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct); // attempt 2, reaches MaxRetries
//        timeProvider.Advance(TimeSpan.FromSeconds(5));
//        await processor.RunProcessorAsync(_ct); // force-drop; handler is NOT called again

//        handler.CallCount.ShouldBe(2);
//        var page = await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct);
//        page.Events.Count.ShouldBe(0, "an event that exceeded MaxRetries must be force-dropped rather than retried indefinitely");
//    }

//    [Fact]
//    public async Task event_is_deleted_when_retry_is_followed_by_success()
//    {
//        var subscription = new TestSubscription("SessionExpiration");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var handler = new ConfigurableHandler([
//            HandleOutcomeResult.Retry("transient"),
//            HandleOutcomeResult.Success()
//        ]);
//        var options = new OutboxProcessorOptions { MaxRetries = 3, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options, timeProvider);

//        await processor.RunProcessorAsync(_ct);
//        var pageBefore = await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct);
//        pageBefore.Events.Count.ShouldBe(1);

//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct);

//        var pageAfter = await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct);
//        pageAfter.Events.Count.ShouldBe(0);
//    }

//    [Fact]
//    public async Task event_is_removed_when_handler_returns_drop()
//    {
//        var subscription = new TestSubscription("sub-drop");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handler = new RecordingHandler(HandleOutcomeResult.Drop("no longer relevant"));
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(_ct);

//        handler.CallCount.ShouldBe(1);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.ShouldBeEmpty();
//    }

//    [Fact]
//    public async Task batch_size_clamps_to_one_when_negative()
//    {
//        var subscription = new TestSubscription("sub-clamp-low");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handler = new RecordingHandler();
//        var options = new OutboxProcessorOptions { BatchSize = -5 }; // clamps to 1
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options);

//        await processor.RunProcessorAsync(_ct);

//        handler.CallCount.ShouldBe(1);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(1);
//    }

//    [Fact]
//    public async Task retry_delay_gates_next_attempt_when_backoff_multiplier_is_nan()
//    {
//        var subscription = new TestSubscription("sub-delay-nan");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var handler = new ConfigurableHandler([HandleOutcomeResult.Retry("fail"), HandleOutcomeResult.Success()]);
//        var options = new OutboxProcessorOptions { RetryBackoffMultiplier = double.NaN, RetryDelay = TimeSpan.FromMinutes(1), MaxRetries = 5 };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options, timeProvider);

//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(1);

//        // Not yet eligible: RetryDelay (1 min) has not elapsed.
//        timeProvider.Advance(TimeSpan.FromSeconds(30));
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(1);

//        // Past RetryDelay: eligible again, proving the NaN multiplier fell back to RetryDelay
//        // rather than propagating a NaN through the backoff calculation.
//        timeProvider.Advance(TimeSpan.FromSeconds(31));
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(2);
//    }

//    [Fact]
//    public async Task retry_delay_gates_next_attempt_when_backoff_multiplier_is_infinite()
//    {
//        var subscription = new TestSubscription("sub-delay-infinite");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var handler = new ConfigurableHandler([HandleOutcomeResult.Retry("fail"), HandleOutcomeResult.Success()]);
//        var options = new OutboxProcessorOptions { RetryBackoffMultiplier = double.PositiveInfinity, RetryDelay = TimeSpan.FromMinutes(1), MaxRetries = 5 };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options, timeProvider);

//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(1);

//        // Not yet eligible: RetryDelay (1 min) has not elapsed.
//        timeProvider.Advance(TimeSpan.FromSeconds(30));
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(1);

//        // Past RetryDelay: eligible again, proving the infinite multiplier fell back to
//        // RetryDelay rather than propagating Infinity through the backoff calculation.
//        timeProvider.Advance(TimeSpan.FromSeconds(31));
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(2);
//    }

//    [Fact]
//    public async Task max_retry_delay_gates_next_attempt_when_backoff_multiplier_causes_tick_overflow()
//    {
//        var subscription = new TestSubscription("sub-delay-overflow");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var handler = new ConfigurableHandler([
//            HandleOutcomeResult.Retry("fail 1"),
//            HandleOutcomeResult.Retry("fail 2"),
//            HandleOutcomeResult.Success()
//        ]);
//        // RetryBackoffMultiplier is astronomically large so that RetryDelay.Ticks * multiplier^(attempt-1)
//        // overflows a long on the second attempt, forcing the tick-overflow guard to fall back to
//        // MaxRetryDelay rather than throwing or producing a nonsensical delay.
//        var options = new OutboxProcessorOptions
//        {
//            MaxRetries = 5,
//            RetryDelay = TimeSpan.FromMinutes(1),
//            RetryBackoffMultiplier = 1e18,
//            MaxRetryDelay = TimeSpan.FromSeconds(5),
//        };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options, timeProvider);

//        await processor.RunProcessorAsync(_ct); // attempt 1: first delivery, no prior retry to gate against
//        handler.CallCount.ShouldBe(1);

//        timeProvider.Advance(TimeSpan.FromMinutes(2));
//        await processor.RunProcessorAsync(_ct); // attempt 2: RetryDelay.Ticks * 1e18 overflows -> falls back to MaxRetryDelay (5s)
//        handler.CallCount.ShouldBe(2);

//        // Not yet eligible: MaxRetryDelay (5s) has not elapsed.
//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(2);

//        // Past MaxRetryDelay: eligible again.
//        timeProvider.Advance(TimeSpan.FromSeconds(4));
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(3);
//    }

//    [Fact]
//    public async Task handler_not_invoked_and_event_retained_when_token_is_pre_cancelled()
//    {
//        var subscription = new TestSubscription("sub-precancelled");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        using var cts = new CancellationTokenSource();
//        await cts.CancelAsync();

//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(cts.Token));

//        exception.ShouldBeNull();
//        handler.CallCount.ShouldBe(0);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(1);
//    }

//    [Fact]
//    public async Task later_subscriptions_are_not_invoked_when_cancellation_occurs_during_first_subscription()
//    {
//        var subA = new TestSubscription("sub-loop-a");
//        var subB = new TestSubscription("sub-loop-b");
//        await using var fixture = await CreateFixtureAsync(subA, subB);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        using var cts = new CancellationTokenSource();
//        var handlerA = new CancellingHandler(cts);
//        var handlerB = new RecordingHandler();
//        var processor = CreateProcessor(
//            fixture.PartitionedStorage,
//            [subA, subB],
//            CreateScopeFactory((subA.SubscriberName.Value, handlerA), (subB.SubscriberName.Value, handlerB)));

//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(cts.Token));

//        exception.ShouldBeNull();
//        handlerA.CallCount.ShouldBe(1);
//        handlerB.CallCount.ShouldBe(0);
//    }

//    [Fact]
//    public async Task later_events_in_batch_are_not_handled_when_cancellation_occurs_during_first_event()
//    {
//        var subscription = new TestSubscription("sub-batch-cancel");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        using var cts = new CancellationTokenSource();
//        var handler = new CancellingHandler(cts);
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(cts.Token));

//        exception.ShouldBeNull();
//        // Only the first event's handler ran; the remaining two were never reached.
//        handler.CallCount.ShouldBe(1);
//        // At-least-once: since cancellation propagated before the delete step, even the
//        // successfully handled first event survives and will be redelivered next pass.
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(3);
//    }

//    [Fact]
//    public async Task no_retry_attempt_is_recorded_when_handler_throws_operation_canceled()
//    {
//        var subscription = new TestSubscription("sub-cancel-not-retry");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        using var cts = new CancellationTokenSource();
//        var handler = new CancellingHandler(cts);
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(cts.Token));
//        exception.ShouldBeNull();

//        // If the cancellation had been recorded as a Retry, this second, uncancelled run
//        // would be gated by a RetryAfterUtc backoff and the handler would not run again
//        // immediately. It runs immediately, proving no retry attempt was recorded.
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(2);
//    }

//    [Fact]
//    public async Task retry_state_is_unchanged_when_an_interleaved_cancelled_run_occurs()
//    {
//        var subscription = new TestSubscription("sub-retry-survives-cancel");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var handler = new ConfigurableHandler([
//            HandleOutcomeResult.Retry("first"),
//            HandleOutcomeResult.Retry("second")
//        ]);
//        var options = new OutboxProcessorOptions { MaxRetries = 5, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler, options, timeProvider);

//        await processor.RunProcessorAsync(_ct); // attempt 1 recorded
//        handler.CallCount.ShouldBe(1);

//        timeProvider.Advance(TimeSpan.FromSeconds(2));

//        using var cts = new CancellationTokenSource();
//        await cts.CancelAsync();
//        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(cts.Token));
//        exception.ShouldBeNull();
//        // The cancelled run never reached the handler (guard fires before dispatch).
//        handler.CallCount.ShouldBe(1, "a cancelled run must not reach the handler again");

//        await processor.RunProcessorAsync(_ct); // attempt 2, proving state survived the cancelled run
//        handler.CallCount.ShouldBe(2);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(1);
//    }

//    [Fact]
//    public async Task event_is_not_deleted_and_is_redelivered_when_cancellation_occurs_after_handling_first_event()
//    {
//        var subscription = new TestSubscription("sub-at-least-once");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);
//        _ = await SeedEventAsync(fixture.PartitionedStorage);

//        using var cts = new CancellationTokenSource();
//        var handler = new CancellingHandler(cts);
//        var processor = CreateProcessor(fixture.PartitionedStorage, subscription, handler);

//        await processor.RunProcessorAsync(cts.Token);

//        // The first event was handled (Success) but never deleted, because cancellation
//        // propagated before the batch's single delete call. Both events are still present.
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.Count.ShouldBe(2);

//        // Redelivery: a subsequent, uncancelled pass processes the previously "handled"
//        // event again.
//        var recordingHandler = new RecordingHandler();
//        var redeliveryProcessor = CreateProcessor(fixture.PartitionedStorage, subscription, recordingHandler);
//        await redeliveryProcessor.RunProcessorAsync(_ct);

//        recordingHandler.CallCount.ShouldBe(2);
//        (await fixture.PartitionedStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.ShouldBeEmpty();
//    }

//    [Fact]
//    public async Task storage_factory_receives_configured_storage_key()
//    {
//        var subscription = new TestSubscription("sub-storage-key");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        var partitionedStorageFactory = new RecordingPartitionedStorageFactory(fixture.PartitionedStorage);
//        var options = new OutboxProcessorOptions { StorageKey = "tenant-a" };
//        var processor = new OutboxProcessor(
//            partitionedStorageFactory,
//            [subscription],
//            CreateScopeFactory((subscription.SubscriberName.Value, new RecordingHandler())),
//            new DefaultAmbientOutboxProcessingContextProvider(),
//            Options.Create(options),
//            TimeProvider.System,
//            NullLogger<OutboxProcessor>.Instance);

//        await processor.RunProcessorAsync(_ct);

//        partitionedStorageFactory.ReceivedStorageKeys.ShouldContain("tenant-a");
//    }

//    [Fact]
//    public async Task storage_factory_receives_null_storage_key_by_default()
//    {
//        var subscription = new TestSubscription("sub-storage-key-default");
//        await using var fixture = await CreateFixtureAsync(subscription);
//        var partitionedStorageFactory = new RecordingPartitionedStorageFactory(fixture.PartitionedStorage);
//        var options = new OutboxProcessorOptions();
//        var processor = new OutboxProcessor(
//            partitionedStorageFactory,
//            [subscription],
//            CreateScopeFactory((subscription.SubscriberName.Value, new RecordingHandler())),
//            new DefaultAmbientOutboxProcessingContextProvider(),
//            Options.Create(options),
//            TimeProvider.System,
//            NullLogger<OutboxProcessor>.Instance);

//        await processor.RunProcessorAsync(_ct);

//        partitionedStorageFactory.ReceivedStorageKeys.ShouldContain(default(object));
//    }

//    [Fact]
//    public async Task handler_sees_each_events_own_ambient_context()
//    {
//        var subscription = new TestSubscription("sub-probe-pools");
//        var eventA = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var eventB = CreatePersistedEvent(subscription.SubscriberName, poolId: 2);
//        var storage = new FakeOutboxStorage([eventA, eventB]);

//        var provider = new ProbeAmbientContextProvider();
//        var handler = new ProbeHandler();
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), provider: provider);

//        await processor.RunProcessorAsync(_ct);

//        handler.ObservedAmbientPoolIds.ShouldBe(["pool-1", "pool-2"]);
//    }

//    [Fact]
//    public async Task lifecycle_timeline_shows_two_establishes_no_leaked_context_and_disposal_before_the_next_establish()
//    {
//        var subscription = new TestSubscription("sub-probe-order");
//        var eventA = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var eventB = CreatePersistedEvent(subscription.SubscriberName, poolId: 2);
//        var storage = new FakeOutboxStorage([eventA, eventB]);

//        var provider = new ProbeAmbientContextProvider();
//        var handler = new ProbeHandler();
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), provider: provider);

//        await processor.RunProcessorAsync(_ct);

//        provider.PreviousValuesAtEstablish.Count.ShouldBe(2);
//        provider.PreviousValuesAtEstablish.ShouldAllBe(previous => previous == null);
//        provider.Timeline.ShouldBe(["establish:pool-1", "dispose", "establish:pool-2", "dispose"]);
//    }

//    [Fact]
//    public async Task the_scope_is_disposed_and_the_throwing_handler_invoked_exactly_once_when_the_handler_throws()
//    {
//        var subscription = new TestSubscription("sub-probe-throw");
//        var evt = CreatePersistedEvent(subscription.SubscriberName, poolId: 9);
//        var storage = new FakeOutboxStorage([evt]);

//        var provider = new ProbeAmbientContextProvider();
//        var handler = new ThrowingHandler(_ => new InvalidOperationException("boom"));
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), provider: provider);

//        await processor.RunProcessorAsync(_ct);

//        handler.CallCount.ShouldBe(1);
//        provider.DisposeCount.ShouldBe(1);
//    }

//    [Fact]
//    public async Task default_provider_allows_processing_to_proceed_when_no_custom_provider_is_registered()
//    {
//        var subscription = new TestSubscription("sub-default-provider-proceeds");
//        var evt = CreatePersistedEvent(subscription.SubscriberName, poolId: 42);
//        var storage = new FakeOutboxStorage([evt]);

//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)));

//        await processor.RunProcessorAsync(_ct);

//        handler.CallCount.ShouldBe(1);
//        (await storage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct)).Events.ShouldBeEmpty();
//    }

//    [Fact]
//    public async Task context_failure_on_one_message_does_not_stop_the_batch_and_other_messages_are_still_handled()
//    {
//        var subscription = new TestSubscription("sub-context-fail-continues");
//        var events = Enumerable.Range(1, 5).Select(i => CreatePersistedEvent(subscription.SubscriberName, poolId: 1)).ToList();
//        var failingMessageId = events[1].MessageId;
//        var storage = new FakeOutboxStorage(events);

//        var provider = new ConfigurableAmbientContextProvider(evt => evt.MessageId == failingMessageId);
//        var handler = new RecordingHandler();
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), provider: provider);

//        await processor.RunProcessorAsync(_ct);

//        handler.Received.Select(e => e.MessageId).ShouldBe(events.Where(e => e.MessageId != failingMessageId).Select(e => e.MessageId));
//        handler.Received.ShouldNotContain(e => e.MessageId == failingMessageId, "the handler must never be invoked for a message whose context establishment failed");
//    }

//    [Fact]
//    public async Task context_failure_records_an_attempt_against_the_subscription_retry_slot_exactly_like_a_handler_retry()
//    {
//        var subscription = new TestSubscription("sub-context-fail-retry-slot");
//        var evt = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var storage = new FakeOutboxStorage([evt]);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var provider = new ConfigurableAmbientContextProvider(_ => true);
//        var handler = new RecordingHandler();
//        var options = new OutboxProcessorOptions { MaxRetries = 5, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), options, timeProvider, provider);

//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(0);
//        storage.Events.Count.ShouldBe(1, "a message whose context establishment failed must not be deleted");

//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(0, "context establishment still fails on every attempt in this test");
//        provider.AttemptCount.ShouldBe(2, "a second attempt must have been recorded against the same retry slot a handler retry would use");
//    }

//    [Fact]
//    public async Task repeated_context_failure_force_drops_the_message_at_max_retries()
//    {
//        var subscription = new TestSubscription("sub-context-fail-force-drop");
//        var evt = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var storage = new FakeOutboxStorage([evt]);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var provider = new ConfigurableAmbientContextProvider(_ => true);
//        var handler = new RecordingHandler();
//        var options = new OutboxProcessorOptions { MaxRetries = 2, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), options, timeProvider, provider);

//        await processor.RunProcessorAsync(_ct); // attempt 1
//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct); // attempt 2, reaches MaxRetries
//        timeProvider.Advance(TimeSpan.FromSeconds(5));
//        await processor.RunProcessorAsync(_ct); // force-drop; context establishment is not even attempted again

//        handler.CallCount.ShouldBe(0, "the handler must never be invoked for a message that only ever fails context establishment");
//        storage.Events.ShouldBeEmpty("the message must be force-dropped once MaxRetries is exceeded, exactly like a handler Retry");
//    }

//    [Fact]
//    public async Task a_context_failure_that_later_succeeds_processes_normally_and_is_never_dropped()
//    {
//        var subscription = new TestSubscription("sub-context-fail-then-succeed");
//        var evt = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var storage = new FakeOutboxStorage([evt]);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var hasFailedOnce = false;
//        var provider = new ConfigurableAmbientContextProvider(_ =>
//        {
//            if (hasFailedOnce)
//            {
//                return false;
//            }

//            hasFailedOnce = true;
//            return true;
//        });
//        var handler = new RecordingHandler();
//        var options = new OutboxProcessorOptions { MaxRetries = 5, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), options, timeProvider, provider);

//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(0);

//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct);

//        handler.CallCount.ShouldBe(1);
//        storage.Events.ShouldBeEmpty("the event must be processed and deleted once context establishment succeeds");
//    }

//    [Fact]
//    public async Task two_poison_messages_in_one_batch_are_force_dropped_in_sequence_without_resetting_each_other_while_other_messages_still_process()
//    {
//        var subscription = new TestSubscription("sub-two-poison-messages");
//        var poisonA = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var poisonB = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var normal1 = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var normal2 = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var storage = new FakeOutboxStorage([poisonA, poisonB, normal1, normal2]);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var provider = new ConfigurableAmbientContextProvider(evt => evt.MessageId == poisonA.MessageId || evt.MessageId == poisonB.MessageId);
//        var handler = new RecordingHandler();
//        var options = new OutboxProcessorOptions { MaxRetries = 2, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), options, timeProvider, provider);

//        // Cycle 1: poisonA claims the free slot; poisonB's failure is not recorded because
//        // the slot is already owned. Both non-poison messages are still processed.
//        await processor.RunProcessorAsync(_ct);
//        handler.Received.Select(e => e.MessageId).ShouldBe([normal1.MessageId, normal2.MessageId]);
//        storage.Events.Select(e => e.MessageId).ShouldBe([poisonA.MessageId, poisonB.MessageId]);

//        // Cycle 2: poisonA's attempt count reaches MaxRetries. poisonB still cannot claim the slot.
//        timeProvider.Advance(TimeSpan.FromSeconds(5));
//        await processor.RunProcessorAsync(_ct);
//        storage.Events.Select(e => e.MessageId).ShouldBe([poisonA.MessageId, poisonB.MessageId]);

//        // Cycle 3: poisonA is force-dropped, freeing the slot, which poisonB claims in the same cycle.
//        timeProvider.Advance(TimeSpan.FromSeconds(5));
//        await processor.RunProcessorAsync(_ct);
//        storage.Events.Select(e => e.MessageId).ShouldBe([poisonB.MessageId]);

//        // Cycle 4: poisonB's attempt count reaches MaxRetries.
//        timeProvider.Advance(TimeSpan.FromSeconds(5));
//        await processor.RunProcessorAsync(_ct);
//        storage.Events.Select(e => e.MessageId).ShouldBe([poisonB.MessageId]);

//        // Cycle 5: poisonB is force-dropped too.
//        timeProvider.Advance(TimeSpan.FromSeconds(5));
//        await processor.RunProcessorAsync(_ct);
//        storage.Events.ShouldBeEmpty();

//        handler.CallCount.ShouldBe(2, "the handler must only ever be invoked for the two non-poison messages");
//    }

//    [Fact]
//    public async Task context_poison_progresses_to_force_drop_despite_a_later_retrying_handler_message()
//    {
//        // A message whose context establishment fails claims the subscription's shared retry
//        // slot first. A later, healthy message whose handler asks for a Retry must not be
//        // able to steal or reset that ownership: otherwise the poisoned message would never
//        // accumulate enough attempts to reach MaxRetries and be force-dropped, since the
//        // retrying message would keep resetting the slot back to attempt 1 for itself.
//        var subscription = new TestSubscription("sub-poison-vs-retry");
//        var poison = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var retrying = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var storage = new FakeOutboxStorage([poison, retrying]);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var provider = new ConfigurableAmbientContextProvider(evt => evt.MessageId == poison.MessageId);
//        var handler = new ConfigurableHandler([HandleOutcomeResult.Retry("keeps asking to retry")]);
//        var options = new OutboxProcessorOptions { MaxRetries = 2, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), options, timeProvider, provider);

//        // Cycle 1: poison claims the free slot (attempt 1). The retrying message reaches its
//        // handler (context establishment succeeds for it) and asks for a Retry, but must not
//        // be granted the slot since poison already owns it.
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(1, "the healthy message's handler still runs even though it can't claim the slot");
//        storage.Events.Select(e => e.MessageId).ShouldBe([poison.MessageId, retrying.MessageId]);

//        // Cycle 2: poison's attempt count reaches MaxRetries. The retrying message is still
//        // blocked from claiming the slot.
//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct);
//        handler.CallCount.ShouldBe(2);
//        storage.Events.Select(e => e.MessageId).ShouldBe([poison.MessageId, retrying.MessageId]);

//        // Cycle 3: poison is force-dropped, freeing the slot. The retrying message can now
//        // claim it and its own retry is honored (ordering is preserved: it stops the batch).
//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct);
//        storage.Events.Select(e => e.MessageId).ShouldBe([retrying.MessageId], "poison must be force-dropped once MaxRetries is exceeded, despite the other message's repeated retry requests");
//        handler.CallCount.ShouldBe(3);
//    }

//    [Fact]
//    public async Task stale_retry_owner_is_cleared_when_externally_deleted_letting_another_context_failing_event_claim_the_slot()
//    {
//        // The message owning the retry slot can be removed from the store by something other
//        // than the processor itself (e.g. manual cleanup, or a separate deletion path) between
//        // cycles. Once that happens, the slot is stale: it must be freed as soon as the owner
//        // is found to be absent from the fetched page, rather than only when the page is
//        // entirely empty, otherwise every other event permanently loses the ability to ever
//        // claim the slot.
//        var subscription = new TestSubscription("sub-stale-owner-deleted");
//        var owner = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var other = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var storage = new FakeOutboxStorage([owner, other]);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var provider = new ConfigurableAmbientContextProvider(evt => evt.MessageId == owner.MessageId || evt.MessageId == other.MessageId);
//        var handler = new RecordingHandler();
//        var options = new OutboxProcessorOptions { MaxRetries = 2, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), options, timeProvider, provider);

//        // Cycle 1: owner claims the free slot. other's context failure is dropped since the
//        // slot is already owned.
//        await processor.RunProcessorAsync(_ct);
//        provider.AttemptCount.ShouldBe(2, "both events attempt context establishment even though only one can claim the slot");

//        // Simulate external deletion of the slot's owner (not via the processor's own
//        // force-drop path).
//        await storage.DeleteOutboxEventsAsync([owner.MessageId], _ct);

//        timeProvider.Advance(TimeSpan.FromSeconds(2));

//        // Cycle 2: owner is no longer in the fetched page, so the stale slot must be freed
//        // before other is processed, letting other claim it (attempt 1, not blocked forever).
//        await processor.RunProcessorAsync(_ct);
//        timeProvider.Advance(TimeSpan.FromSeconds(2));

//        // Cycle 3: other's attempt count reaches MaxRetries, proving it now owns the slot
//        // and is progressing normally rather than being permanently stuck.
//        await processor.RunProcessorAsync(_ct);
//        storage.Events.Select(e => e.MessageId).ShouldBe([other.MessageId]);

//        timeProvider.Advance(TimeSpan.FromSeconds(2));
//        await processor.RunProcessorAsync(_ct);
//        storage.Events.ShouldBeEmpty("other must be force-dropped once it exceeds MaxRetries, proving it successfully claimed and progressed on the freed slot");
//    }

//    [Fact]
//    public async Task context_establishment_failure_logs_context_establishment_failed_event_with_pool_and_retry_after_utc()
//    {
//        // Context establishment failures happen before the handler is ever reached, so they
//        // must be logged as their own distinct event (carrying the pool id, which a handler
//        // exception wouldn't have) rather than reusing HandlerException, which would blur the
//        // two very different failure modes together in logs/alerts.
//        var subscription = new TestSubscription("sub-context-fail-log-event");
//        var evt = CreatePersistedEvent(subscription.SubscriberName, poolId: 7);
//        var storage = new FakeOutboxStorage([evt]);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var provider = new ConfigurableAmbientContextProvider(_ => true);
//        var handler = new RecordingHandler();
//        var options = new OutboxProcessorOptions { MaxRetries = 5, RetryDelay = TimeSpan.FromSeconds(1) };
//        var recordingLogger = new RecordingLogger<OutboxProcessor>();
//        var processor = new OutboxProcessor(
//            new SimplePartitionedStorageFactory(storage),
//            [subscription],
//            CreateScopeFactory((subscription.SubscriberName.Value, handler)),
//            provider,
//            Options.Create(options),
//            timeProvider,
//            recordingLogger);

//        await processor.RunProcessorAsync(_ct);

//        var entry = recordingLogger.Entries.Single(e => e.EventName == nameof(OutboxProcessorLog.ContextEstablishmentFailed));
//        entry.EventName.ShouldBe(nameof(OutboxProcessorLog.ContextEstablishmentFailed));
//        entry.State.ShouldContain(kv => kv.Key == "PoolId" && Equals(kv.Value, 7));
//        entry.State.ShouldContain(kv => kv.Key == "MessageId" && Equals(kv.Value, evt.MessageId.Value));
//        entry.State.ShouldContain(kv => kv.Key == "RetryAfterUtc");
//        recordingLogger.Entries.ShouldNotContain(e => e.EventName == nameof(OutboxProcessorLog.HandlerException),
//            "a context establishment failure must not be logged as a HandlerException");
//    }

//    [Fact]
//    public async Task stale_retry_owner_is_not_cleared_when_absent_only_because_of_a_partial_page()
//    {
//        // The retry slot owner can legitimately be absent from a *partial* page (HasMore is
//        // true) simply because BatchSize is smaller than the subscription's backlog and other,
//        // still-pending events sort ahead of it -- not because it was deleted. Clearing the
//        // slot in that case would silently discard the owner's accumulated attempt count and
//        // let a later event claim ownership prematurely.
//        var subscription = new TestSubscription("sub-stale-owner-partial-page");
//        var owner = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var storage = new FakeOutboxStorage([owner]);

//        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
//        var provider = new SingleMessageFailingAmbientContextProvider(owner.MessageId);
//        var handler = new RecordingHandler();
//        var options = new OutboxProcessorOptions { BatchSize = 1, MaxRetries = 5, RetryDelay = TimeSpan.FromSeconds(1) };
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), options, timeProvider, provider);

//        // Cycle 1: owner is the only event, claims the free slot (attempt 1).
//        await processor.RunProcessorAsync(_ct);
//        provider.FailureAttemptCount.ShouldBe(1);

//        // Two healthy decoy events arrive ahead of owner in the queue. With BatchSize=1, every
//        // following cycle's page contains only a decoy, never reaching owner, while HasMore is
//        // true throughout.
//        var decoy1 = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var decoy2 = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        storage.PrependEvents([decoy1, decoy2]);
//        timeProvider.Advance(TimeSpan.FromSeconds(2));

//        // Cycle 2: page = [decoy1], HasMore = true. Owner is absent from this page but must not
//        // be treated as deleted; the decoy is handled and removed normally.
//        await processor.RunProcessorAsync(_ct);
//        provider.FailureAttemptCount.ShouldBe(1, "owner must not be re-attempted or have its slot cleared while merely absent from a partial page");
//        storage.Events.Select(e => e.MessageId).ShouldBe([decoy2.MessageId, owner.MessageId]);
//        handler.CallCount.ShouldBe(1);

//        // Cycle 3: page = [decoy2], HasMore = true. Same as above.
//        await processor.RunProcessorAsync(_ct);
//        provider.FailureAttemptCount.ShouldBe(1, "owner's slot must survive a second consecutive partial page that omits it");
//        storage.Events.Select(e => e.MessageId).ShouldBe([owner.MessageId]);
//        handler.CallCount.ShouldBe(2);

//        // Cycle 4: page = [owner], HasMore = false. Owner is reached again and its attempt count
//        // has survived at 1 (not reset to 0/cleared) across the two partial-page cycles, so this
//        // attempt becomes attempt 2, not a fresh attempt 1.
//        await processor.RunProcessorAsync(_ct);
//        provider.FailureAttemptCount.ShouldBe(2, "owner's attempt count must carry over rather than being reset by the intervening partial pages");
//        storage.Events.ShouldBe([owner]);
//    }

//    [Fact]
//    public async Task a_retained_retry_event_stops_the_batch_even_when_it_does_not_own_the_slot()
//    {
//        // Regardless of whether this event's Retry is granted the shared retry slot (it isn't,
//        // here, since an earlier context failure already owns it), the event itself is retained
//        // -- never added to processedIds -- so subscription ordering requires that no event after
//        // it in this batch is processed or deleted this cycle either.
//        var subscription = new TestSubscription("sub-retry-blocks-later-even-without-slot");
//        var poison = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var retrying = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var healthy = CreatePersistedEvent(subscription.SubscriberName, poolId: 1);
//        var storage = new FakeOutboxStorage([poison, retrying, healthy]);

//        var provider = new ConfigurableAmbientContextProvider(evt => evt.MessageId == poison.MessageId);
//        var handler = new ConfigurableHandler([HandleOutcomeResult.Retry("not ready yet")]);
//        var processor = CreateProcessor(storage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), provider: provider);

//        await processor.RunProcessorAsync(_ct);

//        // Only the retrying message's handler ran; healthy was never reached, because the
//        // batch stopped at retrying even though retrying could not claim the (already-owned)
//        // retry slot.
//        handler.CallCount.ShouldBe(1);
//        storage.Events.Select(e => e.MessageId).ShouldBe([poison.MessageId, retrying.MessageId, healthy.MessageId],
//            "no event after the retained retrying message may be processed or deleted this cycle");
//    }

//    private static PersistedOutboxEvent CreatePersistedEvent(SubscriberName subscriptionName, int poolId) => new()
//    {
//        MessageId = OutboxEventId.New(),
//        EventId = OutboxEventId.New(),
//        Timestamp = DateTimeOffset.UtcNow,
//        SequenceNumber = 1,
//        EventName = OutboxEventName.Create("ProcessingTestEvent"),
//        SubjectId = UuidV7.New(),
//        EntityTypeName = nameof(TestDso),
//        EntityTypeId = (int)TestDso.DsoVersion.EntityType.Id,
//        PoolId = poolId,
//        Payload = JsonSerializer.Serialize(new TestDso("ambient-context-test")),
//        SubscriberName = subscriptionName,
//    };

//    private sealed class ProbeAmbientContextProvider : IAmbientOutboxProcessingContextProvider
//    {
//        private static readonly AsyncLocal<string?> Ambient = new();

//        private readonly List<string?> _previousValuesAtEstablish = [];
//        private readonly List<string> _timeline = [];
//        private int _disposeCount;

//        public IReadOnlyList<string?> PreviousValuesAtEstablish => _previousValuesAtEstablish;

//        public IReadOnlyList<string> Timeline => _timeline;

//        public int DisposeCount => _disposeCount;

//        public async Task<HandleOutcomeResult> ExecuteWithContextAsync(
//            PersistedOutboxEvent evt,
//            Func<Ct, Task<HandleOutcomeResult>> handlerAsync,
//            Ct ct)
//        {
//            _previousValuesAtEstablish.Add(Ambient.Value);
//            var poolId = $"pool-{evt.PoolId.Value}";
//            _timeline.Add($"establish:{poolId}");
//            Ambient.Value = poolId;
//            try
//            {
//                return await handlerAsync(ct);
//            }
//            finally
//            {
//                _disposeCount++;
//                _timeline.Add("dispose");
//                Ambient.Value = null;
//            }
//        }

//        internal static string? CurrentAmbient => Ambient.Value;
//    }

//    private sealed class ProbeHandler : IOutboxSubscriptionHandler
//    {
//        private readonly List<string?> _observed = [];

//        public IReadOnlyList<string?> ObservedAmbientPoolIds => _observed;

//        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
//        {
//            _observed.Add(ProbeAmbientContextProvider.CurrentAmbient);
//            return Task.FromResult(HandleOutcomeResult.Success());
//        }
//    }

//    private sealed class ConfigurableAmbientContextProvider(Func<PersistedOutboxEvent, bool> shouldFail) : IAmbientOutboxProcessingContextProvider
//    {
//        private int _attemptCount;

//        public int AttemptCount => _attemptCount;

//        public Task<HandleOutcomeResult> ExecuteWithContextAsync(
//            PersistedOutboxEvent evt,
//            Func<Ct, Task<HandleOutcomeResult>> handlerAsync,
//            Ct ct)
//        {
//            _attemptCount++;
//            if (shouldFail(evt))
//            {
//                throw new InvalidOperationException("simulated context establishment failure");
//            }

//            return handlerAsync(ct);
//        }
//    }

//    // Fails context establishment only for a single, targeted message id and counts only those
//    // targeted (failing) attempts, unlike ConfigurableAmbientContextProvider.AttemptCount, which
//    // counts every event's attempt including ones that succeed.
//    private sealed class SingleMessageFailingAmbientContextProvider(OutboxEventId targetMessageId) : IAmbientOutboxProcessingContextProvider
//    {
//        private int _failureAttemptCount;

//        public int FailureAttemptCount => _failureAttemptCount;

//        public Task<HandleOutcomeResult> ExecuteWithContextAsync(
//            PersistedOutboxEvent evt,
//            Func<Ct, Task<HandleOutcomeResult>> handlerAsync,
//            Ct ct)
//        {
//            if (evt.MessageId == targetMessageId)
//            {
//                _failureAttemptCount++;
//                throw new InvalidOperationException("simulated context establishment failure");
//            }

//            return handlerAsync(ct);
//        }
//    }

//    private sealed class FakeOutboxStorage(IReadOnlyList<PersistedOutboxEvent> events) : IPartitionedStorage
//    {
//        private readonly List<PersistedOutboxEvent> _events = events.ToList();

//        public IReadOnlyList<PersistedOutboxEvent> Events => _events;

//        // Test-only hook simulating new events arriving ahead of an already-fetched, still
//        // pending event (e.g. one currently owning the retry slot), so a subsequent, smaller
//        // page can legitimately omit it without it having been deleted.
//        internal void PrependEvents(IEnumerable<PersistedOutboxEvent> events) => _events.InsertRange(0, events);

//        void IPartitionedStorage.SetPoolId(PoolId poolId) { }

//        public Task<OutboxEventsPage> GetOutboxEventsForSubscriptionAsync(SubscriberName subscriptionName, int count, Ct ct) =>
//            Task.FromResult(new OutboxEventsPage(_events.Take(count).ToList(), _events.Count > count));

//        public Task DeleteOutboxEventsAsync(IReadOnlyList<OutboxEventId> ids, Ct ct)
//        {
//            _ = _events.RemoveAll(e => ids.Contains(e.MessageId));
//            return Task.CompletedTask;
//        }

//        public Task<CreateResult> CreateAsync<TDso>(UuidV7 id, TDso value, IReadOnlyCollection<DataStorageKey> keys, SearchFieldCollection searchFieldCollection, Expiration expiration, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) where TDso : IDataStorageObject =>
//            throw new NotSupportedException();

//        public Task<StoreGetResult> TryReadAsync(EntityType type, UuidV7 id, Ct ct) => throw new NotSupportedException();

//        public Task<StoreGetResult> TryReadAsync(EntityType type, DataStorageKey key, Ct ct) => throw new NotSupportedException();

//        public Task<IReadOnlyList<StoreGetResult>> TryReadManyAsync(EntityType entityType, IReadOnlySet<UuidV7> ids, int maximum, Ct ct) => throw new NotSupportedException();

//        public Task<UpdateResult> UpdateAsync<TDso>(UuidV7 id, TDso dso, int expectedEntityVersion, IReadOnlyCollection<DataStorageKey> keys, SearchFieldCollection searchFieldCollection, Expiration? expiration, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) where TDso : IDataStorageObject =>
//            throw new NotSupportedException();

//        public Task<DeleteResult> DeleteAsync(EntityType entityType, UuidV7 id, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) => throw new NotSupportedException();

//        public Task<DeleteResult> DeleteAsync(EntityType entityType, DataStorageKey key, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) => throw new NotSupportedException();

//        public Task<LinkResult> LinkAsync(LinkDefinition definition, UuidV7 leftEntityId, UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) => throw new NotSupportedException();

//        public Task<UnlinkResult> UnlinkAsync(LinkDefinition definition, UuidV7 leftEntityId, UuidV7 rightEntityId, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) => throw new NotSupportedException();

//        public Task<int> PurgeExpiredAsync(int batchSize, Ct ct) => throw new NotSupportedException();

//        public Task<PurgeResult> PurgePoolAsync(Ct ct) => throw new NotSupportedException();

//        public Task<PurgeResult> PurgePoolAsync(int batchSize, Ct ct) => throw new NotSupportedException();

//        public Task<BatchResult> ExecuteBatchAsync(IReadOnlyList<IStorageOperation> operations, IReadOnlyList<OutboxEvent> outboxEvents, Ct ct) => throw new NotSupportedException();

//        public Task<QueryResult<MetadataEnvelope<TDso>>> QueryAsync<TDso>(EntityType entityType, IQueryExpression filter, SortParameter sort, DataRange dataRange, Ct ct) where TDso : IDataStorageObject =>
//            throw new NotSupportedException();

//        public Task<QueryResult<ProjectedResult>> QueryFieldsAsync(EntityType entityType, IReadOnlyCollection<Field> fields, IQueryExpression filter, SortParameter sort, DataRange dataRange, Ct ct) =>
//            throw new NotSupportedException();

//        public Task<QueryResult<MetadataEnvelope<TDso>>> QueryLinksAsync<TDso>(LinkQueryDescriptor query, DataRange dataRange, Ct ct) where TDso : IDataStorageObject =>
//            throw new NotSupportedException();

//        public Task<long> CountAsync(EntityType entityType, IQueryExpression? filter, Ct ct) => throw new NotSupportedException();
//    }

//    private static OutboxProcessor CreateProcessor(
//        IPartitionedStorage partitionedStorage,
//        IOutboxSubscription subscription,
//        IOutboxSubscriptionHandler handler,
//        OutboxProcessorOptions? options = null,
//        TimeProvider? timeProvider = null,
//        IAmbientOutboxProcessingContextProvider? provider = null) =>
//        CreateProcessor(partitionedStorage, [subscription], CreateScopeFactory((subscription.SubscriberName.Value, handler)), options, timeProvider, provider);

//    private static OutboxProcessor CreateProcessor(
//        IPartitionedStorage partitionedStorage,
//        IReadOnlyList<IOutboxSubscription> subscriptions,
//        IServiceScopeFactory scopeFactory,
//        OutboxProcessorOptions? options = null,
//        TimeProvider? timeProvider = null,
//        IAmbientOutboxProcessingContextProvider? provider = null) =>
//        CreateProcessor(new SimplePartitionedStorageFactory(partitionedStorage), subscriptions, scopeFactory, options, timeProvider, provider);

//    private static OutboxProcessor CreateProcessor(
//        IPartitionedStorageFactory partitionedStorageFactory,
//        IReadOnlyList<IOutboxSubscription> subscriptions,
//        IServiceScopeFactory scopeFactory,
//        OutboxProcessorOptions? options = null,
//        TimeProvider? timeProvider = null,
//        IAmbientOutboxProcessingContextProvider? provider = null) =>
//        new(
//            partitionedStorageFactory,
//            subscriptions,
//            scopeFactory,
//            provider ?? new DefaultAmbientOutboxProcessingContextProvider(),
//            Options.Create(options ?? new OutboxProcessorOptions()),
//            timeProvider ?? TimeProvider.System,
//            NullLogger<OutboxProcessor>.Instance);

//    private static IServiceScopeFactory CreateScopeFactory(params (string SubscriberName, IOutboxSubscriptionHandler Handler)[] handlers)
//    {
//        var services = new ServiceCollection();
//        foreach (var (name, handler) in handlers)
//        {
//            _ = services.AddKeyedTransient<IOutboxSubscriptionHandler>(name, (_, _) => handler);
//        }

//        var sp = services.BuildServiceProvider();
//        return sp.GetRequiredService<IServiceScopeFactory>();
//    }

//    private async Task<IStorageFixture> CreateFixtureAsync(params IOutboxSubscription[] subscriptions) =>
//        await FixtureFactory.CreateAsync(
//            _ct,
//            services =>
//            {
//                foreach (var subscription in subscriptions)
//                {
//                    _ = services.AddSingleton(subscription);
//                }

//                services.AddDsoRegistration<TestDso>();
//            });

//    private static async Task<(UuidV7 SubjectId, OutboxEvent Written)> SeedEventAsync(
//        IPartitionedStorage partitionedStorage,
//        int? entityTypeId = null)
//    {
//        var id = UuidV7.New();
//        var evt = new OutboxEvent
//        {
//            Id = OutboxEventId.New(),
//            Timestamp = DateTimeOffset.UtcNow,
//            EventName = OutboxEventName.Create("ProcessingTestEvent"),
//            SubjectId = id,
//            EntityTypeName = nameof(TestDso),
//            EntityTypeId = entityTypeId ?? (int)TestDso.DsoVersion.EntityType.Id,
//            Payload = JsonSerializer.Serialize(new TestDso("outbox-processing-test")),
//            DsoTypeSchemaVersion = (int)TestDso.DsoVersion.SchemaVersion,
//        };

//        _ = await partitionedStorage.CreateAsync(id, new TestDso("outbox-processing-test"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], CancellationToken.None);
//        return (id, evt);
//    }

//    private sealed class TestSubscription(
//        string name,
//        bool isEnabled = true,
//        IReadOnlySet<int>? entityTypeIds = null,
//        IReadOnlySet<OutboxEventName>? eventNames = null) : IOutboxSubscription
//    {
//        public SubscriberName SubscriberName { get; } = SubscriberName.Create(name);
//        public bool IsEnabled { get; set; } = isEnabled;
//        public IReadOnlySet<OutboxEventName> EventNames { get; } = eventNames ?? new HashSet<OutboxEventName>();
//        public IReadOnlySet<int> EntityTypeIds { get; } = entityTypeIds ?? new HashSet<int>();
//    }

//    private sealed class RecordingHandler(HandleOutcomeResult? outcome = null) : IOutboxSubscriptionHandler
//    {
//        private readonly List<PersistedOutboxEvent> _received = [];

//        public IReadOnlyList<PersistedOutboxEvent> Received => _received;

//        public int CallCount => _received.Count;

//        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
//        {
//            _received.Add(item);
//            return Task.FromResult(outcome ?? HandleOutcomeResult.Success());
//        }
//    }

//    private sealed class ConfigurableHandler(IReadOnlyList<HandleOutcomeResult> outcomes) : IOutboxSubscriptionHandler
//    {
//        private int _callIndex;

//        public int CallCount => _callIndex;

//        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
//        {
//            var index = _callIndex < outcomes.Count ? _callIndex : outcomes.Count - 1;
//            _callIndex++;
//            return Task.FromResult(outcomes[index]);
//        }
//    }

//    private sealed class ThrowingHandler(Func<int, Exception> exceptionFactory) : IOutboxSubscriptionHandler
//    {
//        private int _callIndex;

//        public int CallCount => _callIndex;

//        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
//        {
//            var index = _callIndex;
//            _callIndex++;
//            throw exceptionFactory(index);
//        }
//    }

//    // Simulates a handler that cooperatively cancels the shared token and then throws
//    // OperationCanceledException itself, exercising the processor's rethrow guard
//    // (rather than the earlier per-event ThrowIfCancellationRequested guard).
//    private sealed class CancellingHandler(CancellationTokenSource cts) : IOutboxSubscriptionHandler
//    {
//        private int _callCount;

//        public int CallCount => _callCount;

//        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
//        {
//            _callCount++;
//            cts.Cancel();
//            throw new OperationCanceledException(cts.Token);
//        }
//    }

//    private sealed class SimplePartitionedStorageFactory(IPartitionedStorage partitionedStorage) : IPartitionedStorageFactory
//    {
//        public Task<IPartitionedStorage> GetPartitionedStorageAsync(object? storageKey, CancellationToken ct) => Task.FromResult(partitionedStorage);
//    }

//    private sealed class RecordingPartitionedStorageFactory(IPartitionedStorage partitionedStorage) : IPartitionedStorageFactory
//    {
//        private readonly List<object?> _receivedStorageKeys = [];

//        public IReadOnlyList<object?> ReceivedStorageKeys => _receivedStorageKeys;

//        public Task<IPartitionedStorage> GetPartitionedStorageAsync(object? storageKey, CancellationToken ct)
//        {
//            _receivedStorageKeys.Add(storageKey);
//            return Task.FromResult(partitionedStorage);
//        }
//    }

//    private sealed class ThrowingPartitionedStorageFactory : IPartitionedStorageFactory
//    {
//        public Task<IPartitionedStorage> GetPartitionedStorageAsync(object? storageKey, CancellationToken ct) => throw new InvalidOperationException("Simulated factory failure");
//    }

//    private sealed record LoggedEntry(string? EventName, IReadOnlyList<KeyValuePair<string, object?>> State);

//    private sealed class RecordingLogger<T> : ILogger<T>
//    {
//        private readonly List<LoggedEntry> _entries = [];

//        public IReadOnlyList<LoggedEntry> Entries => _entries;

//        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

//        public bool IsEnabled(LogLevel logLevel) => true;

//        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
//        {
//            var values = state as IReadOnlyList<KeyValuePair<string, object?>>
//                ?? (state as IEnumerable<KeyValuePair<string, object?>>)?.ToList()
//                ?? [];
//            _entries.Add(new LoggedEntry(eventId.Name, values));
//        }
//    }
//}
