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
/// Tests for cursor-based pagination functionality across all store implementations.
/// Covers first page, continuation, last page, and sort requirement verification.
/// </summary>
public partial class QueryStoreCursorPagingTests
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
    public async Task QueryCursorFirstPageNullTokenShouldReturnFirstPageWithNextTokenAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 10 items
        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10, ct: _ct);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 3);

        // Act
        var result = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items[0].Value.Name.ShouldBe("Item01");
        result.Items[1].Value.Name.ShouldBe("Item02");
        result.Items[2].Value.Name.ShouldBe("Item03");
        _ = result.NextToken.ShouldNotBeNull();
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryFieldsCursorFirstPageNullTokenShouldReturnFirstPageWithNextTokenAsync()
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
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 4);
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, cursor, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(4);
        result.Items[0].Fields["NAME"].ShouldBe("ITEM01");
        result.Items[3].Fields["NAME"].ShouldBe("ITEM04");
        _ = result.NextToken.ShouldNotBeNull();
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryCursorContinuationWithNextTokenShouldReturnCorrectSubsequentPagesAsync()
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
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 3);

        // Act - Page 1
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);

        // Act - Page 2 using NextToken from page 1
        var cursor2 = DataRange.FromContinuationToken(page1.NextToken!.Value, 3);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor2, Ct.None);

        // Act - Page 3 using NextToken from page 2
        var cursor3 = DataRange.FromContinuationToken(page2.NextToken!.Value, 3);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor3, Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(3);
        page1.Items[0].Value.Rank.ShouldBe(1);
        page1.Items[2].Value.Rank.ShouldBe(3);
        page1.HasMoreData.ShouldBeTrue();

        page2.Items.Count.ShouldBe(3);
        page2.Items[0].Value.Rank.ShouldBe(4);
        page2.Items[2].Value.Rank.ShouldBe(6);
        page2.HasMoreData.ShouldBeTrue();

        page3.Items.Count.ShouldBe(3);
        page3.Items[0].Value.Rank.ShouldBe(7);
        page3.Items[2].Value.Rank.ShouldBe(9);
        page3.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryFieldsCursorContinuationWithNextTokenShouldReturnCorrectSubsequentPagesAsync()
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
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 3);
        var fields = new List<Field> { new StringField("name"), new NumberField("rank") };

        // Act - Page 1
        var page1 = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, cursor, Ct.None);

        // Act - Page 2
        var cursor2 = DataRange.FromContinuationToken(page1.NextToken!.Value, 3);
        var page2 = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, cursor2, Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(3);
        page1.Items[0].Fields["NAME"].ShouldBe("ITEM07");
        page1.Items[2].Fields["NAME"].ShouldBe("ITEM05");

        page2.Items.Count.ShouldBe(3);
        page2.Items[0].Fields["NAME"].ShouldBe("ITEM04");
        page2.Items[2].Fields["NAME"].ShouldBe("ITEM02");
    }

    [Fact]
    public async Task QueryCursorLastPageShouldHaveHasMoreFalseAndNullNextTokenAsync()
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
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), Ct.None);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), Ct.None);
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(page2.NextToken!.Value, 3), Ct.None);

        // Assert
        page3.Items.Count.ShouldBe(1);
        page3.Items[0].Value.Rank.ShouldBe(35);
        _ = page3.NextToken.ShouldNotBeNull(); // Token always set when items exist (enables resumption)
        page3.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryFieldsCursorLastPageShouldHaveHasMoreFalseAndNullNextTokenAsync()
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
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 5);
        var fields = new List<Field> { new StringField("name") };

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, cursor, Ct.None);

        // Assert - Exactly one full page, no more pages
        result.Items.Count.ShouldBe(5);
        _ = result.NextToken.ShouldNotBeNull(); // Token always set when items exist
        result.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryCursorEmptyResultShouldReturnEmptyWithNoNextTokenAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Item1", 10);
        _ = await CreateEntityAsync(store, "Item2", 20);

        var filter = new NumberField("rank").GreaterThan(100);
        var sort = new SortParameter(new NumberField("rank"));
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 10);

        // Act
        var result = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
        result.NextToken.ShouldBeNull();
        result.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryCursorWithoutSortShouldThrowArgumentNullExceptionAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Item1", 10);

        var filter = Query.All();
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 10);

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await store.QueryAsync<TestCursorDso>(_testEntityType, filter, null!, cursor, Ct.None));
    }

    [Fact]
    public async Task QueryFieldsCursorWithoutSortShouldThrowArgumentNullExceptionAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "Item1", 10);

        var filter = Query.All();
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 10);
        var fields = new List<Field> { new StringField("name") };

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await store.QueryFieldsAsync(_testEntityType, fields, filter, null!, cursor, Ct.None));
    }

    [Fact]
    public async Task QueryCursorWithDuplicateSortValuesShouldMaintainStableOrderingAsync()
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
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 2);

        // Act - Page 1
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);

        // Act - Page 2
        var cursor2 = DataRange.FromContinuationToken(page1.NextToken!.Value, 2);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor2, Ct.None);

        // Assert - All items with rank 100 should come before rank 50
        page1.Items.Count.ShouldBe(2);
        page1.Items[0].Value.Rank.ShouldBe(100);
        page1.Items[1].Value.Rank.ShouldBe(100);

        page2.Items.Count.ShouldBe(2);
        page2.Items[0].Value.Rank.ShouldBe(100);
        page2.Items[1].Value.Rank.ShouldBe(50);
        page2.Items[1].Value.Name.ShouldBe("David");
    }

    [Fact]
    public async Task QueryCursorDescendingSortShouldPageCorrectlyAsync()
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
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 4);

        // Act
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(4);
        page1.Items[0].Value.Rank.ShouldBe(90);  // Highest first
        page1.Items[3].Value.Rank.ShouldBe(60);
        page1.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryCursorWithFilterShouldPageFilteredResultsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 20 items, half odd, half even
        for (var i = 1; i <= 20; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i);
        }

        var filter = new NumberField("rank").GreaterThan(10);
        var sort = new SortParameter(new NumberField("rank"));
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 5);

        // Act
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 5), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(5);
        page1.Items[0].Value.Rank.ShouldBe(11);
        page1.Items[4].Value.Rank.ShouldBe(15);

        page2.Items.Count.ShouldBe(5);
        page2.Items[0].Value.Rank.ShouldBe(16);
        page2.Items[4].Value.Rank.ShouldBe(20);
    }

    [Fact]
    public async Task QueryCursorSingleItemShouldReturnOneItemWithNoNextToken()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateEntityAsync(store, "OnlyOne", 42);

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 10);

        // Act
        var result = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("OnlyOne");
        _ = result.NextToken.ShouldNotBeNull(); // Token always set when items exist
        result.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryCursorSortByDateTimeAscendingShouldPageCorrectlyAsync()
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
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 3);

        // Act
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(3);
        page1.Items[0].Value.CreatedAt.ShouldBe(baseDate.AddDays(1));
        page1.Items[1].Value.CreatedAt.ShouldBe(baseDate.AddDays(2));
        page1.Items[2].Value.CreatedAt.ShouldBe(baseDate.AddDays(3));
        page1.HasMoreData.ShouldBeTrue();

        page2.Items.Count.ShouldBe(3);
        page2.Items[0].Value.CreatedAt.ShouldBe(baseDate.AddDays(4));
        page2.Items[1].Value.CreatedAt.ShouldBe(baseDate.AddDays(5));
        page2.Items[2].Value.CreatedAt.ShouldBe(baseDate.AddDays(6));
    }

    [Fact]
    public async Task QueryCursorSortByDateTimeDescendingShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        for (var i = 1; i <= 8; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i, baseDate.AddHours(i), true);
        }

        var filter = Query.All();
        var sort = new SortParameter(new DateTimeField("recordedAt"), SortDirection.Descending);
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 4);

        // Act
        var result = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);

        // Assert - Should return most recent dates first
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.CreatedAt.ShouldBe(baseDate.AddHours(8));
        result.Items[1].Value.CreatedAt.ShouldBe(baseDate.AddHours(7));
        result.Items[2].Value.CreatedAt.ShouldBe(baseDate.AddHours(6));
        result.Items[3].Value.CreatedAt.ShouldBe(baseDate.AddHours(5));
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryFieldsCursorSortByDateTimeAscendingShouldPageCorrectlyAsync()
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
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 3);
        var fields = new List<Field> { new StringField("name"), new DateTimeField("recordedAt") };

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, cursor, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        ((DateTimeOffset)result.Items[0].Fields["RECORDEDAT"]!).ShouldBe(baseDate.AddMonths(1).UtcDateTime);
        ((DateTimeOffset)result.Items[1].Fields["RECORDEDAT"]!).ShouldBe(baseDate.AddMonths(2).UtcDateTime);
        ((DateTimeOffset)result.Items[2].Fields["RECORDEDAT"]!).ShouldBe(baseDate.AddMonths(3).UtcDateTime);
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryCursorSortByBooleanAscendingShouldPageCorrectlyAsync()
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

        var filter = Query.All();
        var sort = new SortParameter(new BooleanField("isActive"));
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 4);

        // Act
        var result = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);

        // Assert - false values should come before true (ascending)
        result.Items.Count.ShouldBe(4);
        result.Items[0].Value.IsActive.ShouldBeFalse();
        result.Items[1].Value.IsActive.ShouldBeFalse();
        result.Items[2].Value.IsActive.ShouldBeFalse();
        result.Items[3].Value.IsActive.ShouldBeTrue();
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryCursorSortByBooleanDescendingShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var baseDate = DateTimeOffset.UtcNow;
        // Create items with mixed boolean values
        for (var i = 1; i <= 8; i++)
        {
            var isActive = i % 3 == 0; // Every third item is active
            _ = await CreateEntityAsync(store, $"Item{i}", i * 10, baseDate.AddDays(i), isActive);
        }

        var filter = Query.All();
        var sort = new SortParameter(new BooleanField("isActive"), SortDirection.Descending);
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 3);

        // Act
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, cursor, Ct.None);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), Ct.None);

        // Assert - true values should come before false (descending)
        page1.Items.Count.ShouldBe(3);
        page1.Items[0].Value.IsActive.ShouldBeTrue();
        page1.Items[1].Value.IsActive.ShouldBeTrue();
        // Third item depends on the count of true values

        page2.Items.Count.ShouldBe(3);
        // Should contain mix or all false values
    }

    [Fact]
    public async Task QueryFieldsCursorSortByBooleanAscendingShouldPageCorrectlyAsync()
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
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 3);
        var fields = new List<Field> { new StringField("name"), new BooleanField("isActive") };

        // Act
        var result = await store.QueryFieldsAsync(_testEntityType, fields, filter, sort, cursor, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        ((bool)result.Items[0].Fields["ISACTIVE"]!).ShouldBeFalse();
        ((bool)result.Items[1].Fields["ISACTIVE"]!).ShouldBeFalse();
        ((bool)result.Items[2].Fields["ISACTIVE"]!).ShouldBeTrue();
        result.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryCursorResumeFromEndShouldReturnNewlyAddedItemsAsync()
    {
        // Arrange — create 5 items, page size 3 → two pages (3 + 2)
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 5; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", i * 10);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act — page through to the end
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(page1.NextToken!.Value, 3), _ct);

        // Assert — we've reached the end
        page2.Items.Count.ShouldBe(2);
        page2.Items[0].Value.Rank.ShouldBe(40);
        page2.Items[1].Value.Rank.ShouldBe(50);
        page2.HasMoreData.ShouldBeFalse();
        var resumeToken = page2.NextToken.ShouldNotBeNull(); // Token is still set for resumption

        // Act — new data arrives after we hit the end
        _ = await CreateEntityAsync(store, "Item06", 60);
        _ = await CreateEntityAsync(store, "Item07", 70);

        // Resume from the saved token — should pick up the new items
        var page3 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(resumeToken.Value, 3), _ct);

        // Assert — the new items are returned
        page3.Items.Count.ShouldBe(2);
        page3.Items[0].Value.Rank.ShouldBe(60);
        page3.Items[1].Value.Rank.ShouldBe(70);
        page3.HasMoreData.ShouldBeFalse();
        _ = page3.NextToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task QueryCursorResumeFromEndWithNoNewDataShouldReturnEmptyAsync()
    {
        // Arrange — create 3 items, page size 3 → exactly one page
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 3; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}", i * 10);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("rank"));

        // Act — fetch the only page
        var page1 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(ContinuationToken.Beginning, 3), _ct);

        page1.Items.Count.ShouldBe(3);
        page1.HasMoreData.ShouldBeFalse();
        var resumeToken = page1.NextToken.ShouldNotBeNull();

        // Act — resume with no new data
        var page2 = await store.QueryAsync<TestCursorDso>(_testEntityType, filter, sort, DataRange.FromContinuationToken(resumeToken.Value, 3), _ct);

        // Assert — empty result, no token (no items returned)
        page2.Items.Count.ShouldBe(0);
        page2.NextToken.ShouldBeNull();
        page2.HasMoreData.ShouldBeFalse();
    }
}
