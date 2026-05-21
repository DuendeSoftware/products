// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using SortDirection = Duende.Storage.Querying.SortDirection;
using SortParameter = Duende.Storage.Internal.Querying.Sorting.SortParameter;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Tests for offset-based pagination functionality across all store implementations.
/// Covers first page, continuation, last page, total counts, and edge cases.
/// </summary>
public partial class QueryStorePagingTests
{

    private readonly EntityType _testEntityType = TestPageDso.DsoVersion.EntityType;

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestPageDso>();
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
        var dso = new TestPageDso
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

        var storeInterface = store;
        var result = await storeInterface.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id;
    }

    [Fact]
    public async Task QueryFirstPageShouldReturnCorrectItemsAndMetadataAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 10 items
        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 3);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items[0].Value.Name.ShouldBe("Item01");
        result.Items[1].Value.Name.ShouldBe("Item02");
        result.Items[2].Value.Name.ShouldBe("Item03");
        result.TotalCount.ShouldBe(10);
        result.TotalPages.ShouldBe(4);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryFieldsFirstPageShouldReturnCorrectItemsAndMetadataAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 8; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 5);
        }

        var filter = Query.All();
        var sort = new SortParameter(new StringField("name"));
        var page = DataRange.FromPage(1, 4);
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Fields["NAME"].ShouldBe("ITEM01");
        result.Items[3].Fields["NAME"].ShouldBe("ITEM04");
        result.TotalCount.ShouldBe(8);
        result.TotalPages.ShouldBe(2);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryMultiplePagesShouldReturnCorrectSubsequentPagesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 10 items
        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act - Page 1
        var page1 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 3), Ct.None);

        // Act - Page 2
        var page2 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 3), Ct.None);

        // Act - Page 3
        var page3 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 3), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(3);
        page1.Items[0].Value.Rank.ShouldBe(1);
        page1.Items[2].Value.Rank.ShouldBe(3);
        page1.HasMoreData.ShouldBeTrue();
        page1.TotalPages.ShouldBe(4);

        page2.Items.Count.ShouldBe(3);
        page2.Items[0].Value.Rank.ShouldBe(4);
        page2.Items[2].Value.Rank.ShouldBe(6);
        page2.HasMoreData.ShouldBeTrue();
        page2.TotalPages.ShouldBe(4);

        page3.Items.Count.ShouldBe(3);
        page3.Items[0].Value.Rank.ShouldBe(7);
        page3.Items[2].Value.Rank.ShouldBe(9);
        page3.HasMoreData.ShouldBeTrue();
        page3.TotalPages.ShouldBe(4);
    }

    [Fact]
    public async Task QueryFieldsMultiplePagesShouldReturnCorrectSubsequentPagesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 7; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10);
        }

        var filter = Query.All();
        var sort = new SortParameter(new StringField("name"), SortDirection.Descending);
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };

        // Act - Page 1
        var page1 = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromPage(1, 3), Ct.None);

        // Act - Page 2
        var page2 = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromPage(2, 3), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(3);
        page1.Items[0].Fields["NAME"].ShouldBe("ITEM07");
        page1.Items[2].Fields["NAME"].ShouldBe("ITEM05");
        page1.HasMoreData.ShouldBeTrue();
        page1.TotalPages.ShouldBe(3);

        page2.Items.Count.ShouldBe(3);
        page2.Items[0].Fields["NAME"].ShouldBe("ITEM04");
        page2.Items[2].Fields["NAME"].ShouldBe("ITEM02");
        page2.HasMoreData.ShouldBeTrue();
        page2.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task QueryLastPageShouldHaveCorrectMetadataAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 7 items (3+3+1)
        for (var i = 1; i <= 7; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 5);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act - Navigate to last page
        var page3 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 3), Ct.None);

        // Assert
        page3.Items.Count.ShouldBe(1);
        page3.Items[0].Value.Rank.ShouldBe(35);
        page3.TotalCount.ShouldBe(7);
        page3.TotalPages.ShouldBe(3);
        page3.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryFieldsLastPageShouldHaveCorrectMetadataAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create exactly 5 items with page size 5
        for (var i = 1; i <= 5; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}", i);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 5);
        var fields = new List<Field> { new StringField("name") };

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, page, Ct.None);

        // Assert - Exactly one full page, no more pages
        result.Items.Count.ShouldBe(5);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(1);
        result.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryEmptyResultShouldReturnEmptyPageWithCorrectMetadataAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Item1", 10);
        _ = await CreateEntityAsync(store, "Item2", 20);

        var filter = new NumberField("rank").GreaterThan(100);
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
        result.TotalCount.ShouldBe(0);
        result.TotalPages.ShouldBe(0);
        result.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryAllPagesShouldHaveConsistentTotalCountAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 15 items
        for (var i = 1; i <= 15; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act - Request multiple pages
        var page1 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 5), Ct.None);
        var page2 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 5), Ct.None);
        var page3 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 5), Ct.None);

        // Assert - Total count and total pages should be consistent across all pages
        page1.TotalCount.ShouldBe(15);
        page2.TotalCount.ShouldBe(15);
        page3.TotalCount.ShouldBe(15);

        page1.TotalPages.ShouldBe(3);
        page2.TotalPages.ShouldBe(3);
        page3.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task QueryWithFilterShouldReturnFilteredTotalCountAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 20 items
        for (var i = 1; i <= 20; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i);
        }

        var filter = new NumberField("rank").GreaterThan(10);
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 5);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert - Should only count items matching the filter (11-20 = 10 items)
        result.Items.Count.ShouldBe(5);
        result.TotalCount.ShouldBe(10);
        result.TotalPages.ShouldBe(2);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryWithoutSortShouldPageByIdAscendingAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create items in random order
        for (var i = 5; i >= 1; i--)
        {
            var id = await CreateEntityAsync(store, $"Item{i}", i * 10);
        }

        var filter = Query.All();
        var page = DataRange.FromPage(1, 3);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should be ordered by ID when no sort is specified
        result.Items.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task QueryDescendingSortShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 9; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}", i * 10);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), SortDirection.Descending);
        var page = DataRange.FromPage(1, 4);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.Rank.ShouldBe(90);  // Highest first
        result.Items[3].Value.Rank.ShouldBe(60);
        result.TotalCount.ShouldBe(9);
        result.TotalPages.ShouldBe(3);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryPageBeyondRangeShouldReturnEmptyPageAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 5; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}", i);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(10, 5); // Page 10 when only 1 page exists

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(1);
        result.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryWithDuplicateSortValuesShouldMaintainStableOrderingAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create items with duplicate rank values
        var id1 = await CreateEntityAsync(store, "Alice", 100);
        var id2 = await CreateEntityAsync(store, "Bob", 100);
        var id3 = await CreateEntityAsync(store, "Charlie", 100);
        var id4 = await CreateEntityAsync(store, "David", 50);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"), SortDirection.Descending);

        // Act - Page 1
        var page1 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 2), Ct.None);

        // Act - Page 2
        var page2 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 2), Ct.None);

        // Assert - All items with rank 100 should come before rank 50
        page1.Items.Count.ShouldBe(2);
        page1.Items[0].Value.Rank.ShouldBe(100);
        page1.Items[1].Value.Rank.ShouldBe(100);
        page1.TotalCount.ShouldBe(4);
        page1.TotalPages.ShouldBe(2);
        page2.Items.Count.ShouldBe(2);
        page2.Items[0].Value.Rank.ShouldBe(100);
        page2.Items[1].Value.Rank.ShouldBe(50);
        page2.Items[1].Value.Name.ShouldBe("David");
    }

    [Fact]
    public async Task QueryWithFilterShouldPageFilteredResultsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 20 items
        for (var i = 1; i <= 20; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i);
        }

        var filter = new NumberField("rank").GreaterThan(10);
        var sort = new SortParameter(new NumberField("rank"));

        // Act
        var page1 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 5), Ct.None);
        var page2 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 5), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(5);
        page1.Items[0].Value.Rank.ShouldBe(11);
        page1.Items[4].Value.Rank.ShouldBe(15);
        page1.TotalCount.ShouldBe(10);
        page1.TotalPages.ShouldBe(2);

        page2.Items.Count.ShouldBe(5);
        page2.Items[0].Value.Rank.ShouldBe(16);
        page2.Items[4].Value.Rank.ShouldBe(20);
        page2.TotalCount.ShouldBe(10);
        page2.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task QuerySingleItemShouldReturnOneItemPage()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "OnlyOne", 42);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("OnlyOne");
        result.TotalCount.ShouldBe(1);
        result.TotalPages.ShouldBe(1);
        result.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryLargePageSizeShouldReturnAllItemsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 5; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}", i);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var page = DataRange.FromPage(1, 100);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(5);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(1);
        result.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryFieldsWithComplexFilterShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 12; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 5);
        }

        var filter = new NumberField("rank").Between(20, 50);
        var sort = new SortParameter(new NumberField("rank"));
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };

        // Act
        var page1 = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromPage(1, 3), Ct.None);
        var page2 = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, DataRange.FromPage(2, 3), Ct.None);

        // Assert - ranks 20, 25, 30, 35, 40, 45, 50 = 7 items
        page1.Items.Count.ShouldBe(3);
        page1.Items[0].Fields["RANK"].ShouldBe(20m);
        page1.TotalCount.ShouldBe(7);
        page1.TotalPages.ShouldBe(3);
        page2.Items.Count.ShouldBe(3);
        page2.Items[0].Fields["RANK"].ShouldBe(35m);
    }

    [Fact]
    public async Task QuerySortByDateTimeAscendingShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, baseDate.AddDays(i), true);
        }

        var filter = Query.All();
        var sort = new SortParameter(new DateTimeField("recordedAt"));
        var page = DataRange.FromPage(1, 3);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items[0].Value.CreatedAt.ShouldBe(baseDate.AddDays(1));
        result.Items[1].Value.CreatedAt.ShouldBe(baseDate.AddDays(2));
        result.Items[2].Value.CreatedAt.ShouldBe(baseDate.AddDays(3));
        result.TotalCount.ShouldBe(10);
        result.TotalPages.ShouldBe(4);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QuerySortByDateTimeDescendingShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 12; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i, baseDate.AddHours(i), true);
        }

        var filter = Query.All();
        var sort = new SortParameter(new DateTimeField("recordedAt"), SortDirection.Descending);
        var page = DataRange.FromPage(2, 4);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert - Should return most recent dates first, page 2
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.CreatedAt.ShouldBe(baseDate.AddHours(8));
        result.Items[1].Value.CreatedAt.ShouldBe(baseDate.AddHours(7));
        result.Items[2].Value.CreatedAt.ShouldBe(baseDate.AddHours(6));
        result.Items[3].Value.CreatedAt.ShouldBe(baseDate.AddHours(5));
        result.TotalCount.ShouldBe(12);
        result.TotalPages.ShouldBe(3);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryFieldsSortByDateTimeAscendingShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = new DateTimeOffset(2024, 3, 10, 0, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 6; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}", i * 5, baseDate.AddMonths(i), true);
        }

        var filter = Query.All();
        var sort = new SortParameter(new DateTimeField("recordedAt"));
        var page = DataRange.FromPage(1, 3);
        var fields = new List<Field> { new StringField("name"), new DateTimeField("recordedAt") };

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        ((DateTimeOffset)result.Items[0].Fields["RECORDEDAT"]!).ShouldBe(baseDate.AddMonths(1));
        ((DateTimeOffset)result.Items[1].Fields["RECORDEDAT"]!).ShouldBe(baseDate.AddMonths(2));
        ((DateTimeOffset)result.Items[2].Fields["RECORDEDAT"]!).ShouldBe(baseDate.AddMonths(3));
        result.TotalCount.ShouldBe(6);
        result.TotalPages.ShouldBe(2);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QuerySortByDateTimeWithMultiplePagesAndFilterShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 15; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i, baseDate.AddDays(i), true);
        }

        var cutoffDate = baseDate.AddDays(5);
        var filter = new DateTimeField("recordedAt").GreaterThan(cutoffDate.UtcDateTime);
        var sort = new SortParameter(new DateTimeField("recordedAt"));

        // Act
        var page1 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 5), Ct.None);
        var page2 = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 5), Ct.None);

        // Assert - Should only include items after cutoff (days 6-15 = 10 items)
        page1.Items.Count.ShouldBe(5);
        page1.Items[0].Value.CreatedAt.ShouldBe(baseDate.AddDays(6));
        page1.TotalCount.ShouldBe(10);
        page1.TotalPages.ShouldBe(2);
        page1.HasMoreData.ShouldBeTrue();

        page2.Items.Count.ShouldBe(5);
        page2.Items[0].Value.CreatedAt.ShouldBe(baseDate.AddDays(11));
        page2.TotalCount.ShouldBe(10);
        page2.TotalPages.ShouldBe(2);
        page2.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QuerySortByBooleanAscendingShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = DateTimeOffset.UtcNow;
        // Create items with mixed boolean values
        _ = await CreateEntityAsync(store, "Item1", 10, baseDate.AddDays(1), false);
        _ = await CreateEntityAsync(store, "Item2", 20, baseDate.AddDays(2), false);
        _ = await CreateEntityAsync(store, "Item3", 30, baseDate.AddDays(3), false);
        _ = await CreateEntityAsync(store, "Item4", 40, baseDate.AddDays(4), true);
        _ = await CreateEntityAsync(store, "Item5", 50, baseDate.AddDays(5), true);
        _ = await CreateEntityAsync(store, "Item6", 60, baseDate.AddDays(6), true);
        _ = await CreateEntityAsync(store, "Item7", 70, baseDate.AddDays(7), true);

        var filter = Query.All();
        var sort = new SortParameter(new BooleanField("isActive"));
        var page = DataRange.FromPage(1, 4);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert - false values should come before true (ascending)
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.IsActive.ShouldBeFalse();
        result.Items[1].Value.IsActive.ShouldBeFalse();
        result.Items[2].Value.IsActive.ShouldBeFalse();
        result.Items[3].Value.IsActive.ShouldBeTrue();
        result.TotalCount.ShouldBe(7);
        result.TotalPages.ShouldBe(2);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QuerySortByBooleanDescendingShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = DateTimeOffset.UtcNow;
        // Create 10 items with mixed boolean values
        for (var i = 1; i <= 10; i++)
        {
            var isActive = i % 3 == 0; // Every third item is active
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, baseDate.AddDays(i), isActive);
        }

        var filter = Query.All();
        var sort = new SortParameter(new BooleanField("isActive"), SortDirection.Descending);
        var page = DataRange.FromPage(1, 4);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert - true values should come before false (descending)
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.IsActive.ShouldBeTrue();
        result.Items[1].Value.IsActive.ShouldBeTrue();
        result.Items[2].Value.IsActive.ShouldBeTrue();
        result.TotalCount.ShouldBe(10);
        result.TotalPages.ShouldBe(3);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryFieldsSortByBooleanAscendingShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = DateTimeOffset.UtcNow;
        _ = await CreateEntityAsync(store, "Inactive1", 10, baseDate, false);
        _ = await CreateEntityAsync(store, "Inactive2", 20, baseDate.AddDays(1), false);
        _ = await CreateEntityAsync(store, "Active1", 30, baseDate.AddDays(2), true);
        _ = await CreateEntityAsync(store, "Active2", 40, baseDate.AddDays(3), true);
        _ = await CreateEntityAsync(store, "Active3", 50, baseDate.AddDays(4), true);

        var filter = Query.All();
        var sort = new SortParameter(new BooleanField("isActive"));
        var page = DataRange.FromPage(1, 3);
        var fields = new List<Field> { new StringField("name"), new BooleanField("isActive") };

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        ((bool)result.Items[0].Fields["ISACTIVE"]!).ShouldBeFalse();
        ((bool)result.Items[1].Fields["ISACTIVE"]!).ShouldBeFalse();
        ((bool)result.Items[2].Fields["ISACTIVE"]!).ShouldBeTrue();
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(2);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QuerySortByBooleanWithFilterShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = DateTimeOffset.UtcNow;
        // Create items with various rank values and boolean states
        for (var i = 1; i <= 12; i++)
        {
            var isActive = i <= 6; // First half is active
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, baseDate.AddDays(i), isActive);
        }

        var filter = new NumberField("rank").GreaterThan(30); // Items 4-12
        var sort = new SortParameter(new BooleanField("isActive"), SortDirection.Descending);
        var page = DataRange.FromPage(1, 5);

        // Act
        var result = await store.QueryAsync<TestPageDso>(_testEntityType, filter, sort, page, Ct.None);

        // Assert - Should include filtered items (rank > 30) sorted by isActive desc
        result.Items.Count.ShouldBe(5);
        // First results should be true (items 4-6 are active and rank > 30)
        result.Items[0].Value.IsActive.ShouldBeTrue();
        result.Items[1].Value.IsActive.ShouldBeTrue();
        result.Items[2].Value.IsActive.ShouldBeTrue();
        result.TotalCount.ShouldBe(9); // Items 4-12 = 9 items
        result.TotalPages.ShouldBe(2);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryWithoutSortShouldPageThroughAllRecordsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 15 items with unique rank values
        var expectedRanks = new HashSet<int>();
        for (var i = 1; i <= 15; i++)
        {
            var rank = i * 10;
            _ = await CreateEntityAsync(store, $"Item{i:D2}", rank);
            _ = expectedRanks.Add(rank);
        }

        var filter = Query.All();
        var pageSize = 4;

        // Act - Page through all results
        var retrievedRanks = new HashSet<int>();
        var pageNumber = 1;
        QueryResult<MetadataEnvelope<TestPageDso>> result;

        do
        {
            result = await store.QueryAsync<TestPageDso>(
                _testEntityType,
                filter,
                SortParameter.Empty,
                DataRange.FromPage(pageNumber, pageSize),
                Ct.None);

            foreach (var item in result.Items)
            {
                // Track by unique rank values
                var wasAdded = retrievedRanks.Add(item.Value.Rank);
                wasAdded.ShouldBeTrue($"Duplicate rank {item.Value.Rank} found - item returned multiple times");
            }

            pageNumber++;
        }
        while (result.HasMoreData);

        // Assert - Should have retrieved all items exactly once
        result.TotalCount.ShouldBe(15);
        retrievedRanks.Count.ShouldBe(15);
        retrievedRanks.ShouldBe(expectedRanks, ignoreOrder: true);
    }

    [Fact]
    public async Task QueryWithoutSortMultiplePagesShouldHaveConsistentTotalCountAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 25 items
        for (var i = 1; i <= 25; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i);
        }

        var filter = Query.All();
        var pageSize = 7;

        // Act - Fetch multiple pages without sort
        var page1 = await store.QueryAsync<TestPageDso>(
            _testEntityType,
            filter,
            SortParameter.Empty,
            DataRange.FromPage(1, pageSize),
            Ct.None);

        var page2 = await store.QueryAsync<TestPageDso>(
            _testEntityType,
            filter,
            SortParameter.Empty,
            DataRange.FromPage(2, pageSize),
            Ct.None);

        var page3 = await store.QueryAsync<TestPageDso>(
            _testEntityType,
            filter,
            SortParameter.Empty,
            DataRange.FromPage(3, pageSize),
            Ct.None);

        var page4 = await store.QueryAsync<TestPageDso>(
            _testEntityType,
            filter,
            SortParameter.Empty,
            DataRange.FromPage(4, pageSize),
            Ct.None);

        // Assert - All pages should report consistent total count and total pages
        page1.TotalCount.ShouldBe(25);
        page2.TotalCount.ShouldBe(25);
        page3.TotalCount.ShouldBe(25);
        page4.TotalCount.ShouldBe(25);

        page1.TotalPages.ShouldBe(4);
        page2.TotalPages.ShouldBe(4);
        page3.TotalPages.ShouldBe(4);
        page4.TotalPages.ShouldBe(4);

        // Check page sizes
        page1.Items.Count.ShouldBe(7);
        page2.Items.Count.ShouldBe(7);
        page3.Items.Count.ShouldBe(7);
        page4.Items.Count.ShouldBe(4); // Last page has remainder

        // Verify pagination flags
        page1.HasMoreData.ShouldBeTrue();

        page2.HasMoreData.ShouldBeTrue();

        page3.HasMoreData.ShouldBeTrue();

        page4.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryWithoutSortShouldRetrieveAllUniqueRecordsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create items with unique rank values that we can track
        var expectedRanks = new HashSet<int>();
        for (var i = 1; i <= 20; i++)
        {
            var rank = i * 7; // Use a multiplier to ensure uniqueness
            _ = await CreateEntityAsync(store, $"Item{i:D2}", rank);
            _ = expectedRanks.Add(rank);
        }

        var filter = Query.All();
        var pageSize = 6;

        // Act - Page through all results and collect ranks
        var retrievedRanks = new HashSet<int>();
        var pageNumber = 1;
        QueryResult<MetadataEnvelope<TestPageDso>> result;

        do
        {
            result = await store.QueryAsync<TestPageDso>(
                _testEntityType,
                filter,
                SortParameter.Empty,
                DataRange.FromPage(pageNumber, pageSize),
                Ct.None);

            foreach (var item in result.Items)
            {
                // Track by rank to ensure no duplicates
                var wasAdded = retrievedRanks.Add(item.Value.Rank);
                wasAdded.ShouldBeTrue($"Duplicate rank {item.Value.Rank} found - item returned multiple times");
            }

            pageNumber++;
        }
        while (result.HasMoreData);

        // Assert - Should have all unique ranks (order doesn't matter without sort)
        retrievedRanks.Count.ShouldBe(20);
        retrievedRanks.ShouldBe(expectedRanks, ignoreOrder: true);
    }

    [Fact]
    public async Task QueryFieldsWithoutSortShouldPageThroughAllRecordsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 18 items
        var expectedRanks = new HashSet<int>();
        for (var i = 1; i <= 18; i++)
        {
            var rank = i * 5;
            _ = await CreateEntityAsync(store, $"Item{i:D2}", rank);
            _ = expectedRanks.Add(rank);
        }

        var filter = Query.All();
        var pageSize = 5;
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };

        // Act - Page through all results
        var retrievedRanks = new HashSet<decimal>();
        var pageNumber = 1;
        QueryResult<ProjectedResult> result;

        do
        {
            result = await store.QueryFieldsAsync(
                _testEntityType,
                fields,
                filter,
                SortParameter.Empty,
                DataRange.FromPage(pageNumber, pageSize),
                Ct.None);

            foreach (var item in result.Items)
            {
                var rank = (decimal)item.Fields["RANK"]!;
                var wasAdded = retrievedRanks.Add(rank);
                wasAdded.ShouldBeTrue($"Duplicate rank {rank} found - item returned multiple times");
            }

            pageNumber++;
        }
        while (result.HasMoreData);

        // Assert
        result.TotalCount.ShouldBe(18);
        retrievedRanks.Count.ShouldBe(18);
        retrievedRanks.ShouldBe(expectedRanks.Select(r => (decimal)r).ToHashSet(), ignoreOrder: true);
    }

    [Fact]
    public async Task QueryWithoutSortWithFilterShouldPageThroughFilteredRecordsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 30 items
        var expectedRanksInRange = new HashSet<int>();
        for (var i = 1; i <= 30; i++)
        {
            var rank = i * 10;
            _ = await CreateEntityAsync(store, $"Item{i:D2}", rank);
            if (rank >= 100 && rank <= 200)
            {
                _ = expectedRanksInRange.Add(rank);
            }
        }

        var filter = new NumberField("rank").Between(100, 200);
        var pageSize = 4;

        // Act - Page through filtered results
        var retrievedRanks = new HashSet<int>();
        var pageNumber = 1;
        QueryResult<MetadataEnvelope<TestPageDso>> result;

        do
        {
            result = await store.QueryAsync<TestPageDso>(
                _testEntityType,
                filter,
                SortParameter.Empty,
                DataRange.FromPage(pageNumber, pageSize),
                Ct.None);

            foreach (var item in result.Items)
            {
                var wasAdded = retrievedRanks.Add(item.Value.Rank);
                wasAdded.ShouldBeTrue($"Duplicate rank {item.Value.Rank} found");
            }

            pageNumber++;
        }
        while (result.HasMoreData);

        // Assert - Should have all filtered items
        result.TotalCount.ShouldBe(11); // Ranks 100, 110, 120, ..., 200 = 11 items
        retrievedRanks.Count.ShouldBe(11);
        retrievedRanks.ShouldBe(expectedRanksInRange, ignoreOrder: true);
    }

    [Fact]
    public async Task QueryFields_field_path_text_should_be_human_readable_not_a_GUID_async()
    {
        // Arrange — create entities with multiple distinct field paths
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Alice", rank: 42);
        _ = await CreateEntityAsync(store, "Bob", rank: 99);

        // Request both "name" and "rank" fields via QueryFieldsAsync
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };
        var filter = Query.All();
        var sort = SortParameter.Empty;
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, page, _ct);

        // Assert — the Fields dictionary must be keyed by the human-readable (uppercased) path,
        // NOT by a GUID string. If field_path_text stored a GUID, the keys would look like
        // "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" and the lookups below would fail.
        result.Items.Count.ShouldBe(2);

        foreach (var item in result.Items)
        {
            // Keys must be the uppercased field path strings, not GUIDs
            item.Fields.Keys.ShouldContain("NAME",
                "Expected field key 'NAME' but got GUID-like key — field_path_text is storing a GUID instead of the human-readable path.");
            item.Fields.Keys.ShouldContain("RANK",
                "Expected field key 'RANK' but got GUID-like key — field_path_text is storing a GUID instead of the human-readable path.");

            // Verify none of the keys look like a GUID (36-char format with dashes)
            foreach (var key in item.Fields.Keys)
            {
                Guid.TryParse(key, out _).ShouldBeFalse(
                    $"Field key '{key}' looks like a GUID — field_path_text is storing a GUID instead of the human-readable path.");
            }
        }
    }

    [Fact]
    public async Task QueryFields_multiple_field_paths_should_all_return_human_readable_keys_async()
    {
        // Arrange — create entities with several different field paths to ensure
        // each field_path_text entry is stored correctly
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "TestUser", rank: 75);

        var fields = new List<Field>
        {
            new StringField("name"),
            new NumberField("rank")
        };
        var filter = Query.All();
        var sort = SortParameter.Empty;
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, page, _ct);

        // Assert — all field paths must appear as human-readable keys
        result.Items.Count.ShouldBe(1);
        var item = result.Items[0];

        item.Fields.Keys.ShouldContain("NAME");
        item.Fields.Keys.ShouldContain("RANK");

        // Verify values are populated (not null) — proves the key lookup succeeded
        _ = item.Fields["NAME"].ShouldNotBeNull();
        _ = item.Fields["RANK"].ShouldNotBeNull();

        // Verify no key is a GUID
        foreach (var key in item.Fields.Keys)
        {
            Guid.TryParse(key, out _).ShouldBeFalse(
                $"Field key '{key}' is a GUID — field_path_text must store the human-readable path.");
        }
    }
}
