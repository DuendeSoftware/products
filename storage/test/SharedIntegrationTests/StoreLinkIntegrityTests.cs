// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Pagination;
using Microsoft.Extensions.DependencyInjection;
using OutboxEventId = Duende.Storage.Internal.Outbox.OutboxEventId;
using OutboxEventName = Duende.Storage.Internal.Outbox.OutboxEventName;
using SubscriberName = Duende.Storage.Internal.Outbox.SubscriberName;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Integration tests for the database-enforced link integrity introduced in the single
/// <c>entity_links</c> table (with a per-row <c>restrict_delete</c> flag): missing-endpoint
/// rejection, cascade-delete semantics, and the <see cref="LinkDeleteBehavior.Restrict"/> behaviour.
/// These tests are expected to fail on engines that have not yet added the V003 foreign keys
/// (Tasks 5-8): links to non-existent entities currently succeed instead of returning
/// <see cref="LinkResult.DoesNotExist"/>, and Restrict deletes are not yet refused.
/// </summary>
public partial class StoreLinkIntegrityTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly EntityType LeftEntityType = TestDso.DsoVersion.EntityType;
    private static readonly EntityType RightEntityType = TestDso2.DsoVersion.EntityType;
    private static readonly LinkDefinition CascadeLink = TestLinkData.TestLink;
    private static readonly LinkDefinition RestrictLink = TestLinkData.TestLinkRestrict;

    private static readonly SubscriberName WildcardSubscriberName =
        SubscriberName.Create("link-integrity-subscription");

    // =========================================================================
    // Integrity: missing endpoints
    // =========================================================================

    [Fact]
    public async Task LinkAsync_with_missing_left_returns_DoesNotExistAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var rightId = UuidV7.New();
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);

        var result = await storage.LinkAsync(CascadeLink, UuidV7.New(), rightId, [], _ct);

        result.ShouldBe(LinkResult.DoesNotExist);
    }

    [Fact]
    public async Task LinkAsync_with_missing_right_returns_DoesNotExistAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);

        var result = await storage.LinkAsync(CascadeLink, leftId, UuidV7.New(), [], _ct);

        result.ShouldBe(LinkResult.DoesNotExist);
    }

    [Fact]
    public async Task LinkAsync_with_right_id_existing_as_another_entity_type_returns_DoesNotExistAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var wrongTypeId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        // wrongTypeId exists, but as the LEFT entity type (TestDso), not the RIGHT entity type (TestDso2).
        _ = await storage.CreateAsync(wrongTypeId, new TestDso("not-a-right"), [], [], Expiration.NoExpiration, [], _ct);

        var result = await storage.LinkAsync(CascadeLink, leftId, wrongTypeId, [], _ct);

        result.ShouldBe(LinkResult.DoesNotExist);
    }

    [Fact]
    public async Task LinkAsync_with_ids_existing_only_in_another_pool_returns_DoesNotExistAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        storage.SetPoolId(2);
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);

        storage.SetPoolId(PoolId.Default);
        var result = await storage.LinkAsync(CascadeLink, leftId, rightId, [], _ct);

        result.ShouldBe(LinkResult.DoesNotExist);
    }

    [Fact]
    public async Task Batch_CreateBothThenLink_succeedsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        var batch = await storage.ExecuteBatchAsync(
            [
                CreateOperation.For(leftId, new TestDso("left"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
                CreateOperation.For(rightId, new TestDso2("right"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
                LinkOperation.For(CascadeLink, leftId, rightId)
            ],
            [],
            _ct);

        batch.Success.ShouldBeTrue();
        batch.Results.ShouldAllBe(r => r.Outcome == OperationOutcome.Success);
    }

    [Fact]
    public async Task Batch_LinkBeforeCreate_fails_at_index_0_and_persists_nothingAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        var batch = await storage.ExecuteBatchAsync(
            [
                LinkOperation.For(CascadeLink, leftId, rightId),
                CreateOperation.For(leftId, new TestDso("left"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
                CreateOperation.For(rightId, new TestDso2("right"), [], SearchFieldCollection.Empty, Expiration.NoExpiration)
            ],
            [],
            _ct);

        batch.Success.ShouldBeFalse();
        batch.Results.Count.ShouldBe(1);
        batch.Results[0].Outcome.ShouldBe(OperationOutcome.DoesNotExist);

        (await storage.TryReadAsync(LeftEntityType, leftId, _ct)).Found.ShouldBeFalse();
        (await storage.TryReadAsync(RightEntityType, rightId, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Failed_LinkAsync_writes_no_outbox_eventAsync()
    {
        await using var fixture = await CreateProviderWithSubscriptionAsync();
        var storage = fixture.PartitionedStorage;
        var evt = MakeEvent();

        var result = await storage.LinkAsync(CascadeLink, UuidV7.New(), UuidV7.New(), [evt], _ct);

        result.ShouldBe(LinkResult.DoesNotExist);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 100, _ct);
        page.Events.ShouldBeEmpty();
    }

    // =========================================================================
    // Cascade
    // =========================================================================

    [Fact]
    public async Task Deleting_entity_leaves_links_of_another_type_reusing_its_uuid_untouchedAsync()
    {
        // Previously, deleting entity X of type T removed links
        // by pool+id regardless of entity type, so it could remove links belonging to an unrelated
        // entity of a DIFFERENT type that reuses the same UUID value. With per-type foreign keys,
        // the cascade only ever applies to the deleted (pool, type, id) row.
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;

        var sharedId = UuidV7.New();
        var otherLeftId = UuidV7.New();

        _ = await storage.CreateAsync(sharedId, new TestDso("to-delete"), [], [], Expiration.NoExpiration, [], _ct);
        // Same UUID value, but stored as a TestDso2 entity (a different entity type) — the right side of
        // another, unrelated link.
        _ = await storage.CreateAsync(sharedId, new TestDso2("reuses-uuid"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(otherLeftId, new TestDso("other-left"), [], [], Expiration.NoExpiration, [], _ct);
        (await storage.LinkAsync(CascadeLink, otherLeftId, sharedId, [], _ct)).ShouldBe(LinkResult.Success);

        _ = await storage.DeleteAsync(LeftEntityType, sharedId, [], _ct);

        var query = LinkQuery.From(LeftEntityType).Join(CascadeLink).Where(RightEntityType, sharedId).Build();
        var page = await storage.QueryLinksAsync(query, DataRange.FromPage(1, 100), _ct);
        page.Items.Count.ShouldBe(1);
    }

    // =========================================================================
    // Restrict
    // =========================================================================

    [Fact]
    public async Task Restrict_DeleteRight_while_linked_returns_StillLinked_and_changes_nothingAsync()
    {
        await using var fixture = await CreateProviderWithSubscriptionAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        (await storage.LinkAsync(RestrictLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        var evt = MakeEvent();
        var result = await storage.DeleteAsync(RightEntityType, rightId, [evt], _ct);

        result.ShouldBe(DeleteResult.StillLinked);
        (await storage.TryReadAsync(RightEntityType, rightId, _ct)).Found.ShouldBeTrue();

        var query = LinkQuery.From(RightEntityType).Join(RestrictLink).Where(LeftEntityType, leftId).Build();
        (await storage.QueryLinksAsync(query, DataRange.FromPage(1, 100), _ct)).Items.Count.ShouldBe(1);

        var page = await fixture.DefaultCrossPartitionStorage.GetOutboxEventsForSubscriptionAsync(WildcardSubscriberName, 100, _ct);
        page.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Restrict_BatchDeleteRight_while_linked_returns_StillLinked_and_rolls_backAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        (await storage.LinkAsync(RestrictLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        var batch = await storage.ExecuteBatchAsync([DeleteOperation.ById(RightEntityType, rightId)], [], _ct);

        batch.Success.ShouldBeFalse();
        batch.Results[0].Outcome.ShouldBe(OperationOutcome.StillLinked);
        (await storage.TryReadAsync(RightEntityType, rightId, _ct)).Found.ShouldBeTrue();
    }

    [Fact]
    public async Task Restrict_DeleteLeft_removes_the_linkAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        (await storage.LinkAsync(RestrictLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        (await storage.DeleteAsync(LeftEntityType, leftId, [], _ct)).ShouldBe(DeleteResult.Success);

        var query = LinkQuery.From(LeftEntityType).Join(RestrictLink).Where(RightEntityType, rightId).Build();
        (await storage.QueryLinksAsync(query, DataRange.FromPage(1, 100), _ct)).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Restrict_Batch_UnlinkThenDeleteRight_succeedsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        (await storage.LinkAsync(RestrictLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        var batch = await storage.ExecuteBatchAsync(
            [
                UnlinkOperation.For(RestrictLink, leftId, rightId),
                DeleteOperation.ById(RightEntityType, rightId)
            ],
            [],
            _ct);

        batch.Success.ShouldBeTrue();
        (await storage.TryReadAsync(RightEntityType, rightId, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Restrict_Batch_DeleteLeftThenDeleteRight_succeedsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        (await storage.LinkAsync(RestrictLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        var batch = await storage.ExecuteBatchAsync(
            [
                DeleteOperation.ById(LeftEntityType, leftId),
                DeleteOperation.ById(RightEntityType, rightId)
            ],
            [],
            _ct);

        batch.Success.ShouldBeTrue();
        (await storage.TryReadAsync(LeftEntityType, leftId, _ct)).Found.ShouldBeFalse();
        (await storage.TryReadAsync(RightEntityType, rightId, _ct)).Found.ShouldBeFalse();
    }

    [Fact]
    public async Task Restrict_QueryLinksAsync_Unlink_AlreadyLinked_behave_like_cascadeAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);

        (await storage.LinkAsync(RestrictLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);
        (await storage.LinkAsync(RestrictLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.AlreadyLinked);

        var query = LinkQuery.From(RightEntityType).Join(RestrictLink).Where(LeftEntityType, leftId).Build();
        (await storage.QueryLinksAsync(query, DataRange.FromPage(1, 100), _ct)).Items.Count.ShouldBe(1);

        (await storage.UnlinkAsync(RestrictLink, leftId, rightId, [], _ct)).ShouldBe(UnlinkResult.Success);
        (await storage.QueryLinksAsync(query, DataRange.FromPage(1, 100), _ct)).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cascade_and_restrict_definitions_between_same_entities_dont_see_each_others_rowsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        _ = await storage.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await storage.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);

        (await storage.LinkAsync(CascadeLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        // The same pair, linked under the Restrict definition: a different link type id, so it is a
        // separate row and not an "already linked" duplicate of the cascade link.
        (await storage.LinkAsync(RestrictLink, leftId, rightId, [], _ct)).ShouldBe(LinkResult.Success);

        var cascadeQuery = LinkQuery.From(RightEntityType).Join(CascadeLink).Where(LeftEntityType, leftId).Build();
        var restrictQuery = LinkQuery.From(RightEntityType).Join(RestrictLink).Where(LeftEntityType, leftId).Build();
        (await storage.QueryLinksAsync(cascadeQuery, DataRange.FromPage(1, 100), _ct)).Items.Count.ShouldBe(1);
        (await storage.QueryLinksAsync(restrictQuery, DataRange.FromPage(1, 100), _ct)).Items.Count.ShouldBe(1);

        // Unlinking the cascade definition must not remove the restrict row for the same pair.
        (await storage.UnlinkAsync(CascadeLink, leftId, rightId, [], _ct)).ShouldBe(UnlinkResult.Success);
        (await storage.QueryLinksAsync(cascadeQuery, DataRange.FromPage(1, 100), _ct)).Items.ShouldBeEmpty();
        (await storage.QueryLinksAsync(restrictQuery, DataRange.FromPage(1, 100), _ct)).Items.Count.ShouldBe(1);

        // Deleting the right entity is still refused: the restrict link is still present.
        (await storage.DeleteAsync(RightEntityType, rightId, [], _ct)).ShouldBe(DeleteResult.StillLinked);
    }

    private static OutboxEvent MakeEvent() => new()
    {
        Id = OutboxEventId.New(),
        Timestamp = DateTimeOffset.UtcNow,
        EventName = OutboxEventName.Create("TestEvent"),
        SubjectId = UuidV7.New(),
        EntityTypeName = nameof(TestDso),
        EntityTypeId = (int)TestDso.DsoVersion.EntityType.Id,
        Payload = "{}",
    };

    private async Task<IStorageFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

    private async Task<IStorageFixture> CreateProviderWithSubscriptionAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            _ = services.AddSingleton<IOutboxSubscription>(new WildcardTestSubscription());
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });

    /// <summary>
    /// Wildcard subscription that matches all entity types and event names,
    /// used to ensure outbox events are written to the storage in tests.
    /// </summary>
    private sealed class WildcardTestSubscription : IOutboxSubscription
    {
        public SubscriberName SubscriberName => WildcardSubscriberName;
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } = new HashSet<OutboxEventName>();
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int>();
    }
}
