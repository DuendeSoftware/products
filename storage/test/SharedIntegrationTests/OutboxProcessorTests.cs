// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying.SearchFields;
using Microsoft.Extensions.DependencyInjection;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Live, all-provider integration tests for <see cref="OutboxProcessor"/>: everything here
/// drives a real, DI-resolved processor against real storage. Events are identified by
/// <see cref="PersistedOutboxEvent.EventId"/> (the caller's id), never by
/// <see cref="PersistedOutboxEvent.MessageId"/> (store-generated). No test asserts handling
/// order across events: the processor makes no ordering guarantee, so assertions compare sets
/// per pass.
/// </summary>
public partial class OutboxProcessorTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task successful_event_is_deleted_and_not_delivered_again()
    {
        var subscription = new TestSubscription("sub-success");
        var handler = new RecordingHandler();
        await using var fixture = await CreateFixtureAsync(subscription, handler);
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        handler.CallCount.ShouldBe(1);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();

        await processor.RunProcessorAsync(_ct);
        handler.CallCount.ShouldBe(1, "a second pass over an already-drained queue must not re-invoke the handler");
    }

    [Fact]
    public async Task dropped_event_is_deleted()
    {
        var subscription = new TestSubscription("sub-drop");
        var handler = new RecordingHandler(HandleOutcomeResult.Drop("not needed"));
        await using var fixture = await CreateFixtureAsync(subscription, handler);
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        handler.CallCount.ShouldBe(1);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task delivered_event_matches_what_was_written()
    {
        var subscription = new TestSubscription("sub-fidelity");
        var handler = new RecordingHandler();
        await using var fixture = await CreateFixtureAsync(subscription, handler);
        var (subjectId, written) = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        handler.Received.Count.ShouldBe(1);
        var delivered = handler.Received[0];
        delivered.EventId.ShouldBe(written.Id);
        delivered.EventName.ShouldBe(written.EventName);
        delivered.EntityTypeName.ShouldBe(written.EntityTypeName);
        delivered.EntityTypeId.ShouldBe(written.EntityTypeId);
        delivered.SubjectId.ShouldBe(subjectId);
        // Compared as parsed JSON rather than raw string, since a round trip through
        // PostgreSQL's jsonb column can reformat (e.g. re-order or re-space) the text
        // without changing its meaning.
        JsonNode.DeepEquals(JsonNode.Parse(delivered.Payload), JsonNode.Parse(written.Payload)).ShouldBeTrue();
        delivered.SubscriberName.ShouldBe(subscription.SubscriberName);
        delivered.MessageId.ShouldNotBe(default);
        delivered.SequenceNumber.ShouldBeGreaterThan(0);
        (delivered.Dso is TestDso).ShouldBeTrue();
    }

    [Fact]
    public async Task failed_event_is_kept_and_later_events_in_the_same_page_are_still_handled()
    {
        var subscription = new TestSubscription("sub-partial-failure");
        var handler = new ConditionalOutcomeHandler(_ => HandleOutcomeResult.Success());
        await using var fixture = await CreateFixtureAsync(subscription, handler);
        var written = await SeedEventsAsync(fixture, 3);
        var failingEventId = written[0].Id;
        handler.NextOutcome = evt => evt.EventId == failingEventId
            ? HandleOutcomeResult.Retry("not ready yet")
            : HandleOutcomeResult.Success();

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        handler.CallCount.ShouldBe(3, "every event in the page is attempted, even though one of them fails");
        var remaining = await RemainingEventIdsAsync(fixture, subscription.SubscriberName);
        remaining.ShouldBe([failingEventId]);
    }

    [Fact]
    public async Task failed_event_is_retried_on_the_next_pass_and_deleted_on_success()
    {
        var subscription = new TestSubscription("sub-retry-then-success");
        var handler = new ConditionalOutcomeHandler(_ => HandleOutcomeResult.Retry("not ready yet"));
        await using var fixture = await CreateFixtureAsync(subscription, handler);
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);
        handler.CallCount.ShouldBe(1);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);

        handler.NextOutcome = _ => HandleOutcomeResult.Success();
        await processor.RunProcessorAsync(_ct);

        handler.CallCount.ShouldBe(2);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task event_is_dropped_in_the_pass_of_its_last_allowed_attempt()
    {
        var subscription = new TestSubscription("sub-max-retries");
        var handler = new ConditionalOutcomeHandler(_ => HandleOutcomeResult.Retry("always fails"));
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 2));
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();

        await processor.RunProcessorAsync(_ct);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);

        await processor.RunProcessorAsync(_ct);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);

        await processor.RunProcessorAsync(_ct);
        handler.CallCount.ShouldBe(3, "MaxRetries + 1");
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();

        await processor.RunProcessorAsync(_ct);
        handler.CallCount.ShouldBe(3, "the row is already gone; a fourth pass must not call the handler again");
    }

    [Fact]
    public async Task several_failing_events_in_a_page_are_counted_independently()
    {
        var subscription = new TestSubscription("sub-independent-failures");
        var handler = new ConditionalOutcomeHandler(_ => HandleOutcomeResult.Success());
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 1));
        var written = await SeedEventsAsync(fixture, 4);
        var failingIds = new HashSet<OutboxEventId> { written[0].Id, written[1].Id };
        handler.NextOutcome = evt => failingIds.Contains(evt.EventId)
            ? HandleOutcomeResult.Retry("always fails")
            : HandleOutcomeResult.Success();

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        var afterPass1 = await RemainingEventIdsAsync(fixture, subscription.SubscriberName);
        afterPass1.ShouldBe(failingIds, ignoreOrder: true);

        await processor.RunProcessorAsync(_ct);

        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
        handler.CallCountFor(written[0].Id).ShouldBe(2);
        handler.CallCountFor(written[1].Id).ShouldBe(2);
        handler.CallCountFor(written[2].Id).ShouldBe(1, "healthy events must never be re-handled");
        handler.CallCountFor(written[3].Id).ShouldBe(1);
    }

    [Fact]
    public async Task next_page_holds_the_failed_events_plus_new_events_up_to_batch_size()
    {
        var subscription = new TestSubscription("sub-page-composition");
        var handler = new ConditionalOutcomeHandler(_ => HandleOutcomeResult.Success());
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o =>
            {
                o.BatchSize = 3;
                o.MaxRetries = 10;
            }));
        var written = await SeedEventsAsync(fixture, 3);
        var failingId = written[0].Id;
        handler.NextOutcome = evt => evt.EventId == failingId
            ? HandleOutcomeResult.Retry("always fails")
            : HandleOutcomeResult.Success();

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        handler.Received.Select(e => e.EventId).ShouldBe(
            [written[0].Id, written[1].Id, written[2].Id], ignoreOrder: true);

        var moreWritten = await SeedEventsAsync(fixture, 2);

        await processor.RunProcessorAsync(_ct);

        var pass2EventIds = handler.Received.Skip(3).Select(e => e.EventId).ToList();
        pass2EventIds.ShouldBe([failingId, moreWritten[0].Id, moreWritten[1].Id], ignoreOrder: true);
    }

    [Fact]
    public async Task fully_failing_page_holds_back_new_events_until_its_events_are_dropped()
    {
        var subscription = new TestSubscription("sub-fully-failing-page");
        var handler = new ConditionalOutcomeHandler(_ => HandleOutcomeResult.Success());
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o =>
            {
                o.BatchSize = 2;
                o.MaxRetries = 2;
            }));
        var written = await SeedEventsAsync(fixture, 2);
        var failingIds = new HashSet<OutboxEventId> { written[0].Id, written[1].Id };
        handler.NextOutcome = evt => failingIds.Contains(evt.EventId)
            ? HandleOutcomeResult.Retry("always fails")
            : HandleOutcomeResult.Success();

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);
        await processor.RunProcessorAsync(_ct);

        var thirdEvent = await SeedEventAsync(fixture);

        await processor.RunProcessorAsync(_ct);
        handler.Received.Select(e => e.EventId).ShouldNotContain(thirdEvent.Written.Id);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBe(
            [thirdEvent.Written.Id], ignoreOrder: true);
        handler.CallCountFor(written[0].Id).ShouldBe(3, "MaxRetries + 1");
        handler.CallCountFor(written[1].Id).ShouldBe(3);

        await processor.RunProcessorAsync(_ct);
        handler.CallCountFor(thirdEvent.Written.Id).ShouldBe(1);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task handler_exception_counts_as_a_failure()
    {
        var subscription = new TestSubscription("sub-handler-exception");
        var handler = new ThrowingHandler();
        var contextProvider = new CountingAmbientContextProvider();
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 1),
            contextProvider: contextProvider);
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));

        exception.ShouldBeNull();
        handler.CallCount.ShouldBe(1);
        contextProvider.DisposeCount.ShouldBe(1, "the ambient context must be torn down exactly once per attempt");
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);

        await processor.RunProcessorAsync(_ct);
        handler.CallCount.ShouldBe(2);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task context_establishment_failure_counts_as_a_failure_and_does_not_stop_the_page()
    {
        var subscription = new TestSubscription("sub-context-failure");
        var handler = new RecordingHandler();
        var provider = new FailingContextProvider();
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 1),
            contextProvider: provider);
        var written = await SeedEventsAsync(fixture, 3);
        var failingEventId = written[1].Id;
        provider.FailingEventId = failingEventId;

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        handler.Received.Select(e => e.EventId).ShouldBe([written[0].Id, written[2].Id], ignoreOrder: true);
        handler.Received.ShouldNotContain(e => e.EventId == failingEventId, "the handler never sees an event whose context failed to establish");
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBe([failingEventId]);

        await processor.RunProcessorAsync(_ct);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task handler_runs_inside_each_events_own_ambient_context()
    {
        var subscription = new TestSubscription("sub-ambient-context");
        var handler = new ProbeHandler();
        var provider = new ProbeAmbientContextProvider();
        await using var fixture = await CreateFixtureAsync(subscription, handler, contextProvider: provider);
        _ = await SeedEventsAsync(fixture, 2);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        handler.ObservedContexts.Count.ShouldBe(2);
        handler.ObservedContexts.Distinct().Count().ShouldBe(2, "each event must see its own, distinct ambient context");
        handler.ObservedContexts.ShouldAllBe(c => c != null);

        // The timeline alternates establish/dispose per event, with nothing leaked between them:
        // no two "establish" entries appear without a "dispose" in between.
        provider.Timeline.Count.ShouldBe(4);
        for (var i = 0; i < provider.Timeline.Count; i += 2)
        {
            provider.Timeline[i].ShouldStartWith("establish:");
            provider.Timeline[i + 1].ShouldBe("dispose");
        }

        provider.PreviousValuesAtEstablish.ShouldAllBe(v => v == null, "no event must observe a context left over from a previous event");
    }

    [Fact]
    public async Task cancelled_pass_leaves_rows_and_attempt_counts_intact()
    {
        var subscription = new TestSubscription("sub-cancelled-pass");
        var handler = new ConditionalOutcomeHandler(_ => HandleOutcomeResult.Retry("always fails"));
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 1));
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();

        // Pass 1: fails, recording attempt 1.
        await processor.RunProcessorAsync(_ct);
        handler.CallCount.ShouldBe(1);

        // Pass 2: a pre-cancelled token must return without calling the handler, keeping the
        // previous attempt count intact.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await processor.RunProcessorAsync(cts.Token);
        handler.CallCount.ShouldBe(1, "a cancelled pass must not call the handler");
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);

        // Pass 3: fails again. If pass 2 had wiped the count, this would be attempt 1 again and
        // the row would survive; since the count survived, this is attempt 2 and the row drops.
        await processor.RunProcessorAsync(_ct);
        handler.CallCount.ShouldBe(2);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task cancellation_during_a_handler_does_not_count_as_a_failure()
    {
        var subscription = new TestSubscription("sub-cancel-not-counted");
        using var cts = new CancellationTokenSource();
        var handler = new CancelOnceThenRetryHandler(cts);
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 1));
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();

        // Pass 1: the handler cancels the token and throws OperationCanceledException itself.
        // This must not be recorded as a failed attempt.
        await processor.RunProcessorAsync(cts.Token);
        handler.CallCount.ShouldBe(1);

        // Pass 2 (fresh token): the handler returns Retry normally. If pass 1 had been counted,
        // this would be attempt 2 and MaxRetries=1 would drop the row; since it wasn't counted,
        // this is attempt 1 and the row survives.
        await processor.RunProcessorAsync(_ct);
        handler.CallCount.ShouldBe(2);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task cancellation_stops_the_pass_and_events_handled_before_it_are_redelivered()
    {
        var subA = new TestSubscription("sub-cancel-a");
        var subB = new TestSubscription("sub-cancel-b");
        using var cts = new CancellationTokenSource();
        var handlerA = new CancelOnSecondEventHandler(cts);
        var handlerB = new RecordingHandler();
        await using var fixture = await FixtureFactoryWithHandlers(
            [(subA, (IOutboxSubscriptionHandler)handlerA), (subB, handlerB)]);
        var written = await SeedEventsAsync(fixture, 2);
        handlerA.TargetEventId = written[1].Id;

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(cts.Token);

        handlerB.CallCount.ShouldBe(0, "the cancelled pass must stop before the next subscription is processed");
        (await RemainingEventIdsAsync(fixture, subA.SubscriberName)).Count.ShouldBe(2, "no rows are deleted when the pass is cancelled mid-page");
        (await RemainingEventIdsAsync(fixture, subB.SubscriberName)).Count.ShouldBe(2);

        await processor.RunProcessorAsync(_ct);

        (await RemainingEventIdsAsync(fixture, subA.SubscriberName)).ShouldBeEmpty();
        (await RemainingEventIdsAsync(fixture, subB.SubscriberName)).ShouldBeEmpty();
        handlerB.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task disabled_subscription_leaves_its_rows_queued_while_enabled_subscriptions_are_processed()
    {
        var disabled = new TestSubscription("sub-disabled", isEnabled: false);
        var enabled = new TestSubscription("sub-enabled");
        var disabledHandler = new RecordingHandler();
        var enabledHandler = new RecordingHandler();
        await using var fixture = await FixtureFactoryWithHandlers(
            [(disabled, (IOutboxSubscriptionHandler)disabledHandler), (enabled, enabledHandler)]);
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        disabledHandler.CallCount.ShouldBe(0);
        enabledHandler.CallCount.ShouldBe(1);
        (await RemainingEventIdsAsync(fixture, enabled.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task is_enabled_is_rechecked_on_every_pass()
    {
        var subscription = new TestSubscription("sub-dynamic-enabled");
        var handler = new RecordingHandler();
        await using var fixture = await CreateFixtureAsync(subscription, handler);
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);
        handler.CallCount.ShouldBe(1);

        subscription.IsEnabled = false;
        _ = await SeedEventAsync(fixture);
        await processor.RunProcessorAsync(_ct);

        handler.CallCount.ShouldBe(1, "a disabled subscription must not be processed, even though it was enabled when the processor was constructed");
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);

        subscription.IsEnabled = true;
        await processor.RunProcessorAsync(_ct);

        handler.CallCount.ShouldBe(2);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task missing_handler_leaves_rows_queued()
    {
        var subscription = new TestSubscription("sub-no-handler");
        await using var fixture = await FixtureFactory.CreateAsync(
            _ct,
            services =>
            {
                _ = services.AddSingleton<IOutboxSubscription>(subscription);
                services.AddDsoRegistration<TestDso>();
            });
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();

        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));
        exception.ShouldBeNull();
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);

        exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));
        exception.ShouldBeNull();
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task each_subscription_handles_its_own_copy_of_an_event()
    {
        var subA = new TestSubscription("sub-fanout-a");
        var subB = new TestSubscription("sub-fanout-b");
        var handlerA = new RecordingHandler();
        var handlerB = new RecordingHandler();
        await using var fixture = await FixtureFactoryWithHandlers(
            [(subA, (IOutboxSubscriptionHandler)handlerA), (subB, handlerB)]);
        var written = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        handlerA.Received.Count.ShouldBe(1);
        handlerB.Received.Count.ShouldBe(1);
        handlerA.Received[0].EventId.ShouldBe(written.Written.Id);
        handlerB.Received[0].EventId.ShouldBe(written.Written.Id);
        handlerA.Received[0].MessageId.ShouldNotBe(handlerB.Received[0].MessageId);

        (await RemainingEventIdsAsync(fixture, subA.SubscriberName)).ShouldBeEmpty();
        (await RemainingEventIdsAsync(fixture, subB.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task subscriptions_track_failures_independently()
    {
        var subA = new TestSubscription("sub-independent-a");
        var subB = new TestSubscription("sub-independent-b");
        var handlerA = new RecordingHandler(HandleOutcomeResult.Retry("always fails"));
        var handlerB = new RecordingHandler();
        await using var fixture = await FixtureFactoryWithHandlers(
            [(subA, (IOutboxSubscriptionHandler)handlerA), (subB, handlerB)],
            services => services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 1));
        _ = await SeedEventAsync(fixture);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        (await RemainingEventIdsAsync(fixture, subB.SubscriberName)).ShouldBeEmpty();
        (await RemainingEventIdsAsync(fixture, subA.SubscriberName)).Count.ShouldBe(1);

        await processor.RunProcessorAsync(_ct);

        (await RemainingEventIdsAsync(fixture, subA.SubscriberName)).ShouldBeEmpty();
        handlerA.CallCount.ShouldBe(2);
        handlerB.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task new_processor_instance_starts_attempt_counts_from_zero()
    {
        var subscription = new TestSubscription("sub-new-instance");
        var handler = new ConditionalOutcomeHandler(_ => HandleOutcomeResult.Retry("always fails"));
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 1));
        _ = await SeedEventAsync(fixture);

        var processor1 = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor1.RunProcessorAsync(_ct);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(1);

        var processor2 = ActivatorUtilities.CreateInstance<OutboxProcessor>(fixture.ServiceProvider);
        await processor2.RunProcessorAsync(_ct);

        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(
            1, "the new instance starts with an empty attempt map, so this is its first failed attempt, not its second");

        await processor2.RunProcessorAsync(_ct);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
    }

    [Fact]
    public async Task externally_deleted_failed_event_does_not_affect_the_other_events()
    {
        var subscription = new TestSubscription("sub-externally-deleted");
        var handler = new ConditionalOutcomeHandler(_ => HandleOutcomeResult.Retry("always fails"));
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 1));
        var written = await SeedEventsAsync(fixture, 2);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscription.SubscriberName, 100, _ct);
        var deletedRow = page.Events.Single(e => e.EventId == written[0].Id);
        await fixture.DefaultCrossPartitionStorage.DeleteOutboxEventsAsync([deletedRow.MessageId], _ct);

        var exception = await Record.ExceptionAsync(() => processor.RunProcessorAsync(_ct));

        exception.ShouldBeNull();
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
        handler.CallCountFor(written[1].Id).ShouldBe(2, "MaxRetries + 1");
    }

    [Fact]
    public async Task queue_larger_than_batch_size_drains_over_several_passes()
    {
        var subscription = new TestSubscription("sub-drain-over-passes");
        var handler = new RecordingHandler();
        await using var fixture = await CreateFixtureAsync(
            subscription,
            handler,
            configure: services => services.Configure<OutboxProcessorOptions>(o => o.BatchSize = 3));
        _ = await SeedEventsAsync(fixture, 5);

        var processor = fixture.ServiceProvider.GetRequiredService<OutboxProcessor>();
        await processor.RunProcessorAsync(_ct);

        handler.CallCount.ShouldBe(3);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).Count.ShouldBe(2);

        await processor.RunProcessorAsync(_ct);

        handler.CallCount.ShouldBe(5);
        (await RemainingEventIdsAsync(fixture, subscription.SubscriberName)).ShouldBeEmpty();
    }

    // Single-subscription convenience overload: registers the subscription and its handler as a
    // keyed singleton (so the same instance accumulates state across passes, matching how the
    // processor resolves a new scope every pass) before the DI container is built.
    private Task<IStorageFixture> CreateFixtureAsync(
        IOutboxSubscription subscription,
        IOutboxSubscriptionHandler handler,
        Action<IServiceCollection>? configure = null,
        IAmbientOutboxProcessingContextProvider? contextProvider = null) =>
        FixtureFactory.CreateAsync(
            _ct,
            services =>
            {
                _ = services.AddSingleton(subscription);
                _ = services.AddKeyedSingleton<IOutboxSubscriptionHandler>(subscription.SubscriberName.Value, handler);
                if (contextProvider is not null)
                {
                    _ = services.AddSingleton(contextProvider);
                }

                services.AddDsoRegistration<TestDso>();
                configure?.Invoke(services);
            });

    private Task<IStorageFixture> FixtureFactoryWithHandlers(
        IReadOnlyList<(IOutboxSubscription Subscription, IOutboxSubscriptionHandler Handler)> subscriptionsAndHandlers,
        Action<IServiceCollection>? configure = null) =>
        FixtureFactory.CreateAsync(
            _ct,
            services =>
            {
                foreach (var (subscription, handler) in subscriptionsAndHandlers)
                {
                    _ = services.AddSingleton(subscription);
                    _ = services.AddKeyedSingleton<IOutboxSubscriptionHandler>(subscription.SubscriberName.Value, handler);
                }

                services.AddDsoRegistration<TestDso>();
                configure?.Invoke(services);
            });

    private async Task<IReadOnlyList<OutboxEventId>> RemainingEventIdsAsync(IStorageFixture fixture, SubscriberName subscriberName) =>
        (await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriberName, 1000, _ct))
            .Events.Select(e => e.EventId).ToList();

    private async Task<(UuidV7 SubjectId, OutboxEvent Written)> SeedEventAsync(IStorageFixture fixture, string payloadValue = "outbox-processing-test")
    {
        var subjectId = UuidV7.New();
        var dso = new TestDso(payloadValue);
        var evt = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = OutboxEventName.Create("ProcessingTestEvent"),
            SubjectId = subjectId,
            EntityTypeName = nameof(TestDso),
            EntityTypeId = (int)TestDso.DsoVersion.EntityType.Id,
            Payload = JsonSerializer.Serialize(dso),
            DsoTypeSchemaVersion = (int)TestDso.DsoVersion.SchemaVersion,
        };

        _ = await fixture.PartitionedStorage.CreateAsync(subjectId, dso, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [evt], _ct);
        return (subjectId, evt);
    }

    private async Task<IReadOnlyList<OutboxEvent>> SeedEventsAsync(IStorageFixture fixture, int count)
    {
        var events = new List<OutboxEvent>();
        for (var i = 0; i < count; i++)
        {
            events.Add((await SeedEventAsync(fixture, $"payload-{i}")).Written);
        }

        return events;
    }

    private sealed class TestSubscription(
        string name,
        bool isEnabled = true,
        IReadOnlySet<int>? entityTypeIds = null,
        IReadOnlySet<OutboxEventName>? eventNames = null) : IOutboxSubscription
    {
        public SubscriberName SubscriberName { get; } = SubscriberName.Create(name);
        public bool IsEnabled { get; set; } = isEnabled;
        public IReadOnlySet<OutboxEventName> EventNames { get; } = eventNames ?? new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds { get; } = entityTypeIds ?? new HashSet<int>();
    }

    private sealed class RecordingHandler(HandleOutcomeResult? outcome = null) : IOutboxSubscriptionHandler
    {
        private readonly List<PersistedOutboxEvent> _received = [];

        public IReadOnlyList<PersistedOutboxEvent> Received => _received;

        public int CallCount => _received.Count;

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            _received.Add(item);
            return Task.FromResult(outcome ?? HandleOutcomeResult.Success());
        }
    }

    private sealed class ConditionalOutcomeHandler(Func<PersistedOutboxEvent, HandleOutcomeResult> outcomeSelector) : IOutboxSubscriptionHandler
    {
        private readonly List<PersistedOutboxEvent> _received = [];

        public IReadOnlyList<PersistedOutboxEvent> Received => _received;

        public int CallCount => _received.Count;

        public Func<PersistedOutboxEvent, HandleOutcomeResult> NextOutcome { get; set; } = outcomeSelector;

        public int CallCountFor(OutboxEventId eventId) => _received.Count(e => e.EventId == eventId);

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            _received.Add(item);
            return Task.FromResult(NextOutcome(item));
        }
    }

    private sealed class ThrowingHandler : IOutboxSubscriptionHandler
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            _callCount++;
            throw new InvalidOperationException("simulated handler failure");
        }
    }

    // Cancels the shared token and then throws OperationCanceledException itself on its first
    // call only, exercising the processor's rethrow guard; subsequent calls (a later pass, with
    // a fresh, non-cancelled token) return Retry normally.
    private sealed class CancelOnceThenRetryHandler(CancellationTokenSource cts) : IOutboxSubscriptionHandler
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            _callCount++;
            if (_callCount == 1)
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }

            return Task.FromResult(HandleOutcomeResult.Retry("still failing"));
        }
    }

    // Succeeds for every event except the targeted one, which it cancels on only the first time
    // it is seen (a later, uncancelled pass processes it normally). The target id is set after
    // construction, once the test has seeded events and knows their ids.
    private sealed class CancelOnSecondEventHandler(CancellationTokenSource cts) : IOutboxSubscriptionHandler
    {
        private bool _hasCancelled;

        public OutboxEventId? TargetEventId { get; set; }

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            if (!_hasCancelled && TargetEventId is not null && item.EventId == TargetEventId)
            {
                _hasCancelled = true;
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }

            return Task.FromResult(HandleOutcomeResult.Success());
        }
    }

    private sealed class CountingAmbientContextProvider : IAmbientOutboxProcessingContextProvider
    {
        private int _disposeCount;

        public int DisposeCount => _disposeCount;

        public async Task<HandleOutcomeResult> ExecuteWithContextAsync(
            PersistedOutboxEvent evt,
            Func<Ct, Task<HandleOutcomeResult>> handlerAsync,
            Ct ct)
        {
            try
            {
                return await handlerAsync(ct);
            }
            finally
            {
                _disposeCount++;
            }
        }
    }

    // The target event id is set after the fixture (and this singleton instance) are
    // constructed, once the test has seeded events and knows their ids.
    private sealed class FailingContextProvider : IAmbientOutboxProcessingContextProvider
    {
        public OutboxEventId? FailingEventId { get; set; }

        public Task<HandleOutcomeResult> ExecuteWithContextAsync(
            PersistedOutboxEvent evt,
            Func<Ct, Task<HandleOutcomeResult>> handlerAsync,
            Ct ct)
        {
            if (FailingEventId is not null && evt.EventId == FailingEventId)
            {
                throw new InvalidOperationException("simulated context establishment failure");
            }

            return handlerAsync(ct);
        }
    }

    private sealed class ProbeAmbientContextProvider : IAmbientOutboxProcessingContextProvider
    {
        private static readonly AsyncLocal<string?> Ambient = new();

        private readonly List<string?> _previousValuesAtEstablish = [];
        private readonly List<string> _timeline = [];

        public IReadOnlyList<string?> PreviousValuesAtEstablish => _previousValuesAtEstablish;

        public IReadOnlyList<string> Timeline => _timeline;

        public async Task<HandleOutcomeResult> ExecuteWithContextAsync(
            PersistedOutboxEvent evt,
            Func<Ct, Task<HandleOutcomeResult>> handlerAsync,
            Ct ct)
        {
            _previousValuesAtEstablish.Add(Ambient.Value);
            var contextId = $"message-{evt.MessageId.Value}";
            _timeline.Add($"establish:{contextId}");
            Ambient.Value = contextId;
            try
            {
                return await handlerAsync(ct);
            }
            finally
            {
                _timeline.Add("dispose");
                Ambient.Value = null;
            }
        }

        internal static string? CurrentAmbient => Ambient.Value;
    }

    private sealed class ProbeHandler : IOutboxSubscriptionHandler
    {
        private readonly List<string?> _observedContexts = [];

        public IReadOnlyList<string?> ObservedContexts => _observedContexts;

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            _observedContexts.Add(ProbeAmbientContextProvider.CurrentAmbient);
            return Task.FromResult(HandleOutcomeResult.Success());
        }
    }
}
