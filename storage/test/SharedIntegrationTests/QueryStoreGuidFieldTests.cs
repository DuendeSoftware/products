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
/// Tests for GuidField, ExactMatchField, and the guid_value column across all store implementations.
/// Covers:
/// - GuidField: Equals, In, Present
/// - ExactMatchField: Equals, In, Present, case-insensitivity
/// - StringField.Equals/In routing through guid_value (deterministic hash)
/// - Logical composition (And, Or, Not) with guid-based fields
/// - Array fields with guid_value
/// </summary>
public partial class QueryStoreGuidFieldTests
{

    private readonly EntityType _guidEntityType = new(6, "GuidTestEntity");
    private readonly EntityType _userEntityType = new(2, "UserEntity");

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private async Task<IStoreFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestGuidEntityDso>();
            services.AddDsoRegistration<TestUserDso>();
        });

    private static async Task<UuidV7> CreateGuidEntityAsync(
        IStore store,
        string name,
        Guid? resourceId = null,
        string? apiKey = null,
        string? tag = null,
        Ct ct = default)
    {
        var id = UuidV7.New();
        var dso = new TestGuidEntityDso
        {
            Name = name,
            ResourceId = resourceId,
            ApiKey = apiKey,
            Tag = tag
        };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);

        if (resourceId.HasValue)
        {
            _ = searchFieldsBuilder.Add("resourceId", resourceId.Value);
        }

        if (apiKey != null)
        {
            _ = searchFieldsBuilder.AddExactMatch("apiKey", apiKey);
        }

        if (tag != null)
        {
            _ = searchFieldsBuilder.Add("tag", tag);
        }

        var searchFields = searchFieldsBuilder.Build();
        var storeInterface = store;
        var result = await storeInterface.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id;
    }

    private static async Task<UuidV7> CreateUserWithEmailsAndGuidsAsync(
        IStore store,
        string name,
        (string type, string value, Guid? correlationId)[] emails,
        Ct ct)
    {
        var id = UuidV7.New();
        var dso = new TestUserDso
        {
            Name = name,
            Emails = emails.Select(e => new EmailAddress { Type = e.type, Value = e.value }).ToArray()
        };

        var searchFieldsBuilder = new SearchFieldsBuilder();
        _ = searchFieldsBuilder.Add("name", name);

        for (var i = 0; i < emails.Length; i++)
        {
            _ = searchFieldsBuilder.Add("emails.type", i, emails[i].type);
            _ = searchFieldsBuilder.Add("emails.value", i, emails[i].value);
            if (emails[i].correlationId is { } correlationId)
            {
                _ = searchFieldsBuilder.Add("emails.correlationId", i, correlationId);
            }
        }

        var searchFields = searchFieldsBuilder.Build();
        var storeInterface = store;
        var result = await storeInterface.CreateAsync(id, dso, Array.Empty<DataStorageKey>(), searchFields, Expiration.NoExpiration, [], ct);
        result.ShouldBe(CreateResult.Success);
        return id;
    }

    [Fact]
    public async Task GuidFieldEqualsShouldReturnExactMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();

        _ = await CreateGuidEntityAsync(store, "Entity1", resourceId: guid1);
        _ = await CreateGuidEntityAsync(store, "Entity2", resourceId: guid2);
        _ = await CreateGuidEntityAsync(store, "Entity3", resourceId: guid3);

        var filter = new GuidField("resourceId").Equals(guid2);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Entity2");
    }

    [Fact]
    public async Task GuidFieldEqualsWithNoMatchShouldReturnEmptyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Entity1", resourceId: Guid.NewGuid());

        var filter = new GuidField("resourceId").Equals(Guid.NewGuid());
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GuidFieldInShouldReturnMatchingEntitiesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();

        _ = await CreateGuidEntityAsync(store, "Entity1", resourceId: guid1);
        _ = await CreateGuidEntityAsync(store, "Entity2", resourceId: guid2);
        _ = await CreateGuidEntityAsync(store, "Entity3", resourceId: guid3);

        Guid[] searchGuids = [guid1, guid3];
        var filter = new GuidField("resourceId").In(searchGuids);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Entity1");
        result.Items.ShouldContain(x => x.Value.Name == "Entity3");
    }

    [Fact]
    public async Task GuidFieldPresentShouldReturnEntitiesWithFieldSetAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "WithGuid", resourceId: Guid.NewGuid());
        _ = await CreateGuidEntityAsync(store, "WithoutGuid");
        _ = await CreateGuidEntityAsync(store, "AlsoWithGuid", resourceId: Guid.NewGuid());

        var filter = new GuidField("resourceId").Present();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "WithGuid");
        result.Items.ShouldContain(x => x.Value.Name == "AlsoWithGuid");
    }

    [Fact]
    public async Task ExactMatchFieldEqualsShouldReturnExactMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Entity1", apiKey: "secret-key-123");
        _ = await CreateGuidEntityAsync(store, "Entity2", apiKey: "different-key-456");
        _ = await CreateGuidEntityAsync(store, "Entity3", apiKey: "another-key-789");

        var filter = new ExactMatchField("apiKey").Equals("secret-key-123");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Entity1");
    }

    [Fact]
    public async Task ExactMatchFieldEqualsShouldBeCaseInsensitiveAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Entity1", apiKey: "My-Secret-Key");
        _ = await CreateGuidEntityAsync(store, "Entity2", apiKey: "other-key");

        // Act — query with different casing than what was stored
        var filter = new ExactMatchField("apiKey").Equals("MY-SECRET-KEY");
        var page = DataRange.FromPage(1, 10);
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert — should match because both are uppercased before hashing
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Entity1");
    }

    [Fact]
    public async Task ExactMatchFieldEqualsWithLowerCaseQueryShouldMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Entity1", apiKey: "ABC-DEF");

        // Query with lowercase
        var filter = new ExactMatchField("apiKey").Equals("abc-def");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Entity1");
    }

    [Fact]
    public async Task ExactMatchFieldInShouldReturnMatchingEntitiesAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Entity1", apiKey: "key-alpha");
        _ = await CreateGuidEntityAsync(store, "Entity2", apiKey: "key-beta");
        _ = await CreateGuidEntityAsync(store, "Entity3", apiKey: "key-gamma");
        _ = await CreateGuidEntityAsync(store, "Entity4", apiKey: "key-delta");

        string[] searchKeys = ["key-alpha", "key-gamma", "key-delta"];
        var filter = new ExactMatchField("apiKey").In(searchKeys);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(x => x.Value.Name == "Entity1");
        result.Items.ShouldContain(x => x.Value.Name == "Entity3");
        result.Items.ShouldContain(x => x.Value.Name == "Entity4");
    }

    [Fact]
    public async Task ExactMatchFieldInShouldBeCaseInsensitiveAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Entity1", apiKey: "Key-Alpha");
        _ = await CreateGuidEntityAsync(store, "Entity2", apiKey: "KEY-BETA");

        // Query with mixed casing
        string[] searchKeys = ["key-alpha", "key-beta"];
        var filter = new ExactMatchField("apiKey").In(searchKeys);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Entity1");
        result.Items.ShouldContain(x => x.Value.Name == "Entity2");
    }

    [Fact]
    public async Task ExactMatchFieldPresentShouldReturnEntitiesWithFieldSetAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "WithApiKey", apiKey: "some-key");
        _ = await CreateGuidEntityAsync(store, "WithoutApiKey");
        _ = await CreateGuidEntityAsync(store, "AlsoWithApiKey", apiKey: "another-key");

        var filter = new ExactMatchField("apiKey").Present();
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "WithApiKey");
        result.Items.ShouldContain(x => x.Value.Name == "AlsoWithApiKey");
    }

    [Fact]
    public async Task ExactMatchFieldEqualsWithNoMatchShouldReturnEmptyAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Entity1", apiKey: "existing-key");

        var filter = new ExactMatchField("apiKey").Equals("nonexistent-key");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ExactMatchFieldWithOrShouldMatchEitherAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Entity1", apiKey: "key-a");
        _ = await CreateGuidEntityAsync(store, "Entity2", apiKey: "key-b");
        _ = await CreateGuidEntityAsync(store, "Entity3", apiKey: "key-c");

        var filter = new ExactMatchField("apiKey").Equals("key-a")
            .Or(new ExactMatchField("apiKey").Equals("key-c"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Entity1");
        result.Items.ShouldContain(x => x.Value.Name == "Entity3");
    }

    [Fact]
    public async Task NotGuidFieldEqualsShouldExcludeMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var excludeGuid = Guid.NewGuid();
        _ = await CreateGuidEntityAsync(store, "Excluded", resourceId: excludeGuid);
        _ = await CreateGuidEntityAsync(store, "Included1", resourceId: Guid.NewGuid());
        _ = await CreateGuidEntityAsync(store, "Included2", resourceId: Guid.NewGuid());

        var filter = Query.Not(new GuidField("resourceId").Equals(excludeGuid));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Included1");
        result.Items.ShouldContain(x => x.Value.Name == "Included2");
    }

    [Fact]
    public async Task NotExactMatchFieldEqualsShouldExcludeMatchAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Excluded", apiKey: "banned-key");
        _ = await CreateGuidEntityAsync(store, "Included1", apiKey: "good-key");
        _ = await CreateGuidEntityAsync(store, "Included2", apiKey: "another-key");

        var filter = Query.Not(new ExactMatchField("apiKey").Equals("banned-key"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Included1");
        result.Items.ShouldContain(x => x.Value.Name == "Included2");
    }

    [Fact]
    public async Task GuidFieldAndStringFieldShouldCombineAsync()
    {
        // Arrange — tests combining GuidField with regular StringField queries
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var targetGuid = Guid.NewGuid();
        _ = await CreateGuidEntityAsync(store, "Match", resourceId: targetGuid, tag: "premium");
        _ = await CreateGuidEntityAsync(store, "WrongTag", resourceId: targetGuid, tag: "standard");
        _ = await CreateGuidEntityAsync(store, "WrongGuid", resourceId: Guid.NewGuid(), tag: "premium");

        var filter = new GuidField("resourceId").Equals(targetGuid)
            .And(new StringField("tag").Equals("premium"));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Match");
    }

    [Fact]
    public async Task ArrayGuidFieldEqualsShouldMatchWithinArrayItemsAsync()
    {
        // Arrange — GuidField within array items (e.g., emails with correlation IDs)
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var targetCorrelationId = Guid.NewGuid();
        var otherCorrelationId = Guid.NewGuid();

        _ = await CreateUserWithEmailsAndGuidsAsync(store, "Alice",
        [
            ("work", "alice@work.com", targetCorrelationId),
            ("personal", "alice@home.com", otherCorrelationId)
        ], _ct);

        _ = await CreateUserWithEmailsAndGuidsAsync(store, "Bob",
        [
            ("work", "bob@work.com", Guid.NewGuid()),
            ("personal", "bob@home.com", Guid.NewGuid())
        ], _ct);

        var filter = Query.ArrayFilter("emails",
            new GuidField("correlationId").Equals(targetCorrelationId));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_userEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Alice");
    }

    [Fact]
    public async Task ArrayGuidFieldInShouldMatchWithinArrayItemsAsync()
    {
        // Arrange
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var correlationId1 = Guid.NewGuid();
        var correlationId2 = Guid.NewGuid();
        var correlationId3 = Guid.NewGuid();

        _ = await CreateUserWithEmailsAndGuidsAsync(store, "Alice",
        [
            ("work", "alice@work.com", correlationId1)
        ], _ct);

        _ = await CreateUserWithEmailsAndGuidsAsync(store, "Bob",
        [
            ("work", "bob@work.com", correlationId2)
        ], _ct);

        _ = await CreateUserWithEmailsAndGuidsAsync(store, "Charlie",
        [
            ("work", "charlie@work.com", correlationId3)
        ], _ct);

        Guid[] searchGuids = [correlationId1, correlationId3];
        var filter = Query.ArrayFilter("emails",
            new GuidField("correlationId").In(searchGuids));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_userEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Alice");
        result.Items.ShouldContain(x => x.Value.Name == "Charlie");
    }

    [Fact]
    public async Task ArrayGuidFieldWithStringFieldAndShouldMatchSameArrayItemAsync()
    {
        // Arrange — combines GuidField and StringField within the same array item
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var targetCorrelationId = Guid.NewGuid();

        // Alice: work email has target correlation ID
        _ = await CreateUserWithEmailsAndGuidsAsync(store, "Alice",
        [
            ("work", "alice@work.com", targetCorrelationId),
            ("personal", "alice@home.com", Guid.NewGuid())
        ], _ct);

        // Bob: has target correlation ID but on a personal email, not work
        _ = await CreateUserWithEmailsAndGuidsAsync(store, "Bob",
        [
            ("work", "bob@work.com", Guid.NewGuid()),
            ("personal", "bob@home.com", targetCorrelationId)
        ], _ct);

        // Query: array item must have BOTH type="work" AND correlationId=target
        var filter = Query.ArrayFilter("emails",
            new StringField("type").Equals("work")
                .And(new GuidField("correlationId").Equals(targetCorrelationId)));
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestUserDso>(_userEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert — Only Alice has a work email with the target correlation ID
        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.Name.ShouldBe("Alice");
    }

    [Fact]
    public async Task ExactMatchFieldWithDuplicateValuesShouldReturnAllAsync()
    {
        // Arrange — multiple entities with the same exact-match value
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        _ = await CreateGuidEntityAsync(store, "Entity1", apiKey: "shared-key");
        _ = await CreateGuidEntityAsync(store, "Entity2", apiKey: "shared-key");
        _ = await CreateGuidEntityAsync(store, "Entity3", apiKey: "different-key");

        var filter = new ExactMatchField("apiKey").Equals("shared-key");
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Entity1");
        result.Items.ShouldContain(x => x.Value.Name == "Entity2");
    }

    [Fact]
    public async Task GuidFieldWithSameGuidAcrossEntitiesShouldReturnAllAsync()
    {
        // Arrange — multiple entities sharing the same GUID value
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        var sharedGuid = Guid.NewGuid();
        _ = await CreateGuidEntityAsync(store, "Entity1", resourceId: sharedGuid);
        _ = await CreateGuidEntityAsync(store, "Entity2", resourceId: sharedGuid);
        _ = await CreateGuidEntityAsync(store, "Entity3", resourceId: Guid.NewGuid());

        var filter = new GuidField("resourceId").Equals(sharedGuid);
        var page = DataRange.FromPage(1, 10);

        // Act
        var result = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, SortParameter.Empty, page, _ct);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Value.Name == "Entity1");
        result.Items.ShouldContain(x => x.Value.Name == "Entity2");
    }

    [Fact]
    public async Task GuidFieldCursorPaginationFirstPageShouldReturnNextTokenAsync()
    {
        // Arrange — create 5 entities with distinct GUIDs, paginate with page size 2
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 0; i < 5; i++)
        {
            _ = await CreateGuidEntityAsync(store, $"Item{i}", resourceId: Guid.NewGuid(), ct: _ct);
        }

        var filter = new GuidField("resourceId").Present();
        var sort = new SortParameter(new GuidField("resourceId"));
        var cursor = DataRange.FromContinuationToken(ContinuationToken.Beginning, 2);

        // Act
        var page1 = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, sort, cursor, _ct);

        // Assert
        page1.Items.Count.ShouldBe(2);
        _ = page1.NextToken.ShouldNotBeNull();
        page1.HasMoreData.ShouldBeTrue();
    }

    [Fact]
    public async Task GuidFieldCursorPaginationContinuationShouldReturnAllItemsAsync()
    {
        // Arrange — create 5 entities, paginate through all with page size 2
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 0; i < 5; i++)
        {
            _ = await CreateGuidEntityAsync(store, $"Item{i}", resourceId: Guid.NewGuid(), ct: _ct);
        }

        var filter = new GuidField("resourceId").Present();
        var sort = new SortParameter(new GuidField("resourceId"));

        // Act — page through all results
        var allItems = new List<MetadataEnvelope<TestGuidEntityDso>>();
        ContinuationToken? token = null;
        var pageCount = 0;

        do
        {
            var cursor = DataRange.FromContinuationToken(token?.Value ?? ContinuationToken.Beginning, 2);
            var page = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, sort, cursor, _ct);
            allItems.AddRange(page.Items);
            token = page.NextToken;
            pageCount++;

            if (!page.HasMoreData || pageCount > 10)
            {
                break;
            }
        }
        while (true);

        // Assert — all 5 items returned across pages, no duplicates
        allItems.Count.ShouldBe(5);
        allItems.Select(x => x.Value.Name).Distinct().Count().ShouldBe(5);
        pageCount.ShouldBe(3); // 2 + 2 + 1
    }

    [Fact]
    public async Task ExactMatchFieldCursorPaginationShouldWorkAsync()
    {
        // Arrange — create 5 entities with distinct api keys, paginate with ExactMatchField sort
        await using var fixture = await CreateProviderAsync();
        var store = fixture.Store;

        for (var i = 0; i < 5; i++)
        {
            _ = await CreateGuidEntityAsync(store, $"Item{i}", apiKey: $"key-{i:D3}", ct: _ct);
        }

        var filter = new ExactMatchField("apiKey").Present();
        var sort = new SortParameter(new ExactMatchField("apiKey"));

        // Act — page through all results
        var allItems = new List<MetadataEnvelope<TestGuidEntityDso>>();
        ContinuationToken? token = null;
        var pageCount = 0;

        do
        {
            var cursor = DataRange.FromContinuationToken(token?.Value ?? ContinuationToken.Beginning, 2);
            var page = await store.QueryAsync<TestGuidEntityDso>(_guidEntityType, filter, sort, cursor, _ct);
            allItems.AddRange(page.Items);
            token = page.NextToken;
            pageCount++;

            if (!page.HasMoreData || pageCount > 10)
            {
                break;
            }
        }
        while (true);

        // Assert — all 5 items returned across pages, no duplicates
        allItems.Count.ShouldBe(5);
        allItems.Select(x => x.Value.Name).Distinct().Count().ShouldBe(5);
        pageCount.ShouldBe(3); // 2 + 2 + 1
    }
}
