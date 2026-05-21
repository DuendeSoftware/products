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
using SortParameter = Duende.Storage.Internal.Querying.Sorting.SortParameter;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Tests for basic query expressions across all store implementations.
/// Focuses on testing various field types and operations, with special emphasis on range queries
/// for datetime and number fields which are critical for filtering data.
/// </summary>
public partial class QueryStoreBasicExpressionTests
{


    private readonly EntityType _testEntityType = new(3, "TestEntity");

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
        int? score = null,
        decimal? price = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? lastLogin = null,
        bool? isActive = null,
        string? status = null,
        Ct ct = default) => await CreateTestEntityAsync(store, name, score, price, createdAt, lastLogin, isActive, status, ct);

    private static async Task<UuidV7> CreateTestEntityAsync(
        IStore store,
        string name,
        int? score = null,
        decimal? price = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? lastLogin = null,
        bool? isActive = null,
        string? status = null,
        Ct ct = default)
    {
        var id = UuidV7.New();
        var dso = new TestEntityDso
        {
            Name = name,
            Score = score,
            Price = price,
            CreatedAt = createdAt,
            LastLogin = lastLogin,
            IsActive = isActive,
            Status = status
        };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);
        if (score.HasValue)
        {
            _ = searchFieldsBuilder.Add("score", score.Value);
        }

        if (price.HasValue)
        {
            _ = searchFieldsBuilder.Add("price", price.Value);
        }

        if (createdAt.HasValue)
        {
            _ = searchFieldsBuilder.Add("recordedAt", createdAt.Value);
        }

        if (lastLogin.HasValue)
        {
            _ = searchFieldsBuilder.Add("lastLogin", lastLogin.Value);
        }

        if (isActive.HasValue)
        {
            _ = searchFieldsBuilder.Add("isActive", isActive.Value);
        }

        if (status != null)
        {
            _ = searchFieldsBuilder.Add("status", status);
        }

        var searchFields = searchFieldsBuilder.Build();

        var storeInterface = store;
        var result = await storeInterface.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id;
    }

    [Fact]
    public async Task QueryStringFieldEqualsShouldReturnExactMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice");
        _ = await CreateTestEntityAsync(store, "alice"); // Different case - stored as same uppercase value
        _ = await CreateTestEntityAsync(store, "Bob");

        var filter = new StringField("name").Equals("Alice");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Both "Alice" and "alice" are stored as "ALICE" in search fields,
        // so querying for "Alice" (uppercased to "ALICE") matches both entries.
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "alice");
    }

    [Fact]
    public async Task QueryStringFieldContainsShouldReturnPartialMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice Smith");
        _ = await CreateTestEntityAsync(store, "Bob Jones");
        _ = await CreateTestEntityAsync(store, "Charlie Smith");

        var filter = new StringField("name").Contains("Smith");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice Smith");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie Smith");
    }

    [Fact]
    public async Task QueryStringFieldStartsWithShouldReturnPrefixMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alpha");
        _ = await CreateTestEntityAsync(store, "Beta");
        _ = await CreateTestEntityAsync(store, "Alpha Centauri");

        var filter = new StringField("name").StartsWith("Alpha");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alpha");
        result.Items.ShouldContain(x => x.Value.Name == "Alpha Centauri");
    }

    [Fact]
    public async Task QueryStringFieldInShouldReturnMatchingValuesAsync()
    {
        // Arrange

        string[] activePendingStatuses = ["active", "pending"];

        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice", status: "active");
        _ = await CreateTestEntityAsync(store, "Bob", status: "pending");
        _ = await CreateTestEntityAsync(store, "Charlie", status: "inactive");
        _ = await CreateTestEntityAsync(store, "David", status: "active");

        var filter = new StringField("status").In(activePendingStatuses);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Bob");
        result.Items.ShouldContain(x => x.Value.Name == "David");
    }

    [Fact]
    public async Task QueryNumberFieldGreaterThanShouldReturnLargerValuesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 50);
        _ = await CreateTestEntityAsync(store, "User2", score: 75);
        _ = await CreateTestEntityAsync(store, "User3", score: 100);
        _ = await CreateTestEntityAsync(store, "User4", score: 125);

        var filter = new NumberField("score").GreaterThan(75);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "User3");
        result.Items.ShouldContain(x => x.Value.Name == "User4");
    }

    [Fact]
    public async Task NumberField_GreaterThan_should_exclude_the_boundary_value_async()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Age25", score: 25);
        _ = await CreateTestEntityAsync(store, "Age30", score: 30);
        _ = await CreateTestEntityAsync(store, "Age35", score: 35);

        var filter = new NumberField("score").GreaterThan(25);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert — GreaterThan(25) must use > not >=, so Age25 must NOT appear
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Age30");
        result.Items.ShouldContain(x => x.Value.Name == "Age35");
        result.Items.ShouldNotContain(x => x.Value.Name == "Age25");
    }

    [Fact]
    public async Task NumberField_Equal_should_return_only_the_exact_boundary_value_async()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Age25", score: 25);
        _ = await CreateTestEntityAsync(store, "Age30", score: 30);
        _ = await CreateTestEntityAsync(store, "Age35", score: 35);

        var filter = new NumberField("score").Equals(25);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert — Equal(25) must use = so only Age25 matches
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Age25");
    }

    [Fact]
    public async Task NumberField_GreaterThan_and_Equal_should_return_different_result_sets_async()
    {
        // Arrange — verifies GreaterThan and Equal produce distinct SQL operators
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Age25", score: 25);
        _ = await CreateTestEntityAsync(store, "Age30", score: 30);
        _ = await CreateTestEntityAsync(store, "Age35", score: 35);

        var greaterThanFilter = new NumberField("score").GreaterThan(25);
        var equalFilter = new NumberField("score").Equals(25);
        var page = DataRange.FromPage(1, 10);

        // Act
        var greaterThanResult = await store.QueryAsync<TestEntityDso>(_testEntityType, greaterThanFilter, SortParameter.Empty, page, _ct);
        var equalResult = await store.QueryAsync<TestEntityDso>(_testEntityType, equalFilter, SortParameter.Empty, page, _ct);

        // Assert — the two filters must produce non-overlapping results
        greaterThanResult.Items.Count.ShouldBe(2);
        equalResult.Items.Count.ShouldBe(1);

        // The entity returned by Equal must NOT appear in GreaterThan results
        var equalName = equalResult.Items[0].Value.Name;
        greaterThanResult.Items.ShouldNotContain(x => x.Value.Name == equalName);
    }

    [Fact]
    public async Task DateTimeField_GreaterThan_should_exclude_the_boundary_date_async()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var boundary = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var after1 = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var after2 = new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Boundary", createdAt: boundary);
        _ = await CreateTestEntityAsync(store, "After1", createdAt: after1);
        _ = await CreateTestEntityAsync(store, "After2", createdAt: after2);

        var cutoff = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").GreaterThan(cutoff);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert — GreaterThan must use > so the boundary date is excluded
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "After1");
        result.Items.ShouldContain(x => x.Value.Name == "After2");
        result.Items.ShouldNotContain(x => x.Value.Name == "Boundary");
    }

    [Fact]
    public async Task DateTimeField_Equal_should_return_only_the_exact_boundary_date_async()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var boundary = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var other = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Boundary", createdAt: boundary);
        _ = await CreateTestEntityAsync(store, "Other", createdAt: other);

        var cutoff = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").Equals(cutoff);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert — Equal must use = so only the exact date matches
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Boundary");
    }

    [Fact]
    public async Task QueryNumberFieldGreaterOrEqualShouldReturnEqualAndLargerValuesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 50);
        _ = await CreateTestEntityAsync(store, "User2", score: 75);
        _ = await CreateTestEntityAsync(store, "User3", score: 100);

        var filter = new NumberField("score").GreaterOrEqual(75);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "User2");
        result.Items.ShouldContain(x => x.Value.Name == "User3");
    }

    [Fact]
    public async Task QueryNumberFieldLessThanShouldReturnSmallerValuesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 25);
        _ = await CreateTestEntityAsync(store, "User2", score: 50);
        _ = await CreateTestEntityAsync(store, "User3", score: 75);
        _ = await CreateTestEntityAsync(store, "User4", score: 100);

        var filter = new NumberField("score").LessThan(60);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "User1");
        result.Items.ShouldContain(x => x.Value.Name == "User2");
    }

    [Fact]
    public async Task QueryNumberFieldLessOrEqualShouldReturnEqualAndSmallerValuesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 25);
        _ = await CreateTestEntityAsync(store, "User2", score: 50);
        _ = await CreateTestEntityAsync(store, "User3", score: 75);

        var filter = new NumberField("score").LessOrEqual(50);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "User1");
        result.Items.ShouldContain(x => x.Value.Name == "User2");
    }

    [Fact]
    public async Task QueryNumberFieldBetweenShouldReturnValuesInRangeAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 10);
        _ = await CreateTestEntityAsync(store, "User2", score: 25);
        _ = await CreateTestEntityAsync(store, "User3", score: 50);
        _ = await CreateTestEntityAsync(store, "User4", score: 75);
        _ = await CreateTestEntityAsync(store, "User5", score: 100);

        var filter = new NumberField("score").Between(25, 75);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should include boundaries (25 and 75)
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(x => x.Value.Name == "User2");
        result.Items.ShouldContain(x => x.Value.Name == "User3");
        result.Items.ShouldContain(x => x.Value.Name == "User4");
    }

    [Fact]
    public async Task QueryNumberFieldBetweenWithDecimalValuesShouldReturnValuesInRangeAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Product1", price: 9.99m);
        _ = await CreateTestEntityAsync(store, "Product2", price: 19.99m);
        _ = await CreateTestEntityAsync(store, "Product3", price: 29.99m);
        _ = await CreateTestEntityAsync(store, "Product4", price: 39.99m);
        _ = await CreateTestEntityAsync(store, "Product5", price: 49.99m);

        var filter = new NumberField("price").Between(15.0m, 35.0m);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Product2");
        result.Items.ShouldContain(x => x.Value.Name == "Product3");
    }

    [Fact]
    public async Task QueryNumberFieldInShouldReturnMatchingValuesAsync()
    {
        // Arrange
        decimal[] testNumberValues = [10.0m, 30.0m, 50.0m];
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 10);
        _ = await CreateTestEntityAsync(store, "User2", score: 20);
        _ = await CreateTestEntityAsync(store, "User3", score: 30);
        _ = await CreateTestEntityAsync(store, "User4", score: 40);
        _ = await CreateTestEntityAsync(store, "User5", score: 50);

        var filter = new NumberField("score").In(testNumberValues);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(x => x.Value.Name == "User1");
        result.Items.ShouldContain(x => x.Value.Name == "User3");
        result.Items.ShouldContain(x => x.Value.Name == "User5");
    }

    [Fact]
    public async Task QueryDateTimeFieldGreaterThanShouldReturnLaterDatesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var date4 = new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", createdAt: date1);
        _ = await CreateTestEntityAsync(store, "Event2", createdAt: date2);
        _ = await CreateTestEntityAsync(store, "Event3", createdAt: date3);
        _ = await CreateTestEntityAsync(store, "Event4", createdAt: date4);

        var cutoffDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").GreaterThan(cutoffDate);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Event3");
        result.Items.ShouldContain(x => x.Value.Name == "Event4");
    }

    [Fact]
    public async Task QueryDateTimeFieldGreaterOrEqualShouldReturnEqualAndLaterDatesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", createdAt: date1);
        _ = await CreateTestEntityAsync(store, "Event2", createdAt: date2);
        _ = await CreateTestEntityAsync(store, "Event3", createdAt: date3);

        var cutoffDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").GreaterOrEqual(cutoffDate);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Event2");
        result.Items.ShouldContain(x => x.Value.Name == "Event3");
    }

    [Fact]
    public async Task QueryDateTimeFieldLessThanShouldReturnEarlierDatesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date4 = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", createdAt: date1);
        _ = await CreateTestEntityAsync(store, "Event2", createdAt: date2);
        _ = await CreateTestEntityAsync(store, "Event3", createdAt: date3);
        _ = await CreateTestEntityAsync(store, "Event4", createdAt: date4);

        var cutoffDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").LessThan(cutoffDate);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Event1");
        result.Items.ShouldContain(x => x.Value.Name == "Event2");
    }

    [Fact]
    public async Task QueryDateTimeFieldLessOrEqualShouldReturnEqualAndEarlierDatesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", createdAt: date1);
        _ = await CreateTestEntityAsync(store, "Event2", createdAt: date2);
        _ = await CreateTestEntityAsync(store, "Event3", createdAt: date3);

        var cutoffDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").LessOrEqual(cutoffDate);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Event1");
        result.Items.ShouldContain(x => x.Value.Name == "Event2");
    }

    [Fact]
    public async Task QueryDateTimeFieldBetweenShouldReturnDatesInRangeAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date4 = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var date5 = new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", createdAt: date1);
        _ = await CreateTestEntityAsync(store, "Event2", createdAt: date2);
        _ = await CreateTestEntityAsync(store, "Event3", createdAt: date3);
        _ = await CreateTestEntityAsync(store, "Event4", createdAt: date4);
        _ = await CreateTestEntityAsync(store, "Event5", createdAt: date5);

        var startDate = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").Between(startDate, endDate);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should include boundaries (March 1 and September 1)
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(x => x.Value.Name == "Event2");
        result.Items.ShouldContain(x => x.Value.Name == "Event3");
        result.Items.ShouldContain(x => x.Value.Name == "Event4");
    }

    [Fact]
    public async Task QueryDateTimeFieldBetweenWithTimeShouldRespectTimeComponentAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2024, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 6, 1, 16, 0, 0, TimeSpan.Zero);
        var date4 = new DateTimeOffset(2024, 6, 1, 20, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", lastLogin: date1);
        _ = await CreateTestEntityAsync(store, "Event2", lastLogin: date2);
        _ = await CreateTestEntityAsync(store, "Event3", lastLogin: date3);
        _ = await CreateTestEntityAsync(store, "Event4", lastLogin: date4);

        var startTime = new DateTime(2024, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 6, 1, 18, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("lastLogin").Between(startTime, endTime);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Event2");
        result.Items.ShouldContain(x => x.Value.Name == "Event3");
    }

    [Fact]
    public async Task QueryDateTimeFieldEqualsShouldReturnExactMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 6, 1, 13, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 6, 2, 12, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", createdAt: date1);
        _ = await CreateTestEntityAsync(store, "Event2", createdAt: date2);
        _ = await CreateTestEntityAsync(store, "Event3", createdAt: date3);

        var targetDate = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").Equals(targetDate);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Event1");
    }

    [Fact]
    public async Task QueryBooleanFieldIsTrueShouldReturnTrueValuesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", isActive: true);
        _ = await CreateTestEntityAsync(store, "User2", isActive: false);
        _ = await CreateTestEntityAsync(store, "User3", isActive: true);

        var filter = new BooleanField("isActive").IsTrue();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "User1");
        result.Items.ShouldContain(x => x.Value.Name == "User3");
    }

    [Fact]
    public async Task QueryBooleanFieldIsFalseShouldReturnFalseValuesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", isActive: true);
        _ = await CreateTestEntityAsync(store, "User2", isActive: false);
        _ = await CreateTestEntityAsync(store, "User3", isActive: true);

        var filter = new BooleanField("isActive").IsFalse();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("User2");
    }

    [Fact]
    public async Task QueryBooleanFieldEqualsShouldReturnMatchingValuesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", isActive: true);
        _ = await CreateTestEntityAsync(store, "User2", isActive: false);
        _ = await CreateTestEntityAsync(store, "User3", isActive: true);

        var filterTrue = new BooleanField("isActive").Equals(true);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filterTrue, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "User1");
        result.Items.ShouldContain(x => x.Value.Name == "User3");
    }

    [Fact]
    public async Task QueryNumberFieldEqualsWithIntegerShouldReturnExactMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 50);
        _ = await CreateTestEntityAsync(store, "User2", score: 75);
        _ = await CreateTestEntityAsync(store, "User3", score: 50);

        var filter = new NumberField("score").Equals(50);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "User1");
        result.Items.ShouldContain(x => x.Value.Name == "User3");
    }

    [Fact]
    public async Task QueryNumberFieldEqualsWithDecimalShouldReturnExactMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Product1", price: 19.99m);
        _ = await CreateTestEntityAsync(store, "Product2", price: 29.99m);
        _ = await CreateTestEntityAsync(store, "Product3", price: 19.99m);

        var filter = new NumberField("price").Equals(19.99m);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Product1");
        result.Items.ShouldContain(x => x.Value.Name == "Product3");
    }

    [Fact]
    public async Task QueryNumberFieldWithNegativeNumbersShouldWorkCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Account1", score: -100);
        _ = await CreateTestEntityAsync(store, "Account2", score: -50);
        _ = await CreateTestEntityAsync(store, "Account3", score: 0);
        _ = await CreateTestEntityAsync(store, "Account4", score: 50);

        var filter = new NumberField("score").LessThan(0);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Account1");
        result.Items.ShouldContain(x => x.Value.Name == "Account2");
    }

    [Fact]
    public async Task QueryNumberFieldBetweenWithNegativeRangeShouldReturnCorrectResultsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Item1", score: -100);
        _ = await CreateTestEntityAsync(store, "Item2", score: -75);
        _ = await CreateTestEntityAsync(store, "Item3", score: -50);
        _ = await CreateTestEntityAsync(store, "Item4", score: -25);
        _ = await CreateTestEntityAsync(store, "Item5", score: 0);

        var filter = new NumberField("score").Between(-80, -40);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Item2");
        result.Items.ShouldContain(x => x.Value.Name == "Item3");
    }

    [Fact]
    public async Task QueryNumberFieldWithZeroShouldWorkCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Zero1", score: 0);
        _ = await CreateTestEntityAsync(store, "Positive", score: 10);
        _ = await CreateTestEntityAsync(store, "Zero2", score: 0);
        _ = await CreateTestEntityAsync(store, "Negative", score: -10);

        var filter = new NumberField("score").Equals(0);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Zero1");
        result.Items.ShouldContain(x => x.Value.Name == "Zero2");
    }

    [Fact]
    public async Task QueryDateTimeFieldMultipleRangesUsingOrShouldReturnUnionAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var jan = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var mar = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var jun = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var sep = new DateTimeOffset(2024, 9, 15, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", createdAt: jan);
        _ = await CreateTestEntityAsync(store, "Event2", createdAt: mar);
        _ = await CreateTestEntityAsync(store, "Event3", createdAt: jun);
        _ = await CreateTestEntityAsync(store, "Event4", createdAt: sep);

        // Match dates in January OR June (using Equals with OR)
        var jan1 = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jun1 = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var filter = new DateTimeField("recordedAt").Equals(jan1)
            .Or(new DateTimeField("recordedAt").Equals(jun1));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Event1");
        result.Items.ShouldContain(x => x.Value.Name == "Event3");
    }

    [Fact]
    public async Task QueryDateTimeFieldWithMillisecondPrecisionShouldMatchExactlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var date1 = new DateTimeOffset(2024, 6, 1, 12, 30, 45, 123, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 6, 1, 12, 30, 45, 456, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 6, 1, 12, 30, 45, 789, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", lastLogin: date1);
        _ = await CreateTestEntityAsync(store, "Event2", lastLogin: date2);
        _ = await CreateTestEntityAsync(store, "Event3", lastLogin: date3);

        var targetDate = new DateTime(2024, 6, 1, 12, 30, 45, 456, DateTimeKind.Utc);
        var filter = new DateTimeField("lastLogin").Equals(targetDate);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Event2");
    }

    [Fact]
    public async Task QueryStringFieldEqualsWithSpecialCharactersShouldMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "user@example.com");
        _ = await CreateTestEntityAsync(store, "user_123");
        _ = await CreateTestEntityAsync(store, "user-test");
        _ = await CreateTestEntityAsync(store, "user.name");

        var filter = new StringField("name").Equals("user@example.com");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("user@example.com");
    }

    [Fact]
    public async Task QueryStringFieldInWithEmptyCollectionShouldReturnNoResultsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", status: "active");
        _ = await CreateTestEntityAsync(store, "User2", status: "pending");

        string[] emptyStatuses = [];
        var filter = new StringField("status").In(emptyStatuses);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryStringFieldContainsWithPercentWildcardShouldTreatAsLiteralAsync()
    {
        // Arrange - Test that % is treated as a literal character, not a wildcard
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "100% Complete");
        _ = await CreateTestEntityAsync(store, "50% Done");
        _ = await CreateTestEntityAsync(store, "100 Complete");
        _ = await CreateTestEntityAsync(store, "100X Complete");

        var filter = new StringField("name").Contains("100%");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should only match "100% Complete", not "100 Complete" or "100X Complete"
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("100% Complete");
    }

    [Fact]
    public async Task QueryStringFieldContainsWithUnderscoreWildcardShouldTreatAsLiteralAsync()
    {
        // Arrange - Test that _ is treated as a literal character, not a single-char wildcard
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "user_name");
        _ = await CreateTestEntityAsync(store, "user-name");
        _ = await CreateTestEntityAsync(store, "username");
        _ = await CreateTestEntityAsync(store, "userXname");

        var filter = new StringField("name").Contains("user_");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should only match "user_name", not "user-name", "username", or "userXname"
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("user_name");
    }

    [Fact]
    public async Task QueryStringFieldStartsWithWithPercentWildcardShouldTreatAsLiteralAsync()
    {
        // Arrange - Test that % at the start is treated as a literal character
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "%discount_code");
        _ = await CreateTestEntityAsync(store, "Xdiscount_code");
        _ = await CreateTestEntityAsync(store, "discount_code");

        var filter = new StringField("name").StartsWith("%discount");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should only match "%discount_code"
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("%discount_code");
    }

    [Fact]
    public async Task QueryStringFieldStartsWithWithUnderscoreWildcardShouldTreatAsLiteralAsync()
    {
        // Arrange - Test that _ at the start is treated as a literal character
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "_private_method");
        _ = await CreateTestEntityAsync(store, "Xprivate_method");
        _ = await CreateTestEntityAsync(store, "private_method");

        var filter = new StringField("name").StartsWith("_private");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should only match "_private_method"
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("_private_method");
    }

    [Fact]
    public async Task QueryStringFieldContainsWithBackslashCharacterShouldTreatAsLiteralAsync()
    {
        // Arrange - Test that backslash is treated as a literal character
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, @"C:\Windows\System32");
        _ = await CreateTestEntityAsync(store, @"C:/Windows/System32");
        _ = await CreateTestEntityAsync(store, @"Windows\System32");

        var filter = new StringField("name").Contains(@"Windows\System");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should match paths with backslashes, not forward slashes
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == @"C:\Windows\System32");
        result.Items.ShouldContain(x => x.Value.Name == @"Windows\System32");
    }

    [Fact]
    public async Task QueryStringFieldContainsWithMultipleWildcardsShouldEscapeAllAsync()
    {
        // Arrange - Test that multiple wildcard characters are all escaped
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "discount_50%_off");
        _ = await CreateTestEntityAsync(store, "discountX50XX off");
        _ = await CreateTestEntityAsync(store, "discount-50%-off");

        var filter = new StringField("name").Contains("discount_50%");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should only match "discount_50%_off"
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("discount_50%_off");
    }

    [Fact]
    public async Task QueryStringFieldStartsWithWithCombinedWildcardsShouldEscapeAllAsync()
    {
        // Arrange - Test StartsWith with multiple wildcard characters
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "_%special_prefix%_test");
        _ = await CreateTestEntityAsync(store, "XXspecial_prefixXX test");
        _ = await CreateTestEntityAsync(store, "special_prefix_test");

        var filter = new StringField("name").StartsWith("_%special");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Should only match "_%special_prefix%_test"
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("_%special_prefix%_test");
    }

    [Fact]
    public async Task QueryNumberFieldInWithSingleValueShouldMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 10);
        _ = await CreateTestEntityAsync(store, "User2", score: 20);
        _ = await CreateTestEntityAsync(store, "User3", score: 30);

        decimal[] singleValue = [20.0m];
        var filter = new NumberField("score").In(singleValue);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("User2");
    }

    [Fact]
    public async Task QueryNumberFieldInWithEmptyCollectionShouldReturnNoResultsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 10);
        _ = await CreateTestEntityAsync(store, "User2", score: 20);

        decimal[] emptyValues = [];
        var filter = new NumberField("score").In(emptyValues);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryDateTimeFieldEdgeOfDayShouldDistinguishAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var startOfDay = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var endOfDay = new DateTimeOffset(2024, 6, 1, 23, 59, 59, 999, TimeSpan.Zero);
        var nextDay = new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "StartOfDay", createdAt: startOfDay);
        _ = await CreateTestEntityAsync(store, "EndOfDay", createdAt: endOfDay);
        _ = await CreateTestEntityAsync(store, "NextDay", createdAt: nextDay);

        // Filter for everything before end of June 1st
        var cutoff = new DateTime(2024, 6, 2, 0, 0, 0, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").LessThan(cutoff);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "StartOfDay");
        result.Items.ShouldContain(x => x.Value.Name == "EndOfDay");
    }

    [Fact]
    public async Task QueryNumberFieldWithVeryLargeNumbersShouldWorkCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Item1", price: 999999999.99m);
        _ = await CreateTestEntityAsync(store, "Item2", price: 1000000000.00m);
        _ = await CreateTestEntityAsync(store, "Item3", price: 1000000000.01m);

        var filter = new NumberField("price").GreaterThan(1000000000.00m);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Item3");
    }

    [Fact]
    public async Task QueryNumberFieldWithVerySmallDecimalsShouldWorkCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Item1", price: 0.001m);
        _ = await CreateTestEntityAsync(store, "Item2", price: 0.002m);
        _ = await CreateTestEntityAsync(store, "Item3", price: 0.003m);

        var filter = new NumberField("price").Between(0.0015m, 0.0025m);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Item2");
    }

    [Fact]
    public async Task QueryStringFieldEndsWithUsingContainsShouldMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "john.smith@example.com");
        _ = await CreateTestEntityAsync(store, "jane.doe@example.com");
        _ = await CreateTestEntityAsync(store, "bob@test.com");

        var filter = new StringField("name").Contains("@example.com");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "john.smith@example.com");
        result.Items.ShouldContain(x => x.Value.Name == "jane.doe@example.com");
    }

    [Fact]
    public async Task QueryDateTimeFieldAtMidnightShouldMatchExactlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var midnight = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var oneSecondAfter = new DateTimeOffset(2024, 6, 1, 0, 0, 1, TimeSpan.Zero);
        var oneSecondBefore = new DateTimeOffset(2024, 5, 31, 23, 59, 59, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Before", createdAt: oneSecondBefore);
        _ = await CreateTestEntityAsync(store, "Midnight", createdAt: midnight);
        _ = await CreateTestEntityAsync(store, "After", createdAt: oneSecondAfter);

        var filter = new DateTimeField("recordedAt").Equals(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Midnight");
    }

    [Fact]
    public async Task QueryCombinedFiltersNotOperatorSimulationUsingOrAsync()
    {
        // Arrange - Simulate NOT by testing values outside a range
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Low", score: 10);
        _ = await CreateTestEntityAsync(store, "MidLow", score: 40);
        _ = await CreateTestEntityAsync(store, "MidHigh", score: 60);
        _ = await CreateTestEntityAsync(store, "High", score: 90);

        // Get all values NOT between 30 and 70 (i.e., less than 30 OR greater than 70)
        var filter = new NumberField("score").LessThan(30)
            .Or(new NumberField("score").GreaterThan(70));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Low");
        result.Items.ShouldContain(x => x.Value.Name == "High");
    }

    [Fact]
    public async Task QueryMixedTypesAllFieldTypesTogetherAsync()
    {
        // Arrange - Test all four field types in one complex query
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var date1 = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 6, 15, 14, 45, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Match1", score: 75, price: 25.50m, createdAt: date1, isActive: true, status: "premium");
        _ = await CreateTestEntityAsync(store, "NoMatch1", score: 45, price: 25.50m, createdAt: date1, isActive: true, status: "basic");
        _ = await CreateTestEntityAsync(store, "NoMatch2", score: 75, price: 15.00m, createdAt: date1, isActive: false, status: "premium");
        _ = await CreateTestEntityAsync(store, "Match2", score: 85, price: 30.00m, createdAt: date2, isActive: true, status: "premium");

        // Complex filter: score >= 70 AND price > 20 AND isActive = true AND status = "premium"
        var filter = new NumberField("score").GreaterOrEqual(70)
            .And(new NumberField("price").GreaterThan(20m))
            .And(new BooleanField("isActive").IsTrue())
            .And(new StringField("status").Equals("premium"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Match1");
        result.Items.ShouldContain(x => x.Value.Name == "Match2");
    }

    [Fact]
    public async Task QueryCombineNumberRangesWithAndShouldReturnIntersectionAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 50, price: 10.0m);
        _ = await CreateTestEntityAsync(store, "User2", score: 75, price: 20.0m);
        _ = await CreateTestEntityAsync(store, "User3", score: 100, price: 30.0m);
        _ = await CreateTestEntityAsync(store, "User4", score: 125, price: 40.0m);

        // Score between 60-110 AND price between 15-35
        var filter = new NumberField("score").Between(60, 110)
            .And(new NumberField("price").Between(15.0m, 35.0m));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "User2");
        result.Items.ShouldContain(x => x.Value.Name == "User3");
    }

    [Fact]
    public async Task QueryCombineDateTimeRangesWithOrShouldReturnUnionAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var jan = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var apr = new DateTimeOffset(2024, 4, 15, 0, 0, 0, TimeSpan.Zero);
        var jul = new DateTimeOffset(2024, 7, 15, 0, 0, 0, TimeSpan.Zero);
        var oct = new DateTimeOffset(2024, 10, 15, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Event1", createdAt: jan);
        _ = await CreateTestEntityAsync(store, "Event2", createdAt: apr);
        _ = await CreateTestEntityAsync(store, "Event3", createdAt: jul);
        _ = await CreateTestEntityAsync(store, "Event4", createdAt: oct);

        // Created in Q1 (Jan-Mar) OR Q4 (Oct-Dec)
        var q1Start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var q1End = new DateTime(2024, 3, 31, 23, 59, 59, DateTimeKind.Utc);
        var q4Start = new DateTime(2024, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var q4End = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var filter = new DateTimeField("recordedAt").Between(q1Start, q1End)
            .Or(new DateTimeField("recordedAt").Between(q4Start, q4End));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Event1");
        result.Items.ShouldContain(x => x.Value.Name == "Event4");
    }

    [Fact]
    public async Task QueryMixedFieldTypesComplexExpressionShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;
        var date1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await CreateTestEntityAsync(store, "Alice", score: 85, createdAt: date1, isActive: true, status: "premium");
        _ = await CreateTestEntityAsync(store, "Bob", score: 45, createdAt: date2, isActive: false, status: "basic");
        _ = await CreateTestEntityAsync(store, "Charlie", score: 95, createdAt: date3, isActive: true, status: "premium");
        _ = await CreateTestEntityAsync(store, "David", score: 75, createdAt: date2, isActive: true, status: "basic");

        // Complex filter: (score >= 80 AND isActive) OR (createdAt after June 1 AND status = premium)
        var juneFirst = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new NumberField("score").GreaterOrEqual(80)
            .And(new BooleanField("isActive").IsTrue())
            .Or(new DateTimeField("recordedAt").GreaterThan(juneFirst)
                .And(new StringField("status").Equals("premium")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");  // Matches first part: score >= 80 AND active
        result.Items.ShouldContain(x => x.Value.Name == "Charlie"); // Matches second part: after June AND premium
    }

    [Fact]
    public async Task QueryWithNumberRangeFilterShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 25 entities with scores from 10 to 250 (step of 10)
        for (var i = 1; i <= 25; i++)
        {
            _ = await CreateEntityAsync(store, $"User{i:D2}", score: i * 10);
        }

        // Filter: score between 50 and 200 (should match 16 items: 50, 60, 70...200)
        var filter = new NumberField("score").Between(50, 200);
        var sort = new SortParameter(new NumberField("score"));

        // Act - Get page 1
        var page1Request = DataRange.FromPage(1, 5);
        var page1 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, page1Request, Ct.None);

        // Act - Get page 2
        var page2Request = DataRange.FromPage(2, 5);
        var page2 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, page2Request, Ct.None);

        // Act - Get page 3
        var page3Request = DataRange.FromPage(3, 5);
        var page3 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, page3Request, Ct.None);

        // Act - Get page 4 (last page with 1 item)
        var page4Request = DataRange.FromPage(4, 5);
        var page4 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, page4Request, Ct.None);

        // Assert - Page 1
        page1.Items.Count.ShouldBe(5);
        page1.TotalCount.ShouldBe(16);
        page1.HasMoreData.ShouldBeTrue();
        page1.Items[0].Value.Score.ShouldBe(50);
        page1.Items[4].Value.Score.ShouldBe(90);

        // Assert - Page 2
        page2.Items.Count.ShouldBe(5);
        page2.TotalCount.ShouldBe(16);
        page2.HasMoreData.ShouldBeTrue();
        page2.Items[0].Value.Score.ShouldBe(100);
        page2.Items[4].Value.Score.ShouldBe(140);

        // Assert - Page 3
        page3.Items.Count.ShouldBe(5);
        page3.TotalCount.ShouldBe(16);
        page3.HasMoreData.ShouldBeTrue();
        page3.Items[0].Value.Score.ShouldBe(150);
        page3.Items[4].Value.Score.ShouldBe(190);

        // Assert - Page 4 (partial page)
        page4.Items.Count.ShouldBe(1);
        page4.TotalCount.ShouldBe(16);
        page4.HasMoreData.ShouldBeFalse();
        page4.Items[0].Value.Score.ShouldBe(200);
    }

    [Fact]
    public async Task QueryWithDateTimeRangeFilterShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 30 entities spread across 2024
        for (var month = 1; month <= 12; month++)
        {
            for (var day = 1; day <= 15; day += 7) // Day 1, 8, 15
            {
                var date = new DateTimeOffset(2024, month, day, 12, 0, 0, TimeSpan.Zero);
                _ = await CreateEntityAsync(store, $"Event{month:D2}_{day:D2}", createdAt: date);
            }
        }

        // Filter: Q2 and Q3 (April 1 to September 30) - should match 18 items
        var startDate = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 9, 30, 23, 59, 59, DateTimeKind.Utc);
        var filter = new DateTimeField("recordedAt").Between(startDate, endDate);
        var sort = new SortParameter(new DateTimeField("recordedAt"));

        // Act - Get first page
        var page1Request = DataRange.FromPage(1, 7);
        var page1 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, page1Request, Ct.None);

        // Act - Get second page
        var page2Request = DataRange.FromPage(2, 7);
        var page2 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, page2Request, Ct.None);

        // Act - Get third page (partial)
        var page3Request = DataRange.FromPage(3, 7);
        var page3 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, page3Request, Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(7);
        page1.TotalCount.ShouldBe(18);
        page1.HasMoreData.ShouldBeTrue();

        page2.Items.Count.ShouldBe(7);
        page2.HasMoreData.ShouldBeTrue();

        page3.Items.Count.ShouldBe(4); // Last 4 items
        page3.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryWithComplexFilterShouldPageCorrectlyAroundBreaksAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 50 entities
        for (var i = 1; i <= 50; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}",
                score: i,
                isActive: i % 2 == 0, // Even numbers are active
                status: i % 3 == 0 ? "premium" : "basic");
        }

        // Filter: (score > 20 AND score < 45) AND isActive = true
        // Should match: 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 42, 44 (12 items)
        var filter = new NumberField("score").GreaterThan(20)
            .And(new NumberField("score").LessThan(45))
            .And(new BooleanField("isActive").IsTrue());
        var sort = new SortParameter(new NumberField("score"));

        // Test exact page break: 12 items with page size 4 = exactly 3 pages
        var page1 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 4), Ct.None);
        var page2 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 4), Ct.None);
        var page3 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 4), Ct.None);
        var page4 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, DataRange.FromPage(4, 4), Ct.None);

        // Assert - Verify exact page break
        page1.Items.Count.ShouldBe(4);
        page1.TotalCount.ShouldBe(12);
        page1.Items[0].Value.Score.ShouldBe(22);
        page1.Items[3].Value.Score.ShouldBe(28);

        page2.Items.Count.ShouldBe(4);
        page2.Items[0].Value.Score.ShouldBe(30);
        page2.Items[3].Value.Score.ShouldBe(36);

        page3.Items.Count.ShouldBe(4);
        page3.Items[0].Value.Score.ShouldBe(38);
        page3.Items[3].Value.Score.ShouldBe(44);
        page3.HasMoreData.ShouldBeFalse();

        // Page 4 should be empty
        page4.Items.Count.ShouldBe(0);
        page4.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QuerySmallPageSizeShouldHandleManyPagesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create exactly 10 items
        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", score: i * 10);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("score"));

        // Act - Use page size of 3 to create 4 pages (3+3+3+1)
        var pages = new List<QueryResult<MetadataEnvelope<TestEntityDso>>>();
        for (var pageNum = 1; pageNum <= 5; pageNum++)
        {
            var page = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort,
                DataRange.FromPage(pageNum, 3), Ct.None);
            pages.Add(page);
        }

        // Assert
        pages[0].Items.Count.ShouldBe(3);
        pages[0].Items[0].Value.Score.ShouldBe(10);
        pages[0].Items[2].Value.Score.ShouldBe(30);

        pages[1].Items.Count.ShouldBe(3);
        pages[1].Items[0].Value.Score.ShouldBe(40);
        pages[1].Items[2].Value.Score.ShouldBe(60);

        pages[2].Items.Count.ShouldBe(3);
        pages[2].Items[0].Value.Score.ShouldBe(70);
        pages[2].Items[2].Value.Score.ShouldBe(90);

        pages[3].Items.Count.ShouldBe(1);
        pages[3].Items[0].Value.Score.ShouldBe(100);
        pages[3].HasMoreData.ShouldBeFalse();

        pages[4].Items.Count.ShouldBe(0); // Beyond last page
    }

    [Fact]
    public async Task QueryExactPageBoundaryShouldHandleCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create exactly 20 items (will be exactly 4 pages of 5)
        for (var i = 1; i <= 20; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i:D2}", score: i);
        }

        var filter = Query.All();
        var sort = new SortParameter(new NumberField("score"));

        // Act
        var page1 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 5), Ct.None);
        var page4 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, DataRange.FromPage(4, 5), Ct.None);
        var page5 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, DataRange.FromPage(5, 5), Ct.None);

        // Assert
        // Last page should be full (not partial)
        page4.Items.Count.ShouldBe(5);
        page4.Items[0].Value.Score.ShouldBe(16);
        page4.Items[4].Value.Score.ShouldBe(20);
        page4.HasMoreData.ShouldBeFalse();

        // Page beyond should be empty
        page5.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QuerySingleItemResultShouldPageCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateEntityAsync(store, $"Item{i}", score: i * 10);
        }

        // Filter that matches only one item
        var filter = new NumberField("score").Equals(50);
        var sort = new SortParameter(new NumberField("score"));

        // Act
        var page1 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 5), Ct.None);
        var page2 = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 5), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(1);
        page1.TotalCount.ShouldBe(1);
        page1.HasMoreData.ShouldBeFalse();

        page2.Items.Count.ShouldBe(0);
        page2.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task QueryStringFieldEndsWithShouldReturnSuffixMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice Smith");
        _ = await CreateTestEntityAsync(store, "Charlie Smith");
        _ = await CreateTestEntityAsync(store, "Bob Jones");

        var filter = new StringField("name").EndsWith("Smith");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice Smith");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie Smith");
    }

    [Fact]
    public async Task QueryStringFieldEndsWithCaseInsensitiveShouldMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice Smith");
        _ = await CreateTestEntityAsync(store, "Charlie Smith");
        _ = await CreateTestEntityAsync(store, "Bob Jones");

        // Lowercase suffix - should match case-insensitively
        var filter = new StringField("name").EndsWith("smith");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice Smith");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie Smith");
    }

    [Fact]
    public async Task QueryStringFieldEndsWithWithPercentCharacterShouldTreatAsLiteralAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "discount50%");
        _ = await CreateTestEntityAsync(store, "discount50");
        _ = await CreateTestEntityAsync(store, "no match");

        // The '%' character should be treated as a literal, not a wildcard
        var filter = new StringField("name").EndsWith("50%");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("discount50%");
    }

    [Fact]
    public async Task QueryStringFieldEndsWithWithUnderscoreCharacterShouldTreatAsLiteralAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "user_name");
        _ = await CreateTestEntityAsync(store, "username");
        _ = await CreateTestEntityAsync(store, "no match");

        // The '_' character should be treated as a literal, not a single-character wildcard
        var filter = new StringField("name").EndsWith("_name");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("user_name");
    }

    [Fact]
    public async Task QueryStringFieldEndsWithNoMatchShouldReturnEmptyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alpha");
        _ = await CreateTestEntityAsync(store, "Beta");
        _ = await CreateTestEntityAsync(store, "Gamma");

        var filter = new StringField("name").EndsWith("xyz_nomatch");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryNotEqualShouldReturnNonMatchingEntitiesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice");
        _ = await CreateTestEntityAsync(store, "Bob");
        _ = await CreateTestEntityAsync(store, "Charlie");

        var filter = Query.Not(new StringField("name").Equals("Alice"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Bob");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task QueryNotContainsShouldExcludePartialMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "alice@example.com");
        _ = await CreateTestEntityAsync(store, "bob@test.com");
        _ = await CreateTestEntityAsync(store, "charlie@example.com");

        var filter = Query.Not(new StringField("name").Contains("example"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("bob@test.com");
    }

    [Fact]
    public async Task QueryNotStartsWithShouldExcludePrefixMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alpha");
        _ = await CreateTestEntityAsync(store, "Alpha Centauri");
        _ = await CreateTestEntityAsync(store, "Beta");

        var filter = Query.Not(new StringField("name").StartsWith("Alpha"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Beta");
    }

    [Fact]
    public async Task QueryNotEndsWithShouldExcludeSuffixMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice Smith");
        _ = await CreateTestEntityAsync(store, "Charlie Smith");
        _ = await CreateTestEntityAsync(store, "Bob Jones");

        var filter = Query.Not(new StringField("name").EndsWith("Smith"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Bob Jones");
    }

    [Fact]
    public async Task QueryNotCombinedWithAndShouldApplyNegationToWholeExpressionAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice", score: 80, isActive: true);
        _ = await CreateTestEntityAsync(store, "Bob", score: 90, isActive: true);
        _ = await CreateTestEntityAsync(store, "Charlie", score: 70, isActive: false);
        _ = await CreateTestEntityAsync(store, "David", score: 60, isActive: true);

        // NOT(score > 75 AND isActive = true) => excludes Alice and Bob
        var filter = Query.Not(new NumberField("score").GreaterThan(75)
            .And(new BooleanField("isActive").IsTrue()));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
        result.Items.ShouldContain(x => x.Value.Name == "David");
    }

    [Fact]
    public async Task QueryNotCombinedWithOrShouldApplyNegationToWholeExpressionAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice", status: "active");
        _ = await CreateTestEntityAsync(store, "Bob", status: "pending");
        _ = await CreateTestEntityAsync(store, "Charlie", status: "inactive");
        _ = await CreateTestEntityAsync(store, "David", status: "banned");

        // NOT(status = "active" OR status = "pending") => only inactive and banned
        var filter = Query.Not(new StringField("status").Equals("active")
            .Or(new StringField("status").Equals("pending")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
        result.Items.ShouldContain(x => x.Value.Name == "David");
    }

    [Fact]
    public async Task QueryDoubleNotShouldReturnOriginalMatchesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice");
        _ = await CreateTestEntityAsync(store, "Bob");
        _ = await CreateTestEntityAsync(store, "Charlie");

        // NOT(NOT(name = "Alice")) is equivalent to name = "Alice"
        var filter = Query.Not(Query.Not(new StringField("name").Equals("Alice")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Alice");
    }

    [Fact]
    public async Task QueryNotBooleanShouldReturnOppositeValuesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Active1", isActive: true);
        _ = await CreateTestEntityAsync(store, "Inactive1", isActive: false);
        _ = await CreateTestEntityAsync(store, "Active2", isActive: true);

        var filter = Query.Not(new BooleanField("isActive").IsTrue());
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Inactive1");
    }

    [Fact]
    public async Task QueryNotPresentShouldReturnEntitiesWithMissingFieldAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "WithScore", score: 42);
        _ = await CreateTestEntityAsync(store, "WithoutScore");

        var filter = Query.Not(new NumberField("score").Present());
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("WithoutScore");
    }

    [Fact]
    public async Task QueryStringFieldPresentShouldReturnEntitiesWithFieldSetAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "WithStatus", status: "active");
        _ = await CreateTestEntityAsync(store, "WithoutStatus");
        _ = await CreateTestEntityAsync(store, "AlsoWithStatus", status: "pending");

        var filter = new StringField("status").Present();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "WithStatus");
        result.Items.ShouldContain(x => x.Value.Name == "AlsoWithStatus");
    }

    [Fact]
    public async Task QueryNumberFieldPresentShouldReturnEntitiesWithFieldSetAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "WithScore", score: 42);
        _ = await CreateTestEntityAsync(store, "WithoutScore");
        _ = await CreateTestEntityAsync(store, "AlsoWithScore", score: 99);

        var filter = new NumberField("score").Present();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "WithScore");
        result.Items.ShouldContain(x => x.Value.Name == "AlsoWithScore");
    }

    [Fact]
    public async Task QueryDateTimeFieldPresentShouldReturnEntitiesWithFieldSetAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var date = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        _ = await CreateTestEntityAsync(store, "WithDate", createdAt: date);
        _ = await CreateTestEntityAsync(store, "WithoutDate");
        _ = await CreateTestEntityAsync(store, "AlsoWithDate", createdAt: date.AddDays(1));

        var filter = new DateTimeField("recordedAt").Present();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "WithDate");
        result.Items.ShouldContain(x => x.Value.Name == "AlsoWithDate");
    }

    [Fact]
    public async Task QueryBooleanFieldPresentShouldReturnEntitiesWithFieldSetAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "WithFlag", isActive: true);
        _ = await CreateTestEntityAsync(store, "WithFalseFlag", isActive: false);
        _ = await CreateTestEntityAsync(store, "WithoutFlag");

        var filter = new BooleanField("isActive").Present();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "WithFlag");
        result.Items.ShouldContain(x => x.Value.Name == "WithFalseFlag");
    }

    [Fact]
    public async Task QueryPresentWhenNoEntitiesHaveFieldShouldReturnEmptyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1");
        _ = await CreateTestEntityAsync(store, "User2");

        var filter = new NumberField("score").Present();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryPresentCombinedWithOtherFilterShouldNarrowResultsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice", score: 80, status: "active");
        _ = await CreateTestEntityAsync(store, "Bob", score: 50);           // no status
        _ = await CreateTestEntityAsync(store, "Charlie", status: "active"); // no score

        // Present(score) AND status = "active"
        var filter = new NumberField("score").Present()
            .And(new StringField("status").Equals("active"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestEntityDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Alice");
    }

    private static readonly string[] AdminUserTags = ["admin", "user"];
    private static readonly string[] UserOnlyTags = ["user"];
    private static readonly string[] AdminModeratorTags = ["admin", "moderator"];
    private static readonly string[] AdminOnlyTags = ["admin"];
    private static readonly EntityType UserEntityType = new(2, "UserEntity");

    [Fact]
    public async Task QueryArrayContainsShouldReturnEntitiesWithMatchingArrayElementAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestUserWithTagsAsync(store, "Alice", AdminUserTags);
        _ = await CreateTestUserWithTagsAsync(store, "Bob", UserOnlyTags);
        _ = await CreateTestUserWithTagsAsync(store, "Charlie", AdminModeratorTags);

        var filter = new StringArrayField("tags").Contains("admin");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(UserEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task QueryArrayContainsNoMatchShouldReturnEmptyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestUserWithTagsAsync(store, "Alice", AdminUserTags);
        _ = await CreateTestUserWithTagsAsync(store, "Bob", UserOnlyTags);

        var filter = new StringArrayField("tags").Contains("superadmin");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(UserEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryArrayContainsCombinedWithStringFilterShouldNarrowResultsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestUserWithTagsAsync(store, "Alice", AdminUserTags);
        _ = await CreateTestUserWithTagsAsync(store, "AdminBob", AdminOnlyTags);
        _ = await CreateTestUserWithTagsAsync(store, "Bob", UserOnlyTags);

        // tags contains "admin" AND name starts with "Alice"
        var filter = new StringArrayField("tags").Contains("admin")
            .And(new StringField("name").StartsWith("Alice"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(UserEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Alice");
    }

    private static async Task<UuidV7> CreateTestUserWithTagsAsync(
        IStore store,
        string name,
        string[] tags,
        Ct ct = default)
    {
        var id = UuidV7.New();
        var dso = new TestUserDso { Name = name };
        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);
        for (var i = 0; i < tags.Length; i++)
        {
            _ = searchFieldsBuilder.Add("tags", i, tags[i]);
        }

        var searchFields = searchFieldsBuilder.Build();
        var storeInterface = store;
        var result = await storeInterface.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id;
    }

}
