// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class SpaceAwareAmbientOutboxProcessingContextTests : IAsyncLifetime
{
    private static int _poolIdCounter = 500_000;
    private ServiceProvider _services = null!;
    private ISpaceAdmin _admin = null!;
    private ISpaceContextAccessor _spaceContext = null!;
    private IAmbientOutboxProcessingContextProvider _sut = null!;
    private static Ct Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpaces();
        TestSpacesLicense.RegisterEntitled(sc);
        sc.AddStorageInternal(b => b.AddSqliteInMemory());
        _services = sc.BuildServiceProvider();

        var storageInstanceSchema = _services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(Ct);

        _admin = _services.GetRequiredService<ISpaceAdmin>();
        _spaceContext = _services.GetRequiredService<ISpaceContextAccessor>();
        _sut = _services.GetRequiredService<IAmbientOutboxProcessingContextProvider>();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public void spaces_registration_replaces_storages_default_provider_as_transient()
    {
        _sut.ShouldBeOfType<SpaceAwareAmbientOutboxProcessingContext>();

        // Task 10 registers the default provider as Transient so Spaces can replace it; Spaces
        // must preserve that lifetime, not upgrade it to Singleton. Resolving twice must produce
        // two distinct instances.
        var second = _services.GetRequiredService<IAmbientOutboxProcessingContextProvider>();
        second.ShouldNotBeSameAs(_sut);
    }

    [Fact]
    public async Task handler_observes_the_resolved_space_while_it_runs()
    {
        var (poolId, spaceId) = await CreateSpaceAsync("Outbox Test Space");
        var evt = CreateEvent(poolId);

        SpaceId? observedDuringHandler = null;
        var outcome = await _sut.ExecuteWithContextAsync(
            evt,
            ct =>
            {
                // Observing ambient state must happen *inside* the handler delegate: this is
                // exactly what OutboxProcessor does (invoke the handler while context is
                // active), and it is the only representative place to assert from. Asserting
                // ambient state on the test's own thread after awaiting the whole call would
                // not prove anything OutboxProcessor itself can rely on - see the nested test
                // below for why a caller-side post-await assertion cannot observe restoration.
                observedDuringHandler = _spaceContext.GetSpaceId();
                return Task.FromResult(HandleOutcomeResult.Success());
            },
            Ct);

        outcome.ShouldBeOfType<HandleOutcomeResult.SuccessResult>();
        observedDuringHandler.ShouldBe(spaceId);
    }

    [Fact]
    public async Task nested_execute_with_context_calls_restore_the_enclosing_space_once_the_inner_call_returns()
    {
        // Restoration cannot be proven from the test's own async flow: once the test's own
        // `await _sut.ExecuteWithContextAsync(...)` call has genuinely suspended internally (a
        // real cache miss resolving the Space), .NET's ExecutionContext capture/restore
        // semantics mean the test's continuation always resumes using the context it captured
        // *before* making that call, regardless of whether the callee's `using` disposal ever
        // ran. An assertion made at the test level after such an await is tautological: it would
        // pass even if restoration were completely broken.
        //
        // To genuinely observe restoration, the assertion must live in the same async flow as
        // the disposal, without crossing back out through the suspending boundary first. This
        // test nests a second ExecuteWithContextAsync call inside the outer handler and asserts
        // immediately after the inner call returns, while still inside the outer handler. Both
        // PoolId -> Space cache entries are warmed up-front so the specific lookups exercised
        // during the nested assertion pass resolve synchronously; without that, the outer
        // handler's own await of the inner call would suspend for the same reason described
        // above, making its post-inner-call assertion equally tautological.
        var (outerPoolId, outerSpaceId) = await CreateSpaceAsync("Outer Nesting Space");
        var (innerPoolId, innerSpaceId) = await CreateSpaceAsync("Inner Nesting Space");

        // Warm both cache entries before the real (assertion-bearing) pass below.
        _ = await _sut.ExecuteWithContextAsync(CreateEvent(outerPoolId), _ => Task.FromResult(HandleOutcomeResult.Success()), Ct);
        _ = await _sut.ExecuteWithContextAsync(CreateEvent(innerPoolId), _ => Task.FromResult(HandleOutcomeResult.Success()), Ct);

        SpaceId? observedDuringInner = null;
        SpaceId? observedAfterInnerReturnsButBeforeOuterReturns = null;

        var outerOutcome = await _sut.ExecuteWithContextAsync(
            CreateEvent(outerPoolId),
            async ct =>
            {
                _ = await _sut.ExecuteWithContextAsync(
                    CreateEvent(innerPoolId),
                    innerCt =>
                    {
                        observedDuringInner = _spaceContext.GetSpaceId();
                        return Task.FromResult(HandleOutcomeResult.Success());
                    },
                    ct);

                // Still inside the outer handler - no boundary back to the test method has been
                // crossed - so this genuinely observes whether the inner call's scope restored
                // the enclosing (outer) space before returning.
                observedAfterInnerReturnsButBeforeOuterReturns = _spaceContext.GetSpaceId();
                return HandleOutcomeResult.Success();
            },
            Ct);

        outerOutcome.ShouldBeOfType<HandleOutcomeResult.SuccessResult>();
        observedDuringInner.ShouldBe(innerSpaceId);
        observedAfterInnerReturnsButBeforeOuterReturns.ShouldBe(outerSpaceId);
    }

    [Fact]
    public async Task unknown_pool_id_throws_and_never_invokes_the_handler()
    {
        var evt = CreateEvent(poolId: 999_999);
        var handlerInvoked = false;

        var act = async () => await _sut.ExecuteWithContextAsync(
            evt,
            ct =>
            {
                handlerInvoked = true;
                return Task.FromResult(HandleOutcomeResult.Success());
            },
            Ct);

        await act.ShouldThrowAsync<InvalidOperationException>();
        handlerInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task deleted_space_throws_and_never_invokes_the_handler()
    {
        var (poolId, spaceId) = await CreateSpaceAsync("Deleted Space");
        _ = await _admin.DeleteAsync(spaceId, Ct);

        var evt = CreateEvent(poolId);
        var handlerInvoked = false;

        var act = async () => await _sut.ExecuteWithContextAsync(
            evt,
            ct =>
            {
                handlerInvoked = true;
                return Task.FromResult(HandleOutcomeResult.Success());
            },
            Ct);

        await act.ShouldThrowAsync<InvalidOperationException>();
        handlerInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task caching_via_pool_id_does_not_serve_a_space_deleted_after_first_resolution()
    {
        var (poolId, spaceId) = await CreateSpaceAsync("Cache Invalidation Space");
        var evt = CreateEvent(poolId);

        // Warm the by-PoolId cache entry.
        var firstOutcome = await _sut.ExecuteWithContextAsync(
            evt,
            ct => Task.FromResult(HandleOutcomeResult.Success()),
            Ct);
        firstOutcome.ShouldBeOfType<HandleOutcomeResult.SuccessResult>();

        _ = await _admin.DeleteAsync(spaceId, Ct);

        var act = async () => await _sut.ExecuteWithContextAsync(
            evt,
            ct => Task.FromResult(HandleOutcomeResult.Success()),
            Ct);

        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task changing_a_spaces_pool_id_evicts_both_the_old_and_new_pool_id_cache_entries()
    {
        // Regression test for a reviewer-caught gap: SpaceRepository.UpdateAsync only busted
        // currentDso.PoolId (the space's *old* PoolId) after a write. If a space's PoolId ever
        // changes (SpaceAdmin rejects this at its own layer, but the repository/cache-invalidation
        // logic must not rely on that guard alone - this test bypasses SpaceAdmin deliberately, by
        // calling SpaceRepository.UpdateAsync directly), the *new* PoolId's cache entry was never
        // evicted. If that new PoolId had previously been negative-cached (e.g. probed by an
        // outbox event for a PoolId that did not yet belong to any space), the stale negative
        // entry would keep masking the space under its new PoolId indefinitely.
        var (oldPoolId, spaceId) = await CreateSpaceAsync("PoolId Change Space");
        var newPoolId = Interlocked.Increment(ref _poolIdCounter);

        var spaceStore = _services.GetRequiredService<ISpaceStore>();
        var repository = _services.GetRequiredService<SpaceRepository>();

        // Warm both cache entries before the change: a positive hit for the old PoolId, and a
        // negative (miss) entry for the new PoolId, which nothing occupies yet.
        var beforeOld = await spaceStore.TryGetSpaceByPoolId(oldPoolId, Ct);
        beforeOld.ShouldNotBeNull();
        beforeOld!.Id.ShouldBe(spaceId);

        var beforeNew = await spaceStore.TryGetSpaceByPoolId(newPoolId, Ct);
        beforeNew.ShouldBeNull();

        // Change the space's PoolId directly through the repository.
        var getResult = await repository.GetByIdAsync(spaceId, Ct);
        getResult.Found.ShouldBeTrue();
        var config = getResult.Item;
        var updated = new SpaceConfiguration
        {
            Id = config.Id,
            Name = config.Name,
            Enabled = config.Enabled,
            MatchPatterns = config.MatchPatterns,
            PoolId = newPoolId,
            IsDeleted = config.IsDeleted,
            ExtendedProperties = config.ExtendedProperties,
        };
        var updateResult = await repository.UpdateAsync(updated, new AttributeValueCollection(), getResult.Version!.Value, Ct);
        updateResult.IsSuccess.ShouldBeTrue();

        // The old PoolId must no longer resolve to this space (or anything else).
        var afterOld = await spaceStore.TryGetSpaceByPoolId(oldPoolId, Ct);
        afterOld.ShouldBeNull();

        // The new PoolId must resolve correctly, not still reflect the stale negative cache entry
        // warmed above.
        var afterNew = await spaceStore.TryGetSpaceByPoolId(newPoolId, Ct);
        afterNew.ShouldNotBeNull();
        afterNew!.Id.ShouldBe(spaceId);

        // And the ambient outbox context provider - the actual consumer this bug would have
        // broken - must resolve lookup/outbox context correctly under the new pool, and must no
        // longer recognize the old one.
        SpaceId? observedUnderNewPool = null;
        _ = await _sut.ExecuteWithContextAsync(
            CreateEvent(newPoolId),
            ct =>
            {
                observedUnderNewPool = _spaceContext.GetSpaceId();
                return Task.FromResult(HandleOutcomeResult.Success());
            },
            Ct);
        observedUnderNewPool.ShouldBe(spaceId);

        var actUnderOldPool = async () => await _sut.ExecuteWithContextAsync(
            CreateEvent(oldPoolId),
            ct => Task.FromResult(HandleOutcomeResult.Success()),
            Ct);
        await actUnderOldPool.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task real_outbox_processor_resolves_the_correct_space_for_each_of_two_real_persisted_events_without_cross_event_leakage()
    {
        // Strongest feasible integration coverage: everything below - OutboxProcessor, the
        // space-aware ambient context provider, real SQLite storage, and the events themselves -
        // comes from a single DI container wired through the normal/public product registration
        // surface (AddSpaces/AddStorageInternal), not hand-constructed. Both events are persisted
        // through the real DSO write path (IPartitionedStorageFactory -> IPartitionedStorage.CreateAsync), so
        // OutboxProcessor discovers them via a genuine SQLite read, and each event's space
        // resolution goes through the real HybridCache-backed SpaceStore/SpaceRepository, so the
        // first lookup for each is a genuine cache miss requiring a real async database round
        // trip, not a synchronously completed Task. Without the two-gap fixes (async ambient
        // establishment invoking the handler internally; Task<HandleOutcomeResult>
        // ExecuteWithContextAsync), this test would observe SpaceId.Default (or throw "no space
        // configured") instead of the space actually resolved for each event's PoolId.
        //
        // This does not assert ambient state from the test's own continuation after
        // RunProcessorAsync returns: once that call has genuinely suspended internally (which it
        // does here, via the real, uncached lookups), the test's continuation always resumes
        // using the context it captured before the call, regardless of what OutboxProcessor's
        // per-event `using` scopes actually did - see the nested unit test above for the full
        // explanation. Instead, this proves the processor resolves the *correct*, distinct space
        // for each of two real persisted events handled in the same batch, in order, which rules
        // out one event's context leaking into the next (a leak would either surface the wrong
        // SpaceId for the second event, or - if the first space were never restored - happen to
        // match by coincidence only when both events share a space, which they deliberately do
        // not here).
        //
        // This test builds its own ServiceCollection/provider (rather than reusing the class's
        // shared _services from InitializeAsync) because the subscription and keyed handler must
        // be registered before the container is built; there is no supported way to add
        // registrations to an already-built ServiceProvider.
        var subscriptionName = SubscriberName.Create("processor-integration-subscription");
        var handler = new RecordingHandler();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSpaces();
        TestSpacesLicense.RegisterEntitled(services);
        services.AddStorageInternal(b => b.AddSqliteInMemory());
        services.AddDsoRegistration<TestDso>();
        services.AddSingleton<IOutboxSubscription>(new TestSubscription(subscriptionName));
        services.AddKeyedTransient<IOutboxSubscriptionHandler>(subscriptionName.Value, (sp, _) =>
        {
            handler.Bind(sp.GetRequiredService<ISpaceContextAccessor>());
            return handler;
        });

        await using var provider = services.BuildServiceProvider();

        var storageInstanceSchema = provider.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(Ct);

        var admin = provider.GetRequiredService<ISpaceAdmin>();
        var spaceContext = provider.GetRequiredService<ISpaceContextAccessor>();

        var spaceAId = (await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Processor Integration Space A",
                MatchPatterns = [new SpaceMatchPattern { Origin = $"https://{Guid.NewGuid()}.example.com" }],
            },
            Ct)).Id!;
        var spaceBId = (await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Processor Integration Space B",
                MatchPatterns = [new SpaceMatchPattern { Origin = $"https://{Guid.NewGuid()}.example.com" }],
            },
            Ct)).Id!;

        // Persist two real outbox events through the real DSO write path, one per space, so the
        // subscriber_name stamped on each row comes from OutboxSubscriptions' genuine matching
        // logic rather than being fabricated by the test. Sequence numbers (and therefore
        // processing order) follow insertion order.
        async Task PersistEventUnderSpaceAsync(SpaceId spaceId, string payload)
        {
            var evt = new OutboxEvent
            {
                Id = OutboxEventId.New(),
                Timestamp = DateTimeOffset.UtcNow,
                EventName = OutboxEventName.Create("SpaceAwareContextTestEvent"),
                SubjectId = UuidV7.New(),
                EntityTypeName = nameof(TestDso),
                EntityTypeId = (int)TestDso.DsoVersion.EntityType.Id,
                Payload = "{}",
                DsoTypeSchemaVersion = 1,
            };

            using (spaceContext.SetSpace(spaceId))
            {
                var writeStorage = await provider.GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Spaces, Ct);
                var createStorageResult = await writeStorage.CreateAsync(
                    evt.SubjectId,
                    new TestDso(payload),
                    [],
                    SearchFieldCollection.Empty,
                    Expiration.NoExpiration,
                    [evt],
                    Ct);
                createStorageResult.ShouldBe(CreateResult.Success);
            }
        }

        await PersistEventUnderSpaceAsync(spaceAId, "outbox-processor-integration-a");
        await PersistEventUnderSpaceAsync(spaceBId, "outbox-processor-integration-b");

        var processor = provider.GetRequiredService<OutboxProcessor>();

        await processor.RunProcessorAsync(Ct);

        handler.ObservedSpaceIds.ShouldBe([spaceAId, spaceBId]);

        var readStorage = await provider.GetRequiredService<ICrossPartitionStorageFactory>().GetCrossPartitionStorageAsync(StorageInstanceId.Default, Ct);
        var remainingPage = await readStorage.GetOutboxEventsForSubscriptionAsync(subscriptionName, 100, Ct);
        remainingPage.Events.ShouldBeEmpty("both events must have been processed successfully and deleted");
    }

    private async Task<(int PoolId, SpaceId SpaceId)> CreateSpaceAsync(string name)
    {
        var poolId = Interlocked.Increment(ref _poolIdCounter);
        var createResult = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = name,
                MatchPatterns = [new SpaceMatchPattern { Origin = $"https://{Guid.NewGuid()}.example.com" }],
                PoolId = poolId
            },
            Ct);
        return (poolId, createResult.Id!);
    }

    private static PersistedOutboxEvent CreateEvent(int poolId, SubscriberName? subscriptionName = null) => new()
    {
        MessageId = OutboxEventId.New(),
        EventId = OutboxEventId.New(),
        Timestamp = DateTimeOffset.UtcNow,
        SequenceNumber = 1,
        EventName = OutboxEventName.Create("SpaceAwareContextTestEvent"),
        SubjectId = UuidV7.New(),
        EntityTypeName = "TestEntity",
        EntityTypeId = 1,
        PoolId = poolId,
        Payload = "{}",
        SubscriberName = subscriptionName ?? SubscriberName.Create("space-aware-context-tests"),
    };

    private sealed class TestSubscription(SubscriberName subscriptionName) : IOutboxSubscription
    {
        public SubscriberName SubscriberName { get; } = subscriptionName;
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } = new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int>();
    }

    /// <summary>
    /// Records the ambient <see cref="SpaceId"/> observed at handling time. Constructed once by
    /// the test and rebound to its accessor via <see cref="Bind"/> at DI-resolution time, since
    /// the keyed handler factory only has access to the scope's <see cref="IServiceProvider"/>
    /// once resolution actually happens.
    /// </summary>
    private sealed class RecordingHandler : IOutboxSubscriptionHandler
    {
        private readonly List<SpaceId> _observedSpaceIds = [];
        private ISpaceContextAccessor? _spaceContext;

        public IReadOnlyList<SpaceId> ObservedSpaceIds => _observedSpaceIds;

        public void Bind(ISpaceContextAccessor spaceContext) => _spaceContext = spaceContext;

        public Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
        {
            _observedSpaceIds.Add(_spaceContext!.GetSpaceId());
            return Task.FromResult(HandleOutcomeResult.Success());
        }
    }

}
