// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Filtering;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;

namespace Duende.Storage.IntegrationTests;

public partial class FilterTranslatorIntegrationTests
{


    private readonly EntityType _testEntityType = new(3, "TestEntity");

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private sealed class TestEntityAttributeResolver : IScimAttributeTypeResolver
    {
        public Field ResolveField(string attributePath) => attributePath.ToLowerInvariant() switch
        {
            "name" => new StringField("name"),
            "score" => new NumberField("score"),
            "price" => new NumberField("price"),
            "recordedat" => new DateTimeField("recordedAt"),
            "lastlogin" => new DateTimeField("lastLogin"),
            "isactive" => new BooleanField("isActive"),
            "status" => new StringField("status"),
            _ => throw new NotSupportedException($"Unknown attribute: {attributePath}")
        };
    }

    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestEntityDso>();
            services.AddDsoRegistration<TestUserDso>();
            services.AddDsoRegistration<TestSortDso>();
        });

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

    private static async Task<QueryResult<MetadataEnvelope<TestEntityDso>>> TranslateAndQueryAsync(
        IStore store, string filterString, EntityType entityType)
    {
        var resolver = new TestEntityAttributeResolver();
        var translator = new FilterTranslator(resolver);
        var filter = translator.Translate(filterString)!;
        var page = DataRange.FromPage(1, 10);
        return await store.QueryAsync<TestEntityDso>(entityType, filter, SortParameter.Empty, page, Ct.None);
    }

    [Fact]
    public async Task FilterAndQuerySimpleEqualityAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice");
        _ = await CreateTestEntityAsync(store, "Bob");
        _ = await CreateTestEntityAsync(store, "Charlie");

        var result = await TranslateAndQueryAsync(store, "name eq \"Alice\"", _testEntityType);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Alice");
    }

    [Fact]
    public async Task FilterAndQueryNotEqualAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice");
        _ = await CreateTestEntityAsync(store, "Bob");
        _ = await CreateTestEntityAsync(store, "Charlie");

        var result = await TranslateAndQueryAsync(store, "name ne \"Alice\"", _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Bob");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task FilterAndQueryContainsAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice Smith");
        _ = await CreateTestEntityAsync(store, "Bob Jones");
        _ = await CreateTestEntityAsync(store, "Charlie Smith");

        var result = await TranslateAndQueryAsync(store, "name co \"Smith\"", _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice Smith");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie Smith");
    }

    [Fact]
    public async Task FilterAndQueryStartsWithAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alpha");
        _ = await CreateTestEntityAsync(store, "Beta");
        _ = await CreateTestEntityAsync(store, "Alpha Centauri");

        var result = await TranslateAndQueryAsync(store, "name sw \"Alpha\"", _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alpha");
        result.Items.ShouldContain(x => x.Value.Name == "Alpha Centauri");
    }

    [Fact]
    public async Task FilterAndQueryEndsWithAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice Smith");
        _ = await CreateTestEntityAsync(store, "Bob Jones");
        _ = await CreateTestEntityAsync(store, "John Smith");

        var result = await TranslateAndQueryAsync(store, "name ew \"Smith\"", _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice Smith");
        result.Items.ShouldContain(x => x.Value.Name == "John Smith");
    }

    [Fact]
    public async Task FilterAndQueryPresentAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "WithScore1", score: 80);
        _ = await CreateTestEntityAsync(store, "NoScore");
        _ = await CreateTestEntityAsync(store, "WithScore2", score: 50);

        var result = await TranslateAndQueryAsync(store, "score pr", _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "WithScore1");
        result.Items.ShouldContain(x => x.Value.Name == "WithScore2");
    }

    [Fact]
    public async Task FilterAndQueryBooleanEqualityAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Active", isActive: true);
        _ = await CreateTestEntityAsync(store, "Inactive", isActive: false);
        _ = await CreateTestEntityAsync(store, "Unknown");

        var result = await TranslateAndQueryAsync(store, "isActive eq true", _testEntityType);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Active");
    }

    [Fact]
    public async Task FilterAndQueryNumberGreaterThanAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Low", score: 50);
        _ = await CreateTestEntityAsync(store, "Boundary", score: 75);
        _ = await CreateTestEntityAsync(store, "High", score: 100);

        var result = await TranslateAndQueryAsync(store, "score gt 75", _testEntityType);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("High");
    }

    [Fact]
    public async Task FilterAndQueryNumberLessOrEqualAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Low", score: 25);
        _ = await CreateTestEntityAsync(store, "Boundary", score: 50);
        _ = await CreateTestEntityAsync(store, "High", score: 75);

        var result = await TranslateAndQueryAsync(store, "score le 50", _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Low");
        result.Items.ShouldContain(x => x.Value.Name == "Boundary");
    }

    [Fact]
    public async Task FilterAndQueryNumberEqualDecimalAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Cheap", price: 19.99m);
        _ = await CreateTestEntityAsync(store, "Expensive", price: 29.99m);

        var result = await TranslateAndQueryAsync(store, "price eq 19.99", _testEntityType);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Cheap");
    }

    [Fact]
    public async Task FilterAndQueryDateTimeGreaterThanAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "May", createdAt: new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero));
        _ = await CreateTestEntityAsync(store, "July", createdAt: new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero));
        _ = await CreateTestEntityAsync(store, "December", createdAt: new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero));

        var result = await TranslateAndQueryAsync(store, "recordedAt gt \"2024-06-01T00:00:00Z\"", _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "July");
        result.Items.ShouldContain(x => x.Value.Name == "December");
    }

    [Fact]
    public async Task FilterAndQueryAndCombinationAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "User1", score: 30);
        _ = await CreateTestEntityAsync(store, "User2", score: 80);
        _ = await CreateTestEntityAsync(store, "Admin1", score: 90);

        var result = await TranslateAndQueryAsync(store, "name sw \"User\" and score gt 50", _testEntityType);

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("User2");
    }

    [Fact]
    public async Task FilterAndQueryOrCombinationAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "ActiveEntity", status: "active");
        _ = await CreateTestEntityAsync(store, "PendingEntity", status: "pending");
        _ = await CreateTestEntityAsync(store, "ArchivedEntity", status: "archived");

        var result = await TranslateAndQueryAsync(store, "status eq \"active\" or status eq \"pending\"", _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "ActiveEntity");
        result.Items.ShouldContain(x => x.Value.Name == "PendingEntity");
    }

    [Fact]
    public async Task FilterAndQueryNotExpressionAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "Alice");
        _ = await CreateTestEntityAsync(store, "Bob");
        _ = await CreateTestEntityAsync(store, "Charlie");

        var result = await TranslateAndQueryAsync(store, "not (name eq \"Alice\")", _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Bob");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task FilterAndQueryComplexNestedAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "A", score: 80, isActive: true, status: "standard");
        _ = await CreateTestEntityAsync(store, "B", score: 30, isActive: true, status: "premium");
        _ = await CreateTestEntityAsync(store, "C", score: 80, isActive: false, status: "standard");
        _ = await CreateTestEntityAsync(store, "D", score: 20, isActive: false, status: "basic");

        var result = await TranslateAndQueryAsync(
            store,
            "(score gt 50 and isActive eq true) or status eq \"premium\"",
            _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "A");
        result.Items.ShouldContain(x => x.Value.Name == "B");
    }

    [Fact]
    public async Task FilterAndQueryPrecedenceAndBindsTighterThanOrAsync()
    {
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateTestEntityAsync(store, "a", score: 10);
        _ = await CreateTestEntityAsync(store, "b", score: 80);
        _ = await CreateTestEntityAsync(store, "c", score: 90);

        var result = await TranslateAndQueryAsync(
            store,
            "name eq \"a\" or name eq \"b\" and score gt 50",
            _testEntityType);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "a");
        result.Items.ShouldContain(x => x.Value.Name == "b");
    }
}
