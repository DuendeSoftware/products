// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Pagination;
using SortDirection = Duende.Storage.Querying.SortDirection;
using SortParameter = Duende.Storage.Internal.Querying.Sorting.SortParameter;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Tests for bidirectional cursor-based pagination (forward + backward navigation).
/// Verifies that PreviousToken is populated on every page, and that the consumer
/// can navigate backward by reversing the sort direction and using PreviousToken.
/// </summary>
public partial class QueryStoreCursorBidirectionalPagingTests
{
    private readonly EntityType _testEntityType = TestCursorDso.DsoVersion.EntityType;

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestCursorDso>();
        });

    private static async Task<UuidV7> CreateEntityAsync(
        IStore store,
        string name,
        int rank,
        DateTimeOffset? createdAt = null,
        bool? isActive = null,
        Ct ct = default)
    {
        var id = UuidV7.New();
        var dso = new TestCursorDso
        {
            Name = name,
            Rank = rank,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            IsActive = isActive ?? true
        };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);
        _ = searchFieldsBuilder.Add("rank", rank);
        _ = searchFieldsBuilder.Add("recordedAt", dso.CreatedAt);
        _ = searchFieldsBuilder.Add("isActive", dso.IsActive);

        var searchFields = searchFieldsBuilder.Build();

        var result = await store.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id;
    }

    [Fact]
    public async Task first_page_should_have_previous_token()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 6; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act — fetch first page
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);

        // Assert — first page SHOULD have a PreviousToken (enables discovering new records before the current first item)
        page1.Items.Count.ShouldBe(3);
        _ = page1.PreviousToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task page2_should_have_previous_token()
    {
        // Arrange — create 9 items, page size 3 → 3 pages
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act — navigate to page 2
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);

        // Assert — page 2 should have a PreviousToken
        page2.Items.Count.ShouldBe(3);
        page2.Items[0].Value.Rank.ShouldBe(40);
        page2.Items[2].Value.Rank.ShouldBe(60);
        _ = page2.PreviousToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task navigate_forward_then_backward_should_return_previous_page()
    {
        // Arrange — create 9 items with ascending rank, page size 3
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var forwardSort = new SortParameter(new NumberField("rank"));
        var backwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act — navigate forward: page 1 → page 2 → page 3
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page2.NextToken!.Value, 3), _ct);

        // Act — navigate backward from page 3: reverse sort + use PreviousToken
        var previousToken = page3.PreviousToken.ShouldNotBeNull();
        var backToPage2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 3), _ct);

        // Assert — going back from page 3 should return the same items as page 2 (in reverse order due to reversed sort)
        backToPage2.Items.Count.ShouldBe(3);
        backToPage2.Items[0].Value.Rank.ShouldBe(60);
        backToPage2.Items[1].Value.Rank.ShouldBe(50);
        backToPage2.Items[2].Value.Rank.ShouldBe(40);
    }

    [Fact]
    public async Task navigate_backward_from_page2_should_return_first_page()
    {
        // Arrange — create 9 items, page size 3
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var forwardSort = new SortParameter(new NumberField("rank"));
        var backwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act — navigate to page 2
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);

        // Act — navigate backward from page 2: reverse sort + use PreviousToken
        var previousToken = page2.PreviousToken.ShouldNotBeNull();
        var backToPage1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 3), _ct);

        // Assert — going back from page 2 should return page 1 items (in reverse order)
        backToPage1.Items.Count.ShouldBe(3);
        backToPage1.Items[0].Value.Rank.ShouldBe(30);
        backToPage1.Items[1].Value.Rank.ShouldBe(20);
        backToPage1.Items[2].Value.Rank.ShouldBe(10);
    }

    [Fact]
    public async Task full_round_trip_forward_and_backward()
    {
        // Arrange — create 12 items, page size 3 → 4 pages
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 12; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var forwardSort = new SortParameter(new NumberField("rank"));
        var backwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act — navigate forward through all pages
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page2.NextToken!.Value, 3), _ct);
        var page4 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page3.NextToken!.Value, 3), _ct);

        // Assert forward navigation
        page1.Items[0].Value.Rank.ShouldBe(10);
        page2.Items[0].Value.Rank.ShouldBe(40);
        page3.Items[0].Value.Rank.ShouldBe(70);
        page4.Items[0].Value.Rank.ShouldBe(100);
        page4.HasMoreData.ShouldBeFalse();

        // Act — navigate backward from page 4 using PreviousToken + reversed sort
        // Page 4 first item is rank 100, so PreviousToken encodes position at 100
        // Descending seek from 100: items with rank < 100 → 90, 80, 70
        var prevToken4 = page4.PreviousToken.ShouldNotBeNull();
        var back3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(prevToken4.Value, 3), _ct);

        // Assert — first backward page contains items immediately before page 4
        back3.Items.Count.ShouldBe(3);
        back3.Items[0].Value.Rank.ShouldBe(90);
        back3.Items[1].Value.Rank.ShouldBe(80);
        back3.Items[2].Value.Rank.ShouldBe(70);
    }

    [Fact]
    public async Task descending_sort_backward_navigation()
    {
        // Arrange — create 9 items, descending sort by rank, page size 3
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var forwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);
        var backwardSort = new SortParameter(new NumberField("rank")); // Ascending = reverse of descending

        // Act — navigate forward (descending: 90, 80, 70 | 60, 50, 40 | 30, 20, 10)
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page2.NextToken!.Value, 3), _ct);

        // Assert forward
        page1.Items[0].Value.Rank.ShouldBe(90);
        page2.Items[0].Value.Rank.ShouldBe(60);
        page3.Items[0].Value.Rank.ShouldBe(30);

        // Act — navigate backward from page 3 (reverse sort = ascending + PreviousToken)
        var previousToken = page3.PreviousToken.ShouldNotBeNull();
        var backToPage2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 3), _ct);

        // Assert — should return page 2 items in ascending order (reversed)
        backToPage2.Items.Count.ShouldBe(3);
        backToPage2.Items[0].Value.Rank.ShouldBe(40);
        backToPage2.Items[1].Value.Rank.ShouldBe(50);
        backToPage2.Items[2].Value.Rank.ShouldBe(60);
    }

    [Fact]
    public async Task string_sort_backward_navigation()
    {
        // Arrange — create items with string names for sort
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Alpha", 1, ct: _ct);
        _ = await CreateEntityAsync(store, "Bravo", 2, ct: _ct);
        _ = await CreateEntityAsync(store, "Charlie", 3, ct: _ct);
        _ = await CreateEntityAsync(store, "Delta", 4, ct: _ct);
        _ = await CreateEntityAsync(store, "Echo", 5, ct: _ct);
        _ = await CreateEntityAsync(store, "Foxtrot", 6, ct: _ct);

        var filter = Query.All();
        var forwardSort = new SortParameter(new StringField("name"));
        var backwardSort = new SortParameter(new StringField("name"), SortDirection.Descending);

        // Act — navigate forward: page 1 (Alpha, Bravo) → page 2 (Charlie, Delta) → page 3 (Echo, Foxtrot)
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 2), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 2), _ct);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page2.NextToken!.Value, 2), _ct);

        // Assert forward
        page2.Items[0].Value.Name.ShouldBe("Charlie");
        page2.Items[1].Value.Name.ShouldBe("Delta");

        // Act — navigate backward from page 3
        var previousToken = page3.PreviousToken.ShouldNotBeNull();
        var backToPage2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 2), _ct);

        // Assert backward (reversed order)
        backToPage2.Items.Count.ShouldBe(2);
        backToPage2.Items[0].Value.Name.ShouldBe("Delta");
        backToPage2.Items[1].Value.Name.ShouldBe("Charlie");
    }

    [Fact]
    public async Task datetime_sort_backward_navigation()
    {
        // Arrange — create items with distinct timestamps
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 6; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i, baseDate.AddDays(i), true, _ct);
        }

        var filter = Query.All();
        var forwardSort = new SortParameter(new DateTimeField("recordedAt"));
        var backwardSort = new SortParameter(new DateTimeField("recordedAt"), SortDirection.Descending);

        // Act — navigate forward
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 2), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 2), _ct);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page2.NextToken!.Value, 2), _ct);

        // Act — navigate backward from page 3
        var previousToken = page3.PreviousToken.ShouldNotBeNull();
        var backToPage2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 2), _ct);

        // Assert backward (reversed order)
        backToPage2.Items.Count.ShouldBe(2);
        backToPage2.Items[0].Value.CreatedAt.ShouldBe(baseDate.AddDays(4));
        backToPage2.Items[1].Value.CreatedAt.ShouldBe(baseDate.AddDays(3));
    }

    [Fact]
    public async Task query_fields_backward_navigation()
    {
        // Arrange — test bidirectional navigation with QueryFieldsAsync
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var forwardSort = new SortParameter(new NumberField("rank"));
        var backwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };

        // Act — navigate forward
        var page1 = await store.QueryFieldsAsync(_testEntityType, fields, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryFieldsAsync(_testEntityType, fields, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);
        var page3 = await store.QueryFieldsAsync(_testEntityType, fields, filter, forwardSort, DataRange.FromContinuationToken(page2.NextToken!.Value, 3), _ct);

        // Assert forward
        page2.Items[0].Fields["RANK"].ShouldBe(40m);

        // Act — navigate backward from page 3
        var previousToken = page3.PreviousToken.ShouldNotBeNull();
        var backToPage2 = await store.QueryFieldsAsync(_testEntityType, fields, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 3), _ct);

        // Assert backward (reversed order)
        backToPage2.Items.Count.ShouldBe(3);
        backToPage2.Items[0].Fields["RANK"].ShouldBe(60m);
        backToPage2.Items[1].Fields["RANK"].ShouldBe(50m);
        backToPage2.Items[2].Fields["RANK"].ShouldBe(40m);
    }

    [Fact]
    public async Task filter_respects_backward_navigation()
    {
        // Arrange — create items, apply filter, verify backward navigation respects filter
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 12; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        // Filter: rank > 30 → items 4-12 (9 items, page size 3 → 3 pages)
        var filter = new NumberField("rank").GreaterThan(30);
        var forwardSort = new SortParameter(new NumberField("rank"));
        var backwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act — navigate forward
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page2.NextToken!.Value, 3), _ct);

        // Assert forward (filtered: 40,50,60 | 70,80,90 | 100,110,120)
        page2.Items[0].Value.Rank.ShouldBe(70);

        // Act — navigate backward from page 3
        var previousToken = page3.PreviousToken.ShouldNotBeNull();
        var backToPage2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 3), _ct);

        // Assert backward — should return filtered page 2 items in reverse
        backToPage2.Items.Count.ShouldBe(3);
        backToPage2.Items[0].Value.Rank.ShouldBe(90);
        backToPage2.Items[1].Value.Rank.ShouldBe(80);
        backToPage2.Items[2].Value.Rank.ShouldBe(70);
    }

    [Fact]
    public async Task backward_navigation_returns_correct_prior_page()
    {
        // Arrange — items with duplicate sort values, verify backward navigation works
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 6 items with distinct sort values to avoid tie-breaking complexity
        _ = await CreateEntityAsync(store, "A", 10, ct: _ct);
        _ = await CreateEntityAsync(store, "B", 20, ct: _ct);
        _ = await CreateEntityAsync(store, "C", 30, ct: _ct);
        _ = await CreateEntityAsync(store, "D", 40, ct: _ct);
        _ = await CreateEntityAsync(store, "E", 50, ct: _ct);
        _ = await CreateEntityAsync(store, "F", 60, ct: _ct);

        var filter = Query.All();
        var forwardSort = new SortParameter(new NumberField("rank"));
        var backwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act — navigate forward (page 1: 10,20,30 | page 2: 40,50,60)
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);

        // Page 2 first item is rank 40
        page2.Items[0].Value.Rank.ShouldBe(40);

        // Act — navigate backward from page 2 (descending from position 40: gets 30, 20, 10)
        var previousToken = page2.PreviousToken.ShouldNotBeNull();
        var backToPage1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 3), _ct);

        // Assert — backward gives items before page 2 start in descending order
        backToPage1.Items.Count.ShouldBe(3);
        backToPage1.Items[0].Value.Rank.ShouldBe(30);
        backToPage1.Items[1].Value.Rank.ShouldBe(20);
        backToPage1.Items[2].Value.Rank.ShouldBe(10);
    }

    [Fact]
    public async Task empty_result_should_not_have_previous_token()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Item1", 10, ct: _ct);

        var filter = new NumberField("rank").GreaterThan(100);
        var sort = new SortParameter(new NumberField("rank"));

        // Act — query returns no results
        var result = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 10), _ct);

        // Assert
        result.Items.Count.ShouldBe(0);
        result.PreviousToken.ShouldBeNull();
        result.NextToken.ShouldBeNull();
    }

    [Fact]
    public async Task backward_from_page1_using_previous_token()
    {
        // Arrange — create 6 items
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 6; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var forwardSort = new SortParameter(new NumberField("rank"));
        var backwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act — get first page
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);

        // Act — use PreviousToken from page 1 with reversed sort
        // Since page1 first item is rank 10, descending seek from 10 returns items < 10 → empty
        var previousToken = page1.PreviousToken.ShouldNotBeNull();
        var backFromPage1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 3), _ct);

        // Assert — no items before the first page
        backFromPage1.Items.Count.ShouldBe(0);
        backFromPage1.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task page_size_of_1_backward_navigation()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "A", 10, ct: _ct);
        _ = await CreateEntityAsync(store, "B", 20, ct: _ct);
        _ = await CreateEntityAsync(store, "C", 30, ct: _ct);

        var filter = Query.All();
        var forwardSort = new SortParameter(new NumberField("rank"));
        var backwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act — navigate forward with page size 1
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 1), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 1), _ct);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page2.NextToken!.Value, 1), _ct);

        // Assert forward
        page1.Items[0].Value.Rank.ShouldBe(10);
        page2.Items[0].Value.Rank.ShouldBe(20);
        page3.Items[0].Value.Rank.ShouldBe(30);

        // Act — navigate backward from page 3 with page size 1
        var previousToken = page3.PreviousToken.ShouldNotBeNull();
        var back = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 1), _ct);

        // Assert — should get item immediately before page 3
        back.Items.Count.ShouldBe(1);
        back.Items[0].Value.Rank.ShouldBe(20);
    }

    [Fact]
    public async Task backward_query_has_correct_next_and_previous_tokens()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var forwardSort = new SortParameter(new NumberField("rank"));
        var backwardSort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Navigate forward to page 3
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, forwardSort, DataRange.FromContinuationToken(page2.NextToken!.Value, 3), _ct);

        // Act — backward from page 3
        var previousToken = page3.PreviousToken.ShouldNotBeNull();
        var backResult = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, backwardSort, DataRange.FromContinuationToken(previousToken.Value, 3), _ct);

        // Assert — backward result should have both NextToken and PreviousToken
        backResult.Items.Count.ShouldBe(3);
        _ = backResult.NextToken.ShouldNotBeNull();
        _ = backResult.PreviousToken.ShouldNotBeNull();
        backResult.HasMoreData.ShouldBeTrue();
    }
}
