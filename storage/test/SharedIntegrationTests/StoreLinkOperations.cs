// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Pagination;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Integration tests for Link/Unlink operations across all store types.
/// Covers basic link/unlink, batch operations, and cascade delete behavior.
/// </summary>
public partial class StoreLinkOperations
{

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly EntityType LeftEntityType = TestDso.DsoVersion.EntityType;
    private static readonly EntityType RightEntityType = TestDso2.DsoVersion.EntityType;
    private static readonly LinkDefinition TestLink = TestLinkData.TestLink;
    private static readonly LinkDefinition TestLink2 = TestLinkData.TestLink2;

    // =========================================================================
    // Link / Unlink basic operations
    // =========================================================================

    [Fact]
    public async Task CanLinkAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        // Create entities on both sides
        _ = await store.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);

        var result = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        result.ShouldBe(LinkResult.Success);

        // Verify via QueryLinks: find TestDso2 entities linked from leftId
        var query = LinkQuery.From(RightEntityType)
            .Join(TestLink)
            .Where(LeftEntityType, leftId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso2>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.Count.ShouldBe(1);
        page.Items[0].Value.Value.ShouldBe("right");
    }

    [Fact]
    public async Task LinkDuplicateReturnsAlreadyLinkedAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);
        var second = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        second.ShouldBe(LinkResult.AlreadyLinked);
    }

    [Fact]
    public async Task UnlinkRemovesLinkAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        _ = await store.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        var unlinkResult = await store.UnlinkAsync(TestLink, leftId, rightId, [], _ct);
        unlinkResult.ShouldBe(UnlinkResult.Success);

        // Verify the link is gone
        var query = LinkQuery.From(RightEntityType)
            .Join(TestLink)
            .Where(LeftEntityType, leftId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso2>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnlinkNonExistentReturnsSuccessAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        // No link was ever created
        var result = await store.UnlinkAsync(TestLink, leftId, rightId, [], _ct);

        result.ShouldBe(UnlinkResult.Success);
    }

    [Fact]
    public async Task LinkWithoutEntityExistingSucceedsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        // No entities created — no referential integrity check
        var result = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        result.ShouldBe(LinkResult.Success);
    }

    // =========================================================================
    // Batch operations with links
    // =========================================================================

    [Fact]
    public async Task CanLinkInBatchAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        _ = await store.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);

        var batchResult = await store.ExecuteBatchAsync(
            [LinkOperation.For(TestLink, leftId, rightId)],
            [],
            _ct);

        batchResult.Success.ShouldBeTrue();
        batchResult.Results.Count.ShouldBe(1);
        batchResult.Results[0].Outcome.ShouldBe(OperationOutcome.Success);

        // Verify link exists
        var query = LinkQuery.From(RightEntityType)
            .Join(TestLink)
            .Where(LeftEntityType, leftId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso2>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CanMixCreateAndLinkInBatchAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        var batchResult = await store.ExecuteBatchAsync(
            [
                CreateOperation.For(leftId, new TestDso("created-left"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
                CreateOperation.For(rightId, new TestDso2("created-right"), [], SearchFieldCollection.Empty, Expiration.NoExpiration),
                LinkOperation.For(TestLink, leftId, rightId)
            ],
            [],
            _ct);

        batchResult.Success.ShouldBeTrue();
        batchResult.Results.Count.ShouldBe(3);
        batchResult.Results.ShouldAllBe(r => r.Outcome == OperationOutcome.Success);

        // Verify entity exists
        (await store.TryReadAsync(LeftEntityType, leftId, _ct)).Found.ShouldBeTrue();

        // Verify link exists
        var query = LinkQuery.From(RightEntityType)
            .Join(TestLink)
            .Where(LeftEntityType, leftId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso2>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task BatchLinkDuplicateIsIdempotentAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        // First link
        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        // Batch with duplicate link — should succeed (idempotent)
        var batchResult = await store.ExecuteBatchAsync(
            [LinkOperation.For(TestLink, leftId, rightId)],
            [],
            _ct);

        batchResult.Success.ShouldBeTrue();
        batchResult.Results[0].Outcome.ShouldBe(OperationOutcome.AlreadyLinked);
    }

    [Fact]
    public async Task CanUnlinkInBatchAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        _ = await store.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        var batchResult = await store.ExecuteBatchAsync(
            [UnlinkOperation.For(TestLink, leftId, rightId)],
            [],
            _ct);

        batchResult.Success.ShouldBeTrue();
        batchResult.Results[0].Outcome.ShouldBe(OperationOutcome.Success);

        // Verify link gone
        var query = LinkQuery.From(RightEntityType)
            .Join(TestLink)
            .Where(LeftEntityType, leftId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso2>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.ShouldBeEmpty();
    }

    // =========================================================================
    // Cascade delete behavior
    // =========================================================================

    [Fact]
    public async Task DeleteEntityRemovesLinksWhereEntityIsLeftAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        _ = await store.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        // Delete the left entity
        _ = await store.DeleteAsync(LeftEntityType, leftId, [], _ct);

        // Query from right side — link should be gone
        var query = LinkQuery.From(LeftEntityType)
            .Join(TestLink)
            .Where(RightEntityType, rightId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteEntityRemovesLinksWhereEntityIsRightAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        _ = await store.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        // Delete the right entity
        _ = await store.DeleteAsync(RightEntityType, rightId, [], _ct);

        // Query from left side — link should be gone
        var query = LinkQuery.From(RightEntityType)
            .Join(TestLink)
            .Where(LeftEntityType, leftId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso2>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteEntityRemovesMultipleLinksAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId1 = UuidV7.New();
        var rightId2 = UuidV7.New();

        _ = await store.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId1, new TestDso2("right1"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId2, new TestDso2("right2"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(TestLink, leftId, rightId1, [], _ct);
        _ = await store.LinkAsync(TestLink, leftId, rightId2, [], _ct);

        // Delete the left entity — both links should go
        _ = await store.DeleteAsync(LeftEntityType, leftId, [], _ct);

        var query = LinkQuery.From(LeftEntityType)
            .Join(TestLink)
            .Where(RightEntityType, rightId1)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchDeleteEntityRemovesLinksAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        _ = await store.CreateAsync(leftId, new TestDso("left"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        // Delete via batch
        var batchResult = await store.ExecuteBatchAsync(
            [DeleteOperation.ById(LeftEntityType, leftId)],
            [],
            _ct);

        batchResult.Success.ShouldBeTrue();

        // Link should be gone
        var query = LinkQuery.From(LeftEntityType)
            .Join(TestLink)
            .Where(RightEntityType, rightId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteEntityByKeyRemovesLinksAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        var leftKey = new TestJsonKeyDsk($"left-key-{Guid.NewGuid()}");

        _ = await store.CreateAsync(leftId, new TestDso("left"), [DataStorageKey.Create(leftKey)], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        // Delete the left entity by key (not by ID)
        var result = await store.DeleteAsync(LeftEntityType, DataStorageKey.Create(leftKey), [], _ct);
        result.ShouldBe(DeleteResult.Success);

        // Link should be gone
        var query = LinkQuery.From(LeftEntityType)
            .Join(TestLink)
            .Where(RightEntityType, rightId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchDeleteEntityByKeyRemovesLinksAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var queryStore = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();
        var leftKey = new TestJsonKeyDsk($"left-key-{Guid.NewGuid()}");

        _ = await store.CreateAsync(leftId, new TestDso("left"), [DataStorageKey.Create(leftKey)], [], Expiration.NoExpiration, [], _ct);
        _ = await store.CreateAsync(rightId, new TestDso2("right"), [], [], Expiration.NoExpiration, [], _ct);
        _ = await store.LinkAsync(TestLink, leftId, rightId, [], _ct);

        // Delete via batch by key
        var batchResult = await store.ExecuteBatchAsync(
            [DeleteOperation.ByKey(LeftEntityType, DataStorageKey.Create(leftKey))],
            [],
            _ct);
        batchResult.Success.ShouldBeTrue();

        // Link should be gone
        var query = LinkQuery.From(LeftEntityType)
            .Join(TestLink)
            .Where(RightEntityType, rightId)
            .Build();
        var page = await queryStore.QueryLinksAsync<TestDso>(query, DataRange.FromPage(1, 100), _ct);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConcurrentLinkCallsNeverThrowAsync()
    {
        // When multiple concurrent Link calls target the same entity pair,
        // exactly one should succeed and the rest should return AlreadyLinked.
        // Before the race-condition fix, the MsSql INSERT WHERE NOT EXISTS
        // could allow two transactions to both observe "not exists" and then
        // one would hit an unhandled PK violation exception.
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var leftId = UuidV7.New();
        var rightId = UuidV7.New();

        const int Concurrency = 10;
        var tasks = Enumerable.Range(0, Concurrency)
            .Select(_ => store.LinkAsync(TestLink, leftId, rightId, [], _ct))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Count(r => r == LinkResult.Success).ShouldBe(1);
        results.Count(r => r == LinkResult.AlreadyLinked).ShouldBe(Concurrency - 1);
    }

    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<TestDso2>();
        });
}
