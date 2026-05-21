// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Pagination;
using SortParameter = Duende.Storage.Internal.Querying.Sorting.SortParameter;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Tests for array filter expressions across all store implementations.
/// Tests SCIM2-compatible array filtering where all conditions must match within the same array item.
///
/// Test Coverage for OR Expressions in Array Filters:
/// - EqualExpression: Tested with multiple OR branches (strings, numbers, datetimes)
/// - ContainsExpression: Tested in OR conditions
/// - StartsWithExpression: Tested in OR conditions
/// - InExpression: Tested in OR conditions and combined with other expressions
/// - Mixed expression types: Tested with combinations of different expression types in OR
/// - Complex multi-branch OR: Tested with 3+ OR branches
///
/// Note: Numeric comparison expressions (GreaterThan, LessThan, GreaterOrEqual, LessOrEqual, BetweenExpression)
/// follow the same code path as EqualExpression in OR conditions (see SqlWhereClauseBuilder.cs:587-596, IsLeafExpression).
/// The BuildOrFilter and CollectConditions methods handle all expression types uniformly.
/// </summary>
public partial class QueryStoreArrayFilterTests
{
    private readonly EntityType _testEntityType = new(2, "UserEntity");

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly string[] WorkBusinessTypes = ["work", "business"];

    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestEntityDso>();
            services.AddDsoRegistration<TestUserDso>();
            services.AddDsoRegistration<TestSortDso>();
        });

    private static async Task<UuidV7> CreateUserWithEmailsAsync(
        IStore store,
        string name,
        EmailAddress[] emails,
        Ct ct)
    {
        var id = UuidV7.New();
        var dso = new TestUserDso
        {
            Name = name,
            Emails = emails
        };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);

        for (var i = 0; i < emails.Length; i++)
        {
            _ = searchFieldsBuilder.Add("emails.type", i, emails[i].Type);
            _ = searchFieldsBuilder.Add("emails.value", i, emails[i].Value);
            if (emails[i].CreatedAt is { } createdAt)
            {
                _ = searchFieldsBuilder.Add("emails.recordedAt", i, createdAt);
            }
            if (emails[i].Priority is { } priority)
            {
                _ = searchFieldsBuilder.Add("emails.priority", i, priority);
            }
        }

        var searchFields = searchFieldsBuilder.Build();
        // IStore extends IStore, so we can cast
        var storeInterface = store;
        var result = await storeInterface.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id;
    }

    [Fact]
    public async Task QueryArrayFilterSingleConditionShouldReturnMatchingEntitiesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" },
            new EmailAddress { Type = "personal", Value = "alice@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "personal", Value = "bob@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "work", Value = "charlie@work.com" }
        ], Ct.None);

        var filter = Query.ArrayFilter("emails", new StringField("type").Equals("work"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task QueryArrayFilterMultipleConditionsShouldMatchSameArrayItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" },
            new EmailAddress { Type = "personal", Value = "alice@example.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "work", Value = "bob@home.com" },
            new EmailAddress { Type = "personal", Value = "bob@example.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "work", Value = "charlie@example.com" }
        ], Ct.None);

        // Filter: emails where type="work" AND value contains "example"
        // Should match: Charlie (has work email containing "example")
        // Should NOT match: Alice (has work email but doesn't contain "example", has example email but not work type)
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .And(new StringField("value").Contains("example")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Charlie");
    }

    [Fact]
    public async Task QueryArrayFilterWithOrConditionShouldMatchSameArrayItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" },
            new EmailAddress { Type = "personal", Value = "alice@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "business", Value = "bob@company.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "other", Value = "charlie@test.com" }
        ], Ct.None);

        // Filter: emails where type="work" OR type="business"
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .Or(new StringField("type").Equals("business")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Bob");
    }

    [Fact]
    public async Task QueryArrayFilterCombinedWithOtherFiltersShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "work", Value = "bob@work.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "personal", Value = "charlie@home.com" }
        ], Ct.None);

        // Filter: name starts with "A" AND has a work email
        var filter = new StringField("name").StartsWith("A")
            .And(Query.ArrayFilter("emails", new StringField("type").Equals("work")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Alice");
    }

    [Fact]
    public async Task QueryArrayFilterNoMatchingArrayItemShouldReturnNoResultsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" },
            new EmailAddress { Type = "personal", Value = "alice@home.com" }
        ], Ct.None);

        // Filter: emails where type="work" AND value contains "home"
        // No single array item has both conditions true
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .And(new StringField("value").Contains("home")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryArrayFilterEmptyArrayShouldReturnNoMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [], Ct.None);
        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "work", Value = "bob@work.com" }
        ], Ct.None);

        var filter = Query.ArrayFilter("emails", new StringField("type").Equals("work"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Bob");
    }

    [Fact]
    public async Task QueryArrayFilterComplexScimLikeScenarioShouldWorkAsync()
    {
        // Arrange - Simulating SCIM2 user schema
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "User1", [
            new EmailAddress { Type = "work", Value = "user1@example.com" },
            new EmailAddress { Type = "home", Value = "user1@personal.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "User2", [
            new EmailAddress { Type = "work", Value = "user2@test.com" },
            new EmailAddress { Type = "other", Value = "user2@example.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "User3", [
            new EmailAddress { Type = "home", Value = "user3@example.com" }
        ], Ct.None);

        // SCIM filter: emails[type eq "work" and value co "@example.com"]
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .And(new StringField("value").Contains("@example.com")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("User1");
    }

    [Fact]
    public async Task QueryArrayFilterOrWithContainsExpressionShouldMatchAnyArrayItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@company.com" },
            new EmailAddress { Type = "personal", Value = "alice@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "work", Value = "bob@example.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "personal", Value = "charlie@other.com" }
        ], Ct.None);

        // Filter: emails where value contains "company" OR value contains "example"
        var filter = Query.ArrayFilter("emails",
            new StringField("value").Contains("company")
                .Or(new StringField("value").Contains("example")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Bob");
    }

    [Fact]
    public async Task QueryArrayFilterOrWithStartsWithExpressionShouldMatchAnyArrayItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" },
            new EmailAddress { Type = "personal", Value = "alice@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "work", Value = "bob@company.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "personal", Value = "charlie@personal.com" }
        ], Ct.None);

        // Filter: emails where value starts with "alice@" OR value starts with "charlie@"
        var filter = Query.ArrayFilter("emails",
            new StringField("value").StartsWith("alice@")
                .Or(new StringField("value").StartsWith("charlie@")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task QueryArrayFilterOrWithInExpressionShouldMatchAnyArrayItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" },
            new EmailAddress { Type = "personal", Value = "alice@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "business", Value = "bob@company.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "other", Value = "charlie@test.com" }
        ], Ct.None);

        // Filter: emails where type IN ("work", "business") OR type = "other"
        var filter = Query.ArrayFilter("emails",
            new StringField("type").In(WorkBusinessTypes)
                .Or(new StringField("type").Equals("other")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Bob");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task QueryArrayFilterOrStringDatetimeoffsetShouldMatchAnyArrayItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var date1 = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2024, 6, 15, 14, 45, 0, TimeSpan.Zero);
        var date3 = new DateTimeOffset(2024, 12, 20, 16, 0, 0, TimeSpan.Zero);

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com", CreatedAt = date1 },
            new EmailAddress { Type = "personal", Value = "alice@home.com", CreatedAt = date2 }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "business", Value = "bob@company.com", CreatedAt = date2 }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "other", Value = "charlie@test.com", CreatedAt = date3 }
        ], Ct.None);

        // Filter: emails where type == work OR recordedAt = date3
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .Or(new DateTimeField("recordedAt").Equals(date3)));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task QueryArrayFilterOrStringNumberShouldMatchAnyArrayItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com", Priority = 1 },
            new EmailAddress { Type = "personal", Value = "alice@home.com", Priority = 2 }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "business", Value = "bob@company.com", Priority = 2 }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "other", Value = "charlie@test.com", Priority = 5 }
        ], Ct.None);

        // Filter: emails where type == "work" OR priority = 5
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .Or(new NumberField("priority").Equals(5)));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task QueryArrayFilterOrWithMixedExpressionTypesShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "personal", Value = "bob@example.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "business", Value = "charlie@test.com" }
        ], Ct.None);

        // Filter: emails where (type = "work") OR (value contains "@example.com")
        // Should match: Alice (has work), Bob (has @example.com)
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .Or(new StringField("value").Contains("@example.com")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Bob");
    }

    [Fact]
    public async Task QueryArrayFilterOrWithMultipleContainsExpressionsShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "User1", [
            new EmailAddress { Type = "work", Value = "user1@company.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "User2", [
            new EmailAddress { Type = "personal", Value = "user2@example.org" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "User3", [
            new EmailAddress { Type = "other", Value = "user3@test.net" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "User4", [
            new EmailAddress { Type = "business", Value = "user4@other.com" }
        ], Ct.None);

        // Filter: emails where value contains ".com" OR value contains ".org"
        var filter = Query.ArrayFilter("emails",
            new StringField("value").Contains(".com")
                .Or(new StringField("value").Contains(".org")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(x => x.Value.Name == "User1");
        result.Items.ShouldContain(x => x.Value.Name == "User2");
        result.Items.ShouldContain(x => x.Value.Name == "User4");
    }

    [Fact]
    public async Task QueryArrayFilterOrWithMultipleStartsWithExpressionsShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "AdminUser", [
            new EmailAddress { Type = "admin", Value = "admin@system.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "SupportUser", [
            new EmailAddress { Type = "support", Value = "support@system.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "RegularUser", [
            new EmailAddress { Type = "user", Value = "user@system.com" }
        ], Ct.None);

        // Filter: emails where value starts with "admin@" OR value starts with "support@"
        var filter = Query.ArrayFilter("emails",
            new StringField("value").StartsWith("admin@")
                .Or(new StringField("value").StartsWith("support@")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "AdminUser");
        result.Items.ShouldContain(x => x.Value.Name == "SupportUser");
    }

    [Fact]
    public async Task QueryArrayFilterComplexOrWithThreeBranchesShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "User1", [
            new EmailAddress { Type = "work", Value = "user1@company.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "User2", [
            new EmailAddress { Type = "personal", Value = "user2@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "User3", [
            new EmailAddress { Type = "business", Value = "user3@biz.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "User4", [
            new EmailAddress { Type = "other", Value = "user4@test.com" }
        ], Ct.None);

        // Filter: emails where type="work" OR type="personal" OR type="business"
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .Or(new StringField("type").Equals("personal"))
                .Or(new StringField("type").Equals("business")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(x => x.Value.Name == "User1");
        result.Items.ShouldContain(x => x.Value.Name == "User2");
        result.Items.ShouldContain(x => x.Value.Name == "User3");
    }

    [Fact]
    public async Task QueryArrayFilterOrCombinedWithInExpressionShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "TeamA", [
            new EmailAddress { Type = "work", Value = "teama@company.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "TeamB", [
            new EmailAddress { Type = "business", Value = "teamb@company.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "TeamC", [
            new EmailAddress { Type = "contractor", Value = "teamc@contractor.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "External", [
            new EmailAddress { Type = "external", Value = "ext@external.com" }
        ], Ct.None);

        // Filter: emails where type IN ("work", "business") OR value contains "contractor"
        var filter = Query.ArrayFilter("emails",
            new StringField("type").In(WorkBusinessTypes)
                .Or(new StringField("value").Contains("contractor")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(x => x.Value.Name == "TeamA");
        result.Items.ShouldContain(x => x.Value.Name == "TeamB");
        result.Items.ShouldContain(x => x.Value.Name == "TeamC");
    }

    [Fact]
    public async Task QueryArrayFilterWithPagingShouldHandlePageBreaksAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 15 users with work emails
        for (var i = 1; i <= 15; i++)
        {
            _ = await CreateUserWithEmailsAsync(store, $"User{i:D2}", [
                new EmailAddress { Type = "work", Value = $"user{i}@work.com" },
                new EmailAddress { Type = "personal", Value = $"user{i}@home.com" }
            ], Ct.None);
        }

        // Add 5 users without work emails
        for (var i = 16; i <= 20; i++)
        {
            _ = await CreateUserWithEmailsAsync(store, $"User{i:D2}", [
                new EmailAddress { Type = "personal", Value = $"user{i}@home.com" }
            ], Ct.None);
        }

        var filter = Query.ArrayFilter("emails", new StringField("type").Equals("work"));
        var sort = new SortParameter(new StringField("name"));

        // Act - Get pages with size 5 (should be 3 pages: 5+5+5)
        var page1 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 5), Ct.None);
        var page2 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 5), Ct.None);
        var page3 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 5), Ct.None);
        var page4 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(4, 5), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(5);
        page1.TotalCount.ShouldBe(15);
        page1.HasMoreData.ShouldBeTrue();
        page1.Items[0].Value.Name.ShouldBe("User01");
        page1.Items[4].Value.Name.ShouldBe("User05");

        page2.Items.Count.ShouldBe(5);
        page2.HasMoreData.ShouldBeTrue();
        page2.Items[0].Value.Name.ShouldBe("User06");
        page2.Items[4].Value.Name.ShouldBe("User10");

        page3.Items.Count.ShouldBe(5);
        page3.HasMoreData.ShouldBeFalse();
        page3.Items[0].Value.Name.ShouldBe("User11");
        page3.Items[4].Value.Name.ShouldBe("User15");

        page4.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryArrayFilterComplexConditionPartialPageAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 14 users with various email patterns
        for (var i = 1; i <= 14; i++)
        {
            var emails = new List<EmailAddress>
            {
                new EmailAddress { Type = "work", Value = $"user{i}@company.com" }
            };

            // Every 3rd user gets an @example.com work email
            if (i % 3 == 0)
            {
                emails.Add(new EmailAddress { Type = "work", Value = $"user{i}@example.com" });
            }

            _ = await CreateUserWithEmailsAsync(store, $"User{i:D2}", emails.ToArray(), Ct.None);
        }

        // Filter: work emails containing "@example.com" (should match users 3, 6, 9, 12)
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .And(new StringField("value").Contains("@example.com")));
        var sort = new SortParameter(new StringField("name"));

        // Act - Page size 3 creates 2 pages (3+1)
        var page1 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 3), Ct.None);
        var page2 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 3), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(3);
        page1.TotalCount.ShouldBe(4);
        page1.Items[0].Value.Name.ShouldBe("User03");
        page1.Items[1].Value.Name.ShouldBe("User06");
        page1.Items[2].Value.Name.ShouldBe("User09");

        page2.Items.Count.ShouldBe(1); // Partial last page
        page2.Items[0].Value.Name.ShouldBe("User12");
        page2.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryArrayFilterWithOtherFilterAndPagingShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create users A1-A10 and B1-B10, all with work emails
        for (var i = 1; i <= 10; i++)
        {
            _ = await CreateUserWithEmailsAsync(store, $"A{i:D2}", [
                new EmailAddress { Type = "work", Value = $"a{i}@work.com" }
            ], Ct.None);
            _ = await CreateUserWithEmailsAsync(store, $"B{i:D2}", [
                new EmailAddress { Type = "work", Value = $"b{i}@work.com" }
            ], Ct.None);
        }

        // Filter: name starts with "A" AND has work email (10 results)
        var filter = new StringField("name").StartsWith("A")
            .And(Query.ArrayFilter("emails", new StringField("type").Equals("work")));
        var sort = new SortParameter(new StringField("name"));

        // Act - Page size 4 creates 3 pages (4+4+2)
        var page1 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 4), Ct.None);
        var page2 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 4), Ct.None);
        var page3 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 4), Ct.None);

        // Assert
        page1.TotalCount.ShouldBe(10);
        page1.Items.Count.ShouldBe(4);
        page1.Items.ShouldAllBe(u => u.Value.Name.StartsWith('A'));

        page2.Items.Count.ShouldBe(4);
        page2.Items.ShouldAllBe(u => u.Value.Name.StartsWith('A'));

        page3.Items.Count.ShouldBe(2);
        page3.Items.ShouldAllBe(u => u.Value.Name.StartsWith('A'));
    }

    [Fact]
    public async Task QueryArrayFilterOrConditionAcrossPagesShouldMaintainConsistencyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create 8 users with work emails, 8 with business emails
        for (var i = 1; i <= 8; i++)
        {
            _ = await CreateUserWithEmailsAsync(store, $"WorkUser{i:D2}", [
                new EmailAddress { Type = "work", Value = $"work{i}@company.com" }
            ], Ct.None);
            _ = await CreateUserWithEmailsAsync(store, $"BizUser{i:D2}", [
                new EmailAddress { Type = "business", Value = $"biz{i}@company.com" }
            ], Ct.None);
        }

        // Filter: type = "work" OR type = "business" (16 results)
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .Or(new StringField("type").Equals("business")));
        var sort = new SortParameter(new StringField("name"));

        // Act - Page size 6 creates 3 pages (6+6+4)
        var page1 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 6), Ct.None);
        var page2 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 6), Ct.None);
        var page3 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 6), Ct.None);

        // Assert
        page1.TotalCount.ShouldBe(16);
        page1.Items.Count.ShouldBe(6);

        page2.Items.Count.ShouldBe(6);

        page3.Items.Count.ShouldBe(4);
        page3.HasMoreData.ShouldBeFalse();

        // Verify all results have either work or business email
        var allItems = page1.Items.Concat(page2.Items).Concat(page3.Items).ToList();
        allItems.Count.ShouldBe(16);
    }

    [Fact]
    public async Task QueryArrayFilterNoMatchesShouldReturnEmptyPagesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "User1", [
            new EmailAddress { Type = "personal", Value = "user1@home.com" }
        ], Ct.None);
        _ = await CreateUserWithEmailsAsync(store, "User2", [
            new EmailAddress { Type = "personal", Value = "user2@home.com" }
        ], Ct.None);

        // Filter for work emails (no matches)
        var filter = Query.ArrayFilter("emails", new StringField("type").Equals("work"));

        // Act
        var page1 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 10), Ct.None);
        var page2 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, DataRange.FromPage(2, 10), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(0);
        page1.TotalCount.ShouldBe(0);
        page1.HasMoreData.ShouldBeFalse();

        page2.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryArrayFilterExactPageBoundaryShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        // Create exactly 12 users with work emails (exactly 3 pages of 4)
        for (var i = 1; i <= 12; i++)
        {
            _ = await CreateUserWithEmailsAsync(store, $"User{i:D2}", [
                new EmailAddress { Type = "work", Value = $"user{i}@work.com" }
            ], Ct.None);
        }

        var filter = Query.ArrayFilter("emails", new StringField("type").Equals("work"));
        var sort = new SortParameter(new StringField("name"));

        // Act
        var page1 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 4), Ct.None);
        var page2 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 4), Ct.None);
        var page3 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 4), Ct.None);
        var page4 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(4, 4), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(4);
        page1.Items[0].Value.Name.ShouldBe("User01");

        page2.Items.Count.ShouldBe(4);
        page2.Items[0].Value.Name.ShouldBe("User05");

        page3.Items.Count.ShouldBe(4); // Full last page (not partial)
        page3.Items[0].Value.Name.ShouldBe("User09");
        page3.Items[3].Value.Name.ShouldBe("User12");
        page3.HasMoreData.ShouldBeFalse();

        // Page 4 should be empty
        page4.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task QueryArrayFilterSmallPagesShouldIterateCorrectlyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 1; i <= 5; i++)
        {
            _ = await CreateUserWithEmailsAsync(store, $"User{i}", [
                new EmailAddress { Type = "work", Value = $"user{i}@work.com" }
            ], Ct.None);
        }

        var filter = Query.ArrayFilter("emails", new StringField("type").Equals("work"));
        var sort = new SortParameter(new StringField("name"));

        // Act - Page size of 2 creates 3 pages (2+2+1)
        var page1 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(1, 2), Ct.None);
        var page2 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(2, 2), Ct.None);
        var page3 = await store.QueryAsync<TestUserDso>(_testEntityType, filter, sort, DataRange.FromPage(3, 2), Ct.None);

        // Assert
        page1.Items.Count.ShouldBe(2);
        page1.Items[0].Value.Name.ShouldBe("User1");
        page1.Items[1].Value.Name.ShouldBe("User2");

        page2.Items.Count.ShouldBe(2);
        page2.Items[0].Value.Name.ShouldBe("User3");
        page2.Items[1].Value.Name.ShouldBe("User4");

        page3.Items.Count.ShouldBe(1);
        page3.Items[0].Value.Name.ShouldBe("User5");
        page3.HasMoreData.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryArrayFilterWithEndsWithShouldMatchArrayItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" },
            new EmailAddress { Type = "personal", Value = "alice@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "work", Value = "bob@company.org" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "personal", Value = "charlie@work.com" }
        ], Ct.None);

        // Filter: emails where value ends with "@work.com"
        var filter = Query.ArrayFilter("emails", new StringField("value").EndsWith("@work.com"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Alice (alice@work.com) and Charlie (charlie@work.com) match
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task QueryArrayFilterEndsWithWithAndConditionShouldMatchSameArrayItemAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@company.com" },
            new EmailAddress { Type = "personal", Value = "alice@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "personal", Value = "bob@company.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "work", Value = "charlie@other.org" }
        ], Ct.None);

        // Filter: emails where type="work" AND value ends with "@company.com"
        // Only Alice has a work email ending with @company.com
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .And(new StringField("value").EndsWith("@company.com")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Alice");
    }

    [Fact]
    public async Task QueryArrayFilterWithNotEqualShouldExcludeMatchingItemsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" },
            new EmailAddress { Type = "personal", Value = "alice@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "personal", Value = "bob@home.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "personal", Value = "charlie@home.com" }
        ], Ct.None);

        // Filter: emails where NOT(type = "personal")
        // Alice has a non-personal email (work), Bob and Charlie only have personal emails
        var filter = Query.ArrayFilter("emails", Query.Not(new StringField("type").Equals("personal")));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Only Alice has an email where type != "personal"
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Alice");
    }

    [Fact]
    public async Task QueryArrayFilterNotCombinedWithTopLevelFilterShouldWorkAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "work", Value = "bob@work.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Charlie", [
            new EmailAddress { Type = "personal", Value = "charlie@home.com" }
        ], Ct.None);

        // NOT(emails where type = "work") - excludes users who have a work email
        var arrayFilter = Query.ArrayFilter("emails", new StringField("type").Equals("work"));
        var filter = Query.Not(arrayFilter);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Only Charlie has no work email
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Charlie");
    }

    [Fact]
    public async Task QueryArrayFilterPresentShouldMatchEntitiesWithArrayItemsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateUserWithEmailsAsync(store, "Alice", [
            new EmailAddress { Type = "work", Value = "alice@work.com" }
        ], Ct.None);

        _ = await CreateUserWithEmailsAsync(store, "Bob", [
            new EmailAddress { Type = "personal", Value = "bob@home.com" }
        ], Ct.None);

        // Charlie has no emails
        _ = await CreateUserWithEmailsAsync(store, "Charlie", [], Ct.None);

        // Filter: emails where value is present (any email value exists)
        var filter = Query.ArrayFilter("emails", new StringField("value").Present());
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_testEntityType, filter, SortParameter.Empty, page, Ct.None);

        // Assert - Alice and Bob have email entries, Charlie has none
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Bob");
    }

}
