// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Pagination;
using SortDirection = Duende.Storage.Internal.Querying.SortDirection;
using SortParameter = Duende.Storage.Internal.Querying.Sorting.SortParameter;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Shared conformance tests for deterministic bidirectional cursor traversal (#3460).
/// Encodes the full requested order (nulls last, sort field direction, entity_id tie-break), the
/// unsorted default order, flags that are independent of traversal direction, tokens that are
/// always populated (including empty pages), and rejection of invalid or mismatched tokens.
/// </summary>
public partial class QueryStoreCursorTraversalTests
{
    private readonly EntityType _testEntityType = TestCursorDso.DsoVersion.EntityType;
    private readonly EntityType _testSortEntityType = TestSortDso.DsoVersion.EntityType;

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private async Task<IStorageFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestCursorDso>();
            services.AddDsoRegistration<TestSortDso>();
        });

    /// <summary>
    /// Creates an entity. When <paramref name="rank"/> is null, the "rank" search field is
    /// omitted entirely (never an empty string: Oracle stores '' as NULL), so the sort field
    /// is NULL through the LEFT JOIN.
    /// </summary>
    private static async Task<Guid> CreateEntityAsync(
        IPartitionedStorage partitionedStorage,
        string name,
        int? rank,
        DateTimeOffset? createdAt = null,
        bool? isActive = null,
        Ct ct = default)
    {
        var id = UuidV7.New();
        var dso = new TestCursorDso
        {
            Name = name,
            Rank = rank ?? 0,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            IsActive = isActive ?? true
        };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);
        if (rank.HasValue)
        {
            _ = searchFieldsBuilder.Add("rank", rank.Value);
        }

        _ = searchFieldsBuilder.Add("recordedAt", dso.CreatedAt);
        _ = searchFieldsBuilder.Add("isActive", dso.IsActive);

        var searchFields = searchFieldsBuilder.Build();

        var result = await partitionedStorage.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id.Value;
    }

    /// <summary>
    /// Updates an existing <see cref="TestCursorDso"/>'s "rank" search field, keeping its other fields.
    /// </summary>
    private async Task UpdateRankAsync(IPartitionedStorage partitionedStorage, Guid id, int newRank, Ct ct)
    {
        var existing = await partitionedStorage.TryReadAsync(_testEntityType, UuidV7.From(id), ct);
        existing.Found.ShouldBeTrue();
        var dso = ((TestCursorDso)existing.Dso) with { Rank = newRank };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", dso.Name);
        _ = searchFieldsBuilder.Add("rank", newRank);
        _ = searchFieldsBuilder.Add("recordedAt", dso.CreatedAt);
        _ = searchFieldsBuilder.Add("isActive", dso.IsActive);

        var result = await partitionedStorage.UpdateAsync(UuidV7.From(id), dso, existing.Version!.Value, [], searchFieldsBuilder.Build(), Expiration.NoExpiration, [], ct);
        result.ShouldBe(UpdateResult.Success);
    }

    /// <summary>
    /// Creates a <see cref="TestSortDso"/> entity with a decimal "rating" search field, for tests that
    /// need exact fractional sort values (Design decision: F1 adversarial-review fix).
    /// </summary>
    private static async Task<Guid> CreateRatingEntityAsync(IPartitionedStorage partitionedStorage, string name, decimal rating, Ct ct)
    {
        var id = UuidV7.New();
        var dso = new TestSortDso { Name = name, Rating = rating };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);
        _ = searchFieldsBuilder.Add("rating", rating);

        var result = await partitionedStorage.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFieldsBuilder.Build(), Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id.Value;
    }

    private async Task<List<Guid>> ReadAllForwardAsync(
        IPartitionedStorage partitionedStorage,
        IQueryExpression filter,
        SortParameter sort,
        int pageSize)
    {
        var ids = new List<Guid>();
        ContinuationToken token = ContinuationToken.Beginning;
        while (true)
        {
            var page = await partitionedStorage.QueryAsync(
                _testEntityType, filter, sort, DataRange.FromContinuationToken(token, pageSize), _ct);
            ids.AddRange(page.Items.Select(i => i.Id));
            if (!page.HasMoreData)
            {
                break;
            }

            token = page.NextToken!.Value;
        }

        return ids;
    }

    [Fact]
    public async Task Unsorted_forward_traversal_returns_every_id_once_in_entity_id_order()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var ids = new List<Guid>();
        for (var i = 1; i <= 10; i++)
        {
            ids.Add(await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct));
        }

        var filter = Query.All();

        // Act - page unsorted with SortParameter.Empty
        var collected = await ReadAllForwardAsync(partitionedStorage, filter, SortParameter.Empty, 3);

        // Assert - every id exactly once, ordered the same way unsorted offset paging orders them,
        // i.e. the provider's own ORDER BY entity_id (not .NET Guid.CompareTo order: SQL Server
        // orders uniqueidentifier by byte groups, SQLite orders by TEXT representation).
        var offsetResult = await partitionedStorage.QueryAsync(
            _testEntityType, filter, SortParameter.Empty, DataRange.FromOffset(0, 10), _ct);
        var expectedOrder = offsetResult.Items.Select(i => i.Id).ToList();

        collected.Count.ShouldBe(10);
        collected.ToHashSet().SetEquals(ids).ShouldBeTrue();
        expectedOrder.ToHashSet().SetEquals(ids).ShouldBeTrue();
        collected.ShouldBe(expectedOrder);
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task Duplicate_sort_values_forward_traversal_has_no_gaps_or_duplicates(SortDirection direction)
    {
        // Arrange - 10 rows sharing 2-3 rank values
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var ranks = new[] { 10, 10, 10, 10, 20, 20, 20, 30, 30, 30 };
        var ids = new List<Guid>();
        for (var i = 0; i < ranks.Length; i++)
        {
            ids.Add(await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", ranks[i], ct: _ct));
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), direction);

        // Act
        var collected = await ReadAllForwardAsync(partitionedStorage, filter, sort, 3);

        // Assert
        collected.Count.ShouldBe(10);
        collected.ToHashSet().SetEquals(ids).ShouldBeTrue();
        collected.Distinct().Count().ShouldBe(10);
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task Null_sort_values_come_last_in_requested_order_with_no_gaps_or_duplicates(SortDirection direction)
    {
        // Arrange - a mix of null and non-null ranks
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var nonNullIds = new List<Guid>();
        var nullIds = new List<Guid>();
        for (var i = 1; i <= 6; i++)
        {
            nonNullIds.Add(await CreateEntityAsync(partitionedStorage, $"NonNull{i:D2}", i * 10, ct: _ct));
        }

        for (var i = 1; i <= 4; i++)
        {
            nullIds.Add(await CreateEntityAsync(partitionedStorage, $"Null{i:D2}", null, ct: _ct));
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), direction);

        // Act
        var collected = await ReadAllForwardAsync(partitionedStorage, filter, sort, 3);

        // Assert - no gaps, no duplicates, nulls last regardless of direction
        collected.Count.ShouldBe(10);
        collected.ToHashSet().SetEquals(nonNullIds.Concat(nullIds)).ShouldBeTrue();

        var firstNullIndex = collected.FindIndex(nullIds.Contains);
        for (var i = firstNullIndex; i < collected.Count; i++)
        {
            nullIds.ShouldContain(collected[i]);
        }
    }

    [Fact]
    public async Task Backward_from_page_k_returns_exactly_page_k_minus_1_in_requested_order()
    {
        // Arrange - 9 items, page size 3 -> 3 pages
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        var page1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);
        var page3 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page2.NextToken!.Value, 3), _ct);

        // Act - follow page3.PreviousToken with the SAME sort
        var back = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page3.PreviousToken!.Value, 3), _ct);

        // Assert - items in requested (forward) order, same as page2
        back.Items.Select(i => ((TestCursorDso)i.Value).Rank).ShouldBe(page2.Items.Select(i => ((TestCursorDso)i.Value).Rank));
    }

    [Fact]
    public async Task Backward_traversal_covers_duplicates_and_the_null_boundary_page_size_1()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var id1 = await CreateEntityAsync(partitionedStorage, "A", 10, ct: _ct);
        var id2 = await CreateEntityAsync(partitionedStorage, "B", 10, ct: _ct);
        var id3 = await CreateEntityAsync(partitionedStorage, "C", 20, ct: _ct);
        var id4 = await CreateEntityAsync(partitionedStorage, "D", null, ct: _ct);
        var id5 = await CreateEntityAsync(partitionedStorage, "E", null, ct: _ct);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act - page forward with size 1 through every row, remembering the last page so we can
        // start the backward walk from its own PreviousToken (the position of its one item).
        var forwardIds = new List<Guid>();
        var token = (ContinuationToken)ContinuationToken.Beginning;
        QueryResult<MetadataEnvelope> lastPage = null!;
        for (var i = 0; i < 5; i++)
        {
            var page = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(token, 1), _ct);
            forwardIds.Add(page.Items[0].Id);
            lastPage = page;
            token = page.NextToken!.Value;
        }

        // Assert — the last page's own item, then walking backward from its PreviousToken with the
        // same sort reproduces every earlier page in reverse.
        var backIds = new List<Guid> { lastPage.Items[0].Id };
        var backToken = lastPage.PreviousToken!.Value;
        for (var i = 0; i < 4; i++)
        {
            var page = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(backToken, 1), _ct);
            backIds.Add(page.Items[0].Id);
            backToken = page.PreviousToken!.Value;
        }

        backIds.ShouldBe(Enumerable.Reverse(forwardIds).ToList());
        forwardIds.ToHashSet().SetEquals([id1, id2, id3, id4, id5]).ShouldBeTrue();
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task Round_trip_forward_then_backward_reproduces_pages_in_reverse(SortDirection direction)
    {
        // Arrange - duplicates and nulls
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 4; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Dup{i}", 10, ct: _ct);
        }

        for (var i = 1; i <= 4; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Val{i}", i * 20, ct: _ct);
        }

        for (var i = 1; i <= 3; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Null{i}", null, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), direction);

        // Act - page forward to the end, remembering the last page's PreviousToken
        var forwardPages = new List<IReadOnlyList<Guid>>();
        ContinuationToken token = ContinuationToken.Beginning;
        ContinuationToken lastPreviousToken = ContinuationToken.Beginning;
        while (true)
        {
            var page = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(token, 3), _ct);
            forwardPages.Add(page.Items.Select(i => i.Id).ToArray());
            lastPreviousToken = page.PreviousToken!.Value;
            if (!page.HasMoreData)
            {
                break;
            }

            token = page.NextToken!.Value;
        }

        // Act - follow PreviousToken back from the last page with the same sort. The last page's own
        // items are already known; walking Previous reconstructs every earlier page in reverse.
        var backwardPages = new List<IReadOnlyList<Guid>> { forwardPages[^1] };
        var backToken = lastPreviousToken;
        for (var i = forwardPages.Count - 2; i >= 0; i--)
        {
            var page = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(backToken, 3), _ct);
            backwardPages.Add(page.Items.Select(i2 => i2.Id).ToArray());
            backToken = page.PreviousToken!.Value;
        }

        // Assert
        for (var i = 0; i < forwardPages.Count; i++)
        {
            backwardPages[i].ShouldBe(forwardPages[forwardPages.Count - 1 - i]);
        }
    }

    [Fact]
    public async Task Round_trip_unsorted_forward_then_backward()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 7; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = SortParameter.Empty;

        // Act - page forward to the last page, remembering the last page's PreviousToken
        var forwardPages = new List<IReadOnlyList<Guid>>();
        ContinuationToken token = ContinuationToken.Beginning;
        ContinuationToken lastPreviousToken = ContinuationToken.Beginning;
        while (true)
        {
            var page = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(token, 3), _ct);
            forwardPages.Add(page.Items.Select(i => i.Id).ToArray());
            lastPreviousToken = page.PreviousToken!.Value;
            if (!page.HasMoreData)
            {
                break;
            }

            token = page.NextToken!.Value;
        }

        // Act - follow PreviousToken back from the last page with the same (empty) sort. The last
        // page's own items are already known; walking Previous reconstructs every earlier page in reverse.
        var backwardPages = new List<IReadOnlyList<Guid>> { forwardPages[^1] };
        var backToken = lastPreviousToken;
        for (var i = forwardPages.Count - 2; i >= 0; i--)
        {
            var page = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(backToken, 3), _ct);
            backwardPages.Add(page.Items.Select(i2 => i2.Id).ToArray());
            backToken = page.PreviousToken!.Value;
        }

        // Assert
        for (var i = 0; i < forwardPages.Count; i++)
        {
            backwardPages[i].ShouldBe(forwardPages[forwardPages.Count - 1 - i]);
        }
    }

    [Fact]
    public async Task Empty_page_from_Beginning_on_empty_set_has_non_null_NextToken_that_picks_up_inserted_rows()
    {
        // Arrange - no rows yet
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        var firstPage = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        firstPage.Items.Count.ShouldBe(0);
        var resumeToken = firstPage.NextToken.ShouldNotBeNull();

        // Act - insert rows, then follow the empty page's token
        var id1 = await CreateEntityAsync(partitionedStorage, "Item1", 10, ct: _ct);
        var id2 = await CreateEntityAsync(partitionedStorage, "Item2", 20, ct: _ct);

        var page = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(resumeToken.Value, 3), _ct);

        // Assert
        page.Items.Select(i => i.Id).ShouldBe([id1, id2]);
    }

    [Fact]
    public async Task Empty_pages_return_the_given_position_so_following_PreviousToken_matches_the_originating_page()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var ids = new List<Guid>();
        for (var i = 1; i <= 3; i++)
        {
            ids.Add(await CreateEntityAsync(partitionedStorage, $"Item{i}", i * 10, ct: _ct));
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        var page1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        page1.HasMoreData.ShouldBeFalse();
        var resumeToken = page1.NextToken.ShouldNotBeNull();

        // Act — follow the still-empty next page, then its PreviousToken. The empty page was reached
        // from page1's last item, so its PreviousToken carries that same position: querying it returns
        // every row strictly before that last item, i.e. page1 minus its own last item.
        var emptyPage = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(resumeToken.Value, 3), _ct);
        emptyPage.Items.Count.ShouldBe(0);

        var viaEmptyPage = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(emptyPage.PreviousToken!.Value, 3), _ct);

        // Assert — the position carried through the empty page is preserved, not lost or reset.
        viaEmptyPage.Items.Select(i => i.Id).ShouldBe(ids.Take(2));
    }

    [Fact]
    public async Task First_page_flags_HasPreviousData_false_HasMoreData_true()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 6; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        var page1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);

        page1.HasPreviousData.ShouldBeFalse();
        page1.HasMoreData.ShouldBeTrue();
        // A first page still carries a PreviousToken (enables discovering new records before it) and a
        // NextToken (enables continuing forward).
        _ = page1.PreviousToken.ShouldNotBeNull();
        _ = page1.NextToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task Middle_page_flags_are_both_true_reached_forward_or_backward()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        var page1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2Forward = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);

        page2Forward.HasPreviousData.ShouldBeTrue();
        page2Forward.HasMoreData.ShouldBeTrue();

        var page3 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page2Forward.NextToken!.Value, 3), _ct);
        var page2Backward = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page3.PreviousToken!.Value, 3), _ct);

        page2Backward.HasPreviousData.ShouldBeTrue();
        page2Backward.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task Backward_page_reaching_the_start_has_HasPreviousData_false_HasMoreData_true()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 6; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        var page1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);

        var backToPage1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page2.PreviousToken!.Value, 3), _ct);

        backToPage1.HasPreviousData.ShouldBeFalse();
        backToPage1.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task Backward_navigation_with_string_sort_reproduces_prior_page_in_requested_order()
    {
        // Arrange — create items with string names for sort
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        _ = await CreateEntityAsync(partitionedStorage, "Alpha", 1, ct: _ct);
        _ = await CreateEntityAsync(partitionedStorage, "Bravo", 2, ct: _ct);
        _ = await CreateEntityAsync(partitionedStorage, "Charlie", 3, ct: _ct);
        _ = await CreateEntityAsync(partitionedStorage, "Delta", 4, ct: _ct);
        _ = await CreateEntityAsync(partitionedStorage, "Echo", 5, ct: _ct);
        _ = await CreateEntityAsync(partitionedStorage, "Foxtrot", 6, ct: _ct);

        var filter = Query.All();
        var sort = new SortParameter(new StringField("name"));

        // Act — navigate forward: page 1 (Alpha, Bravo) → page 2 (Charlie, Delta) → page 3 (Echo, Foxtrot)
        var page1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 2), _ct);
        var page2 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 2), _ct);
        var page3 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page2.NextToken!.Value, 2), _ct);

        // Act — navigate backward from page 3 with the SAME sort
        var backToPage2 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page3.PreviousToken!.Value, 2), _ct);

        // Assert — backward gives page 2's items in requested (forward) order
        backToPage2.Items.Select(i => ((TestCursorDso)i.Value).Name).ShouldBe(page2.Items.Select(i => ((TestCursorDso)i.Value).Name));
    }

    [Fact]
    public async Task Backward_navigation_with_datetime_sort_reproduces_prior_page_in_requested_order()
    {
        // Arrange — create items with distinct timestamps
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var baseDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 6; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i, baseDate.AddDays(i), true, _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new DateTimeField("recordedAt"));

        // Act — navigate forward
        var page1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 2), _ct);
        var page2 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 2), _ct);
        var page3 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page2.NextToken!.Value, 2), _ct);

        // Act — navigate backward from page 3 with the SAME sort
        var backToPage2 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page3.PreviousToken!.Value, 2), _ct);

        // Assert — backward gives page 2's items in requested (forward) order
        backToPage2.Items.Select(i => ((TestCursorDso)i.Value).CreatedAt).ShouldBe(page2.Items.Select(i => ((TestCursorDso)i.Value).CreatedAt));
    }

    [Fact]
    public async Task Backward_navigation_respects_a_filter_applied_to_the_query()
    {
        // Arrange — create items, apply filter, verify backward navigation respects filter
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 12; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct);
        }

        // Filter: rank > 30 → items 4-12 (9 items, page size 3 → 3 pages)
        var filter = new NumberField("rank").GreaterThan(30);
        var sort = new SortParameter(new NumberField("rank"));

        // Act — navigate forward (filtered: 40,50,60 | 70,80,90 | 100,110,120)
        var page1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);
        var page3 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page2.NextToken!.Value, 3), _ct);

        // Act — navigate backward from page 3 with the SAME sort
        var backToPage2 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page3.PreviousToken!.Value, 3), _ct);

        // Assert — backward should return filtered page 2 items in requested (forward) order,
        // never items the filter excludes (e.g. rank 10/20/30).
        backToPage2.Items.Select(i => ((TestCursorDso)i.Value).Rank).ShouldBe(page2.Items.Select(i => ((TestCursorDso)i.Value).Rank));
        backToPage2.Items.Select(i => ((TestCursorDso)i.Value).Rank).ShouldAllBe(rank => rank > 30);
    }

    [Fact]
    public async Task Previous_token_from_the_first_page_finds_nothing_before_it()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 6; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act — get first page
        var page1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);

        // Act — use PreviousToken from page 1 with the SAME sort: nothing comes before the first page
        var backFromPage1 = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.PreviousToken!.Value, 3), _ct);

        // Assert — no items before the first page
        backFromPage1.Items.Count.ShouldBe(0);
        backFromPage1.HasPreviousData.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Offset_mode_HasPreviousData_equals_skip_greater_than_zero(int skip)
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var range = DataRange.FromOffset(skip, 3);

        var result = await partitionedStorage.QueryAsync(_testEntityType, filter, sort, range, _ct);

        result.HasPreviousData.ShouldBe(skip > 0);
        result.PreviousToken.ShouldBeNull();
        result.NextToken.ShouldBeNull();
    }

    [Fact]
    public async Task Offset_mode_search_without_matches_on_page_2_has_no_previous_data()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = new StringField("name").StartsWith("Gadget");
        var sort = new SortParameter(new StringField("name"));

        var result = await partitionedStorage.QueryAsync(
            _testEntityType, filter, sort, DataRange.FromPage(2, 3), _ct);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
        result.HasPreviousData.ShouldBeFalse();
    }

    [Fact]
    public async Task Offset_mode_page_past_the_end_with_matches_has_previous_data()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 5; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        var result = await partitionedStorage.QueryAsync(
            _testEntityType, filter, sort, DataRange.FromPage(3, 3), _ct);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(5);
        result.HasPreviousData.ShouldBeTrue();
    }

    [Fact]
    public async Task Garbage_token_throws_InvalidContinuationTokenException()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;
        _ = await CreateEntityAsync(partitionedStorage, "Item1", 10, ct: _ct);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var badToken = ContinuationToken.Create("not a real token at all");

        var ex = await Should.ThrowAsync<InvalidContinuationTokenException>(async () =>
            await partitionedStorage.QueryAsync(_testEntityType, filter, sort, DataRange.FromContinuationToken(badToken, 3), _ct));

        ex.Reason.ShouldBe(InvalidContinuationTokenReason.Malformed);
    }

    [Fact]
    public async Task Token_from_flipped_sort_direction_throws_SortMismatch()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(partitionedStorage, $"Item{i}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var ascending = new SortParameter(new NumberField("rank"));
        var descending = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        var page = await partitionedStorage.QueryAsync(_testEntityType, filter, ascending, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);

        // Act & Assert — using NextToken (forward) with the opposite sort direction is a mismatch,
        // not a valid "flip the sort to go backward" flow under the new contract.
        var ex = await Should.ThrowAsync<InvalidContinuationTokenException>(async () =>
            await partitionedStorage.QueryAsync(_testEntityType, filter, descending, DataRange.FromContinuationToken(page.NextToken!.Value, 3), _ct));

        ex.Reason.ShouldBe(InvalidContinuationTokenReason.SortMismatch);
    }

    [Fact]
    public async Task QueryFieldsAsync_sorted_round_trip_forward_then_backward()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var ids = new List<Guid>();
        for (var i = 1; i <= 7; i++)
        {
            ids.Add(await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct));
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };

        var page1 = await partitionedStorage.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await partitionedStorage.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);

        // Act - backward from page2 with the same sort
        var back = await partitionedStorage.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromContinuationToken(page2.PreviousToken!.Value, 3), _ct);

        // Assert - items in requested order, same as page1
        back.Items.Select(i => i.Id).ShouldBe(page1.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task QueryFieldsAsync_unsorted_traversal_and_bidirectional_round_trip()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var ids = new List<Guid>();
        for (var i = 1; i <= 7; i++)
        {
            ids.Add(await CreateEntityAsync(partitionedStorage, $"Item{i:D2}", i * 10, ct: _ct));
        }

        var filter = Query.All();
        var sort = SortParameter.Empty;
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };

        // Act - page forward to the end with page size 3, remembering each page and the last
        // page's own PreviousToken.
        var forwardPages = new List<IReadOnlyList<Guid>>();
        ContinuationToken token = ContinuationToken.Beginning;
        ContinuationToken lastPreviousToken = ContinuationToken.Beginning;
        var firstPageHasPreviousData = (bool?)null;
        var lastPageHasMoreData = true;
        while (true)
        {
            var page = await partitionedStorage.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromContinuationToken(token, 3), _ct);
            firstPageHasPreviousData ??= page.HasPreviousData;
            forwardPages.Add(page.Items.Select(i => i.Id).ToArray());
            lastPreviousToken = page.PreviousToken!.Value;
            lastPageHasMoreData = page.HasMoreData;
            if (!page.HasMoreData)
            {
                break;
            }

            token = page.NextToken!.Value;
        }

        // Act - follow PreviousToken back from the last page with the same (empty) sort. The last
        // page's own items are already known; walking Previous reconstructs every earlier page in reverse.
        var backwardPages = new List<IReadOnlyList<Guid>> { forwardPages[^1] };
        var backToken = lastPreviousToken;
        for (var i = forwardPages.Count - 2; i >= 0; i--)
        {
            var page = await partitionedStorage.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromContinuationToken(backToken, 3), _ct);
            backwardPages.Add(page.Items.Select(i2 => i2.Id).ToArray());
            backToken = page.PreviousToken!.Value;
        }

        // Assert - every id appears exactly once across the forward pages
        var collected = forwardPages.SelectMany(p => p).ToList();
        collected.Count.ShouldBe(7);
        collected.Distinct().Count().ShouldBe(7);
        collected.ToHashSet().SetEquals(ids).ShouldBeTrue();

        // Assert - backward pages equal the forward pages in reverse, each page in requested
        // (forward) order.
        for (var i = 0; i < forwardPages.Count; i++)
        {
            backwardPages[i].ShouldBe(forwardPages[forwardPages.Count - 1 - i]);
        }

        // Assert - flags on the first and last page
        firstPageHasPreviousData.ShouldBe(false);
        lastPageHasMoreData.ShouldBeFalse();
    }


    [Fact]
    public async Task QueryFieldsAsync_invalid_token_throws_InvalidContinuationTokenException()
    {
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;
        _ = await CreateEntityAsync(partitionedStorage, "Item1", 10, ct: _ct);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var fields = new List<Field> { new StringField("name") };
        var badToken = ContinuationToken.Create("garbage-token-value");

        _ = await Should.ThrowAsync<InvalidContinuationTokenException>(async () =>
            await partitionedStorage.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromContinuationToken(badToken, 3), _ct));
    }

    // --- F1: fractional number sort values must not round through Convert.ToDecimal(double) ---

    [Fact]
    public async Task Fractional_number_sort_values_differing_in_the_16th_or_17th_significant_digit_traverse_without_repeats_or_gaps()
    {
        // Arrange — values that differ only in the 16th/17th significant digit, plus a couple of whole
        // numbers, so a lossy double->decimal round trip (Convert.ToDecimal rounds to 15 significant
        // digits) would make the cursor value not equal the stored double and repeat a row.
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var ratings = new[]
        {
            1.000000000000000m,
            1.0000000000000002m,
            1.0000000000000004m,
            1.000000000000001m,
            2m,
            3m,
        };

        var ids = new List<Guid>();
        foreach (var rating in ratings)
        {
            ids.Add(await CreateRatingEntityAsync(partitionedStorage, $"Product-{rating}", rating, _ct));
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rating"));

        // Act — page forward to the end, one row at a time.
        var forwardIds = new List<Guid>();
        ContinuationToken token = ContinuationToken.Beginning;
        QueryResult<MetadataEnvelope> page;
        while (true)
        {
            page = await partitionedStorage.QueryAsync(
                _testSortEntityType, filter, sort, DataRange.FromContinuationToken(token, 1), _ct);
            forwardIds.AddRange(page.Items.Select(i => i.Id));
            if (!page.HasMoreData)
            {
                break;
            }

            token = page.NextToken!.Value;
        }

        // Assert — every record exactly once, forward.
        forwardIds.Count.ShouldBe(ids.Count);
        forwardIds.ShouldBe(ids, ignoreOrder: true);
        forwardIds.Distinct().Count().ShouldBe(ids.Count);

        // Act — page backward from the last page using PreviousToken and the SAME sort.
        var backwardIds = new List<Guid>(page.Items.Select(i => i.Id));
        while (page.HasPreviousData && page.PreviousToken is { } previousToken)
        {
            page = await partitionedStorage.QueryAsync(
                _testSortEntityType, filter, sort, DataRange.FromContinuationToken(previousToken, 1), _ct);
            backwardIds.InsertRange(0, page.Items.Select(i => i.Id));
        }

        // Assert — backward pages equal the forward pages, reversed.
        backwardIds.ShouldBe(forwardIds);
    }

    // --- F2: the opposite-side flag is inferred, not probed ---

    [Fact]
    public async Task Inferred_opposite_side_flag_can_be_true_with_no_rows_left_after_a_rank_update()
    {
        // Arrange — A(rank 10), B(rank 20), page size 1.
        await using var fixture = await CreateProviderAsync();
        var partitionedStorage = fixture.PartitionedStorage;

        var idA = await CreateEntityAsync(partitionedStorage, "A", 10, ct: _ct);
        var idB = await CreateEntityAsync(partitionedStorage, "B", 20, ct: _ct);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        var page1 = await partitionedStorage.QueryAsync(
            _testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 1), _ct);
        page1.Items.Select(i => i.Id).ShouldBe([idA]);

        // Act — A's rank moves past B's.
        await UpdateRankAsync(partitionedStorage, idA, 30, _ct);

        // Act — follow page 1's NextToken with the same sort.
        var page2 = await partitionedStorage.QueryAsync(
            _testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 1), _ct);

        // Assert — B is now the next row. HasPreviousData is inferred true (not probed): the query
        // started from a key position, not Beginning.
        page2.Items.Select(i => i.Id).ShouldBe([idB]);
        page2.HasPreviousData.ShouldBeTrue();

        // Act — follow B's PreviousToken with the same sort.
        var page3 = await partitionedStorage.QueryAsync(
            _testEntityType, filter, sort, DataRange.FromContinuationToken(page2.PreviousToken!.Value, 1), _ct);

        // Assert — an empty page: A's updated rank (30) no longer sorts before B's (20), so nothing
        // satisfies the backward seek predicate. HasPreviousData is the exact same-side flag here
        // (backward traversal), so it correctly reports false even though the inferred HasMoreData stays true.
        page3.Items.ShouldBeEmpty();
        page3.HasPreviousData.ShouldBeFalse();
    }
}
