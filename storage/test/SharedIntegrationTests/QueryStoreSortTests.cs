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
/// Tests for sorting functionality across all store implementations.
/// Covers sorting by different field types (string, number, datetime) in both directions,
/// and verifies correct behavior with filtering and pagination.
/// </summary>
public partial class QueryStoreSortTests
{

    private readonly EntityType _testEntityType = new(4, "SortTestEntity");

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestEntityDso>();
            services.AddDsoRegistration<TestUserDso>();
            services.AddDsoRegistration<TestSortDso>();
        });

    private static async Task<UuidV7> CreateEntityAsync(
        IStore store,
        string name,
        int? rank = null,
        decimal? rating = null,
        DateTimeOffset? timestamp = null,
        string? category = null,
        Ct ct = default)
    {
        var id = UuidV7.New();
        var dso = new TestSortDso
        {
            Name = name,
            Rank = rank,
            Rating = rating,
            Timestamp = timestamp,
            Category = category
        };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);
        if (rank.HasValue)
        {
            _ = searchFieldsBuilder.Add("rank", rank.Value);
        }

        if (rating.HasValue)
        {
            _ = searchFieldsBuilder.Add("rating", rating.Value);
        }

        if (timestamp.HasValue)
        {
            _ = searchFieldsBuilder.Add("timestamp", timestamp.Value);
        }

        if (category != null)
        {
            _ = searchFieldsBuilder.Add("category", category);
        }

        var searchFields = searchFieldsBuilder.Build();

        var storeInterface = store;
        var result = await storeInterface.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id;
    }

    [Fact]
    public async Task QuerySortByStringAscendingShouldReturnAlphabeticalOrderAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Zebra");
        _ = await CreateEntityAsync(store, "Apple");
        _ = await CreateEntityAsync(store, "Mango");
        _ = await CreateEntityAsync(store, "Banana");

        var filter = Query.All();
        var sort = new SortParameter(new StringField("name"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Name.ShouldBe("Apple");
        result.Items[1].Value.Name.ShouldBe("Banana");
        result.Items[2].Value.Name.ShouldBe("Mango");
        result.Items[3].Value.Name.ShouldBe("Zebra");
    }

    [Fact]
    public async Task QuerySortByStringDescendingShouldReturnReverseAlphabeticalOrderAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Alpha");
        _ = await CreateEntityAsync(store, "Bravo");
        _ = await CreateEntityAsync(store, "Charlie");
        _ = await CreateEntityAsync(store, "Delta");

        var filter = Query.All();
        var sort = new SortParameter(new StringField("name"), SortDirection.Descending);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Name.ShouldBe("Delta");
        result.Items[1].Value.Name.ShouldBe("Charlie");
        result.Items[2].Value.Name.ShouldBe("Bravo");
        result.Items[3].Value.Name.ShouldBe("Alpha");
    }

    [Fact]
    public async Task QuerySortByStringWithNumbersShouldSortLexicographicallyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Item10");
        _ = await CreateEntityAsync(store, "Item2");
        _ = await CreateEntityAsync(store, "Item1");
        _ = await CreateEntityAsync(store, "Item20");

        var filter = Query.All();
        var sort = new SortParameter(new StringField("name"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert - Lexicographic order, not numeric
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Name.ShouldBe("Item1");
        result.Items[1].Value.Name.ShouldBe("Item10");
        result.Items[2].Value.Name.ShouldBe("Item2");
        result.Items[3].Value.Name.ShouldBe("Item20");
    }

    [Fact]
    public async Task QuerySortByNumberAscendingShouldReturnSmallestToLargestAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Item1", rank: 42);
        _ = await CreateEntityAsync(store, "Item2", rank: 7);
        _ = await CreateEntityAsync(store, "Item3", rank: 99);
        _ = await CreateEntityAsync(store, "Item4", rank: 23);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Rank.ShouldBe(7);
        result.Items[1].Value.Rank.ShouldBe(23);
        result.Items[2].Value.Rank.ShouldBe(42);
        result.Items[3].Value.Rank.ShouldBe(99);
    }

    [Fact]
    public async Task QuerySortByNumberDescendingShouldReturnLargestToSmallestAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Item1", rank: 10);
        _ = await CreateEntityAsync(store, "Item2", rank: 50);
        _ = await CreateEntityAsync(store, "Item3", rank: 30);
        _ = await CreateEntityAsync(store, "Item4", rank: 40);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), SortDirection.Descending);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Rank.ShouldBe(50);
        result.Items[1].Value.Rank.ShouldBe(40);
        result.Items[2].Value.Rank.ShouldBe(30);
        result.Items[3].Value.Rank.ShouldBe(10);
    }

    [Fact]
    public async Task QuerySortByDecimalAscendingShouldHandlePrecisionAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Product1", rating: 4.5m);
        _ = await CreateEntityAsync(store, "Product2", rating: 4.25m);
        _ = await CreateEntityAsync(store, "Product3", rating: 4.75m);
        _ = await CreateEntityAsync(store, "Product4", rating: 4.1m);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rating"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Rating.ShouldBe(4.1m);
        result.Items[1].Value.Rating.ShouldBe(4.25m);
        result.Items[2].Value.Rating.ShouldBe(4.5m);
        result.Items[3].Value.Rating.ShouldBe(4.75m);
    }

    [Fact]
    public async Task QuerySortByNumberWithNegativeValuesShouldSortCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Item1", rank: -10);
        _ = await CreateEntityAsync(store, "Item2", rank: 5);
        _ = await CreateEntityAsync(store, "Item3", rank: -25);
        _ = await CreateEntityAsync(store, "Item4", rank: 0);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Rank.ShouldBe(-25);
        result.Items[1].Value.Rank.ShouldBe(-10);
        result.Items[2].Value.Rank.ShouldBe(0);
        result.Items[3].Value.Rank.ShouldBe(5);
    }

    [Fact]
    public async Task QuerySortByDateTimeAscendingShouldReturnOldestToNewestAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);
        var date4 = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateEntityAsync(store, "Event1", timestamp: date1);
        _ = await CreateEntityAsync(store, "Event2", timestamp: date2);
        _ = await CreateEntityAsync(store, "Event3", timestamp: date3);
        _ = await CreateEntityAsync(store, "Event4", timestamp: date4);

        var filter = Query.All();
        var sort = new SortParameter(new DateTimeField("timestamp"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Name.ShouldBe("Event2"); // Jan 1
        result.Items[1].Value.Name.ShouldBe("Event4"); // Mar 1
        result.Items[2].Value.Name.ShouldBe("Event1"); // Jun 1
        result.Items[3].Value.Name.ShouldBe("Event3"); // Dec 1
    }

    [Fact]
    public async Task QuerySortByDateTimeDescendingShouldReturnNewestToOldestAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date4 = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateEntityAsync(store, "Event2023", timestamp: date1);
        _ = await CreateEntityAsync(store, "Event2024", timestamp: date2);
        _ = await CreateEntityAsync(store, "Event2025", timestamp: date3);
        _ = await CreateEntityAsync(store, "Event2022", timestamp: date4);

        var filter = Query.All();
        var sort = new SortParameter(new DateTimeField("timestamp"), SortDirection.Descending);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Name.ShouldBe("Event2025");
        result.Items[1].Value.Name.ShouldBe("Event2024");
        result.Items[2].Value.Name.ShouldBe("Event2023");
        result.Items[3].Value.Name.ShouldBe("Event2022");
    }

    [Fact]
    public async Task QuerySortByDateTimeWithSameDateShouldSortByTimeAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var time1 = new DateTimeOffset(2024, 6, 1, 14, 0, 0, TimeSpan.Zero);
        var time2 = new DateTimeOffset(2024, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var time3 = new DateTimeOffset(2024, 6, 1, 18, 0, 0, TimeSpan.Zero);
        var time4 = new DateTimeOffset(2024, 6, 1, 6, 0, 0, TimeSpan.Zero);

        _ = await CreateEntityAsync(store, "Event14:00", timestamp: time1);
        _ = await CreateEntityAsync(store, "Event09:00", timestamp: time2);
        _ = await CreateEntityAsync(store, "Event18:00", timestamp: time3);
        _ = await CreateEntityAsync(store, "Event06:00", timestamp: time4);

        var filter = Query.All();
        var sort = new SortParameter(new DateTimeField("timestamp"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Name.ShouldBe("Event06:00");
        result.Items[1].Value.Name.ShouldBe("Event09:00");
        result.Items[2].Value.Name.ShouldBe("Event14:00");
        result.Items[3].Value.Name.ShouldBe("Event18:00");
    }

    [Fact]
    public async Task QuerySortWithFilterShouldApplyBothCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Alice", rank: 80, category: "premium");
        _ = await CreateEntityAsync(store, "Bob", rank: 60, category: "basic");
        _ = await CreateEntityAsync(store, "Charlie", rank: 95, category: "premium");
        _ = await CreateEntityAsync(store, "David", rank: 70, category: "basic");
        _ = await CreateEntityAsync(store, "Eve", rank: 85, category: "premium");

        var filter = new StringField("category").Equals("premium");
        var sort = new SortParameter(new NumberField("rank"), SortDirection.Descending);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items[0].Value.Name.ShouldBe("Charlie"); // 95
        result.Items[1].Value.Name.ShouldBe("Eve");     // 85
        result.Items[2].Value.Name.ShouldBe("Alice");   // 80
    }

    [Fact]
    public async Task QuerySortByDateTimeWithRangeFilterShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var jan = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var mar = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var jun = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var sep = new DateTimeOffset(2024, 9, 15, 0, 0, 0, TimeSpan.Zero);
        var dec = new DateTimeOffset(2024, 12, 15, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateEntityAsync(store, "Event1", timestamp: jan);
        _ = await CreateEntityAsync(store, "Event2", timestamp: mar);
        _ = await CreateEntityAsync(store, "Event3", timestamp: jun);
        _ = await CreateEntityAsync(store, "Event4", timestamp: sep);
        _ = await CreateEntityAsync(store, "Event5", timestamp: dec);

        // Filter: events in second half of year (after June)
        var midYear = new DateTime(2024, 6, 30, 23, 59, 59, DateTimeKind.Utc);
        var filter = new DateTimeField("timestamp").GreaterThan(midYear);
        var sort = new SortParameter(new DateTimeField("timestamp"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items[0].Value.Name.ShouldBe("Event4"); // Sep (earlier)
        result.Items[1].Value.Name.ShouldBe("Event5"); // Dec (later)
    }

    [Fact]
    public async Task QuerySortWithPaginationShouldMaintainOrderAcrossPagesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", rank: i * 10);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act - Get first page
        var page1 = DataRange.FromPage(1, 3);
        var result1 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page1, Ct.None);

        // Act - Get second page
        var page2 = DataRange.FromPage(2, 3);
        var result2 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page2, Ct.None);

        // Act - Get third page
        var page3 = DataRange.FromPage(3, 3);
        var result3 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page3, Ct.None);

        // Assert
        result1.Items.Count.ShouldBe(3);
        result1.Items[0].Value.Rank.ShouldBe(100);
        result1.Items[1].Value.Rank.ShouldBe(90);
        result1.Items[2].Value.Rank.ShouldBe(80);

        result2.Items.Count.ShouldBe(3);
        result2.Items[0].Value.Rank.ShouldBe(70);
        result2.Items[1].Value.Rank.ShouldBe(60);
        result2.Items[2].Value.Rank.ShouldBe(50);

        result3.Items.Count.ShouldBe(3);
        result3.Items[0].Value.Rank.ShouldBe(40);
        result3.Items[1].Value.Rank.ShouldBe(30);
        result3.Items[2].Value.Rank.ShouldBe(20);
    }

    [Fact]
    public async Task QueryNoSortWithPaginationShouldReturnConsistentResultsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 5; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}");
        }

        var filter = Query.All();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(5);
        result.TotalCount.ShouldBe(5);
    }

    [Fact]
    public async Task QuerySortWithDuplicateValuesShouldReturnAllItemsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Alice", rank: 100);
        _ = await CreateEntityAsync(store, "Bob", rank: 100);
        _ = await CreateEntityAsync(store, "Charlie", rank: 100);
        _ = await CreateEntityAsync(store, "David", rank: 50);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), SortDirection.Descending);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        // First three should all have rank 100
        result.Items[0].Value.Rank.ShouldBe(100);
        result.Items[1].Value.Rank.ShouldBe(100);
        result.Items[2].Value.Rank.ShouldBe(100);
        // Last should have rank 50
        result.Items[3].Value.Rank.ShouldBe(50);
        result.Items[3].Value.Name.ShouldBe("David");
    }

    [Fact]
    public async Task QuerySortEmptyResultShouldReturnEmptyListAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Alice", rank: 100);
        _ = await CreateEntityAsync(store, "Bob", rank: 200);

        var filter = new NumberField("rank").GreaterThan(300);
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task QuerySortSingleItemShouldReturnThatItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "OnlyOne", rank: 42);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), SortDirection.Descending);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("OnlyOne");
        result.Items[0].Value.Rank.ShouldBe(42);
    }

    [Fact]
    public async Task QuerySortLargeDatasetShouldHandleEfficientlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 100 items with random-ish ranks
        for (var i = 1; i <= 100; i++)
        {
            var rank = (i * 7) % 100; // Creates a pseudo-random distribution
            _ = await CreateEntityAsync(store, $"Item{i:D3}", rank: rank);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 20);

        // Act
        var result = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(20);
        result.TotalCount.ShouldBe(100);

        // Verify ordering of first page
        for (var i = 0; i < result.Items.Count - 1; i++)
        {
            _ = result.Items[i].Value.Rank.ShouldNotBeNull();
            _ = result.Items[i + 1].Value.Rank.ShouldNotBeNull();
            result.Items[i].Value.Rank!.Value.ShouldBeLessThanOrEqualTo(result.Items[i + 1].Value.Rank!.Value);
        }
    }

    [Fact]
    public async Task QuerySortedPagingShouldPreserveOrderAcrossAllPagesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 17 items (to test partial last page)
        for (var i = 1; i <= 17; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", rank: i * 5);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act - Get all 4 pages (5+5+5+2)
        var page1 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 5), Ct.None);
        var page2 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 5), Ct.None);
        var page3 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 5), Ct.None);
        var page4 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(4, 5), Ct.None);

        // Assert metadata
        page1.TotalCount.ShouldBe(17);
        page1.HasMoreData.ShouldBeTrue();

        page2.HasMoreData.ShouldBeTrue();

        page3.HasMoreData.ShouldBeTrue();

        page4.Items.Count.ShouldBe(2); // Partial last page
        page4.HasMoreData.ShouldBeFalse();

        // Assert descending order across pages
        page1.Items[0].Value.Rank.ShouldBe(85); // Highest
        page1.Items[4].Value.Rank.ShouldBe(65);

        page2.Items[0].Value.Rank.ShouldBe(60);
        page2.Items[4].Value.Rank.ShouldBe(40);

        page3.Items[0].Value.Rank.ShouldBe(35);
        page3.Items[4].Value.Rank.ShouldBe(15);

        page4.Items[0].Value.Rank.ShouldBe(10);
        page4.Items[1].Value.Rank.ShouldBe(5); // Lowest
    }

    [Fact]
    public async Task QuerySortByDateTimeWithPagingShouldHandlePageBreaksCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 11 events (to test exact break: 4+4+3)
        for (var i = 1; i <= 11; i++)
        {
            var date = new DateTimeOffset(2024, 1, i, 10, 0, 0, TimeSpan.Zero);
            _ = await CreateEntityAsync(store, $"Event{i:D2}", timestamp: date);
        }

        var filter = Query.All();
        var sort = new SortParameter(new DateTimeField("timestamp"));

        // Act
        var page1 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 4), Ct.None);
        var page2 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 4), Ct.None);
        var page3 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 4), Ct.None);

        // Assert correct dates across pages
        page1.Items.Count.ShouldBe(4);
        page1.Items[0].Value.Name.ShouldBe("Event01");
        page1.Items[3].Value.Name.ShouldBe("Event04");

        page2.Items.Count.ShouldBe(4);
        page2.Items[0].Value.Name.ShouldBe("Event05");
        page2.Items[3].Value.Name.ShouldBe("Event08");

        page3.Items.Count.ShouldBe(3);
        page3.Items[0].Value.Name.ShouldBe("Event09");
        page3.Items[2].Value.Name.ShouldBe("Event11");
    }

    [Fact]
    public async Task QuerySortByStringWithPageBreakAtDuplicatesShouldHandleCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create items with duplicate names around page break
        _ = await CreateEntityAsync(store, "Alpha", rank: 1);
        _ = await CreateEntityAsync(store, "Alpha", rank: 2);
        _ = await CreateEntityAsync(store, "Alpha", rank: 3); // Page 1 ends here (page size 3)
        _ = await CreateEntityAsync(store, "Beta", rank: 4);  // Page 2 starts here
        _ = await CreateEntityAsync(store, "Beta", rank: 5);
        _ = await CreateEntityAsync(store, "Charlie", rank: 6);

        var filter = Query.All();
        var sort = new SortParameter(new StringField("name"));

        // Act
        var page1 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 3), Ct.None);
        var page2 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 3), Ct.None);

        // Assert - All Alphas should be on page 1, Betas and Charlie on page 2
        page1.Items.Count.ShouldBe(3);
        page1.Items.ShouldAllBe(x => x.Value.Name == "Alpha");

        page2.Items.Count.ShouldBe(3);
        page2.Items[0].Value.Name.ShouldBe("Beta");
        page2.Items[1].Value.Name.ShouldBe("Beta");
        page2.Items[2].Value.Name.ShouldBe("Charlie");
    }

    [Fact]
    public async Task QuerySortDescendingWithSmallPageSizeShouldReverseOrderAcrossPagesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 10; i <= 19; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}", rank: i);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act - Page size of 3 creates 4 pages (3+3+3+1)
        var page1 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 3), Ct.None);
        var page2 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 3), Ct.None);
        var page3 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 3), Ct.None);
        var page4 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(4, 3), Ct.None);

        // Assert - Should be in descending order
        page1.Items[0].Value.Rank.ShouldBe(19);
        page1.Items[2].Value.Rank.ShouldBe(17);

        page2.Items[0].Value.Rank.ShouldBe(16);
        page2.Items[2].Value.Rank.ShouldBe(14);

        page3.Items[0].Value.Rank.ShouldBe(13);
        page3.Items[2].Value.Rank.ShouldBe(11);

        page4.Items.Count.ShouldBe(1);
        page4.Items[0].Value.Rank.ShouldBe(10);
    }

    [Fact]
    public async Task QuerySortWithFilterPagingAcrossPageBreaksShouldMaintainOrderAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 30 items
        for (var i = 1; i <= 30; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}",
                rank: i,
                category: i % 2 == 0 ? "even" : "odd");
        }

        // Filter for even numbers (15 results)
        var filter = new StringField("category").Equals("even");
        var sort = new SortParameter(new NumberField("rank"));

        // Act - Page size 4 creates 4 pages (4+4+4+3)
        var page1 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 4), Ct.None);
        var page2 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 4), Ct.None);
        var page3 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 4), Ct.None);
        var page4 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(4, 4), Ct.None);

        // Assert
        page1.TotalCount.ShouldBe(15);
        page1.Items[0].Value.Rank.ShouldBe(2);
        page1.Items[3].Value.Rank.ShouldBe(8);

        page2.Items[0].Value.Rank.ShouldBe(10);
        page2.Items[3].Value.Rank.ShouldBe(16);

        page3.Items[0].Value.Rank.ShouldBe(18);
        page3.Items[3].Value.Rank.ShouldBe(24);

        page4.Items.Count.ShouldBe(3);
        page4.Items[0].Value.Rank.ShouldBe(26);
        page4.Items[2].Value.Rank.ShouldBe(30);
    }

    [Fact]
    public async Task QueryExactMultipleOfPageSizeShouldNotHaveExtraEmptyPageAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create exactly 15 items (exactly 3 pages of 5)
        for (var i = 1; i <= 15; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", rank: i);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act
        var page3 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 5), Ct.None);
        var page4 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(4, 5), Ct.None);

        // Assert
        page3.Items.Count.ShouldBe(5); // Full last page
        page3.HasMoreData.ShouldBeFalse();
        page3.Items[0].Value.Rank.ShouldBe(11);
        page3.Items[4].Value.Rank.ShouldBe(15);

        // Page 4 should be empty
        page4.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QuerySingleItemPerPageShouldIterateCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "First", rank: 1);
        _ = await CreateEntityAsync(store, "Second", rank: 2);
        _ = await CreateEntityAsync(store, "Third", rank: 3);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act - Page size of 1
        var page1 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 1), Ct.None);
        var page2 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 1), Ct.None);
        var page3 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 1), Ct.None);
        var page4 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(4, 1), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(1);
        page1.Items[0].Value.Name.ShouldBe("First");
        page1.HasMoreData.ShouldBeTrue();

        page2.Items.Count.ShouldBe(1);
        page2.Items[0].Value.Name.ShouldBe("Second");

        page3.Items.Count.ShouldBe(1);
        page3.Items[0].Value.Name.ShouldBe("Third");
        page3.HasMoreData.ShouldBeFalse();

        page4.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryLargePageSizeShouldFitAllOnOnePage()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}", rank: i);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act - Page size larger than total items
        var page1 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 100), Ct.None);
        var page2 = await store.QueryAsync<TestSortDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 100), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(10);
        page1.HasMoreData.ShouldBeFalse();

        page2.Items.Count.ShouldBe(0);
    }

}
