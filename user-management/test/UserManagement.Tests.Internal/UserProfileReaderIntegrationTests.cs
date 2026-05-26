// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;
using SortDirection = Duende.Storage.Querying.SortDirection;
using StoreQuery = Duende.Storage.Internal.Querying.Query;

namespace Duende.Platform.UserManagement;

/// <summary>
/// Integration tests for <see cref="UserProfileReader"/>.
/// Verifies that users created with dynamic schema attributes can be queried
/// using SCIM filter expressions against the in-memory store.
/// </summary>
public sealed class UserProfileReaderIntegrationTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private IUserProfileSelfService _selfService = null!;
    private UserProfileReader _profileReader = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _schemaAdmin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        _selfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _profileReader = _serviceProvider.GetRequiredService<UserProfileReader>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private async Task SetupSchema()
    {
        // Define schema attributes of all supported data types
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create("name"),
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("The user's name")
            }, _ct)).ShouldBeTrue();

        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create("is_active"),
                AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
                Description = AttributeDescription.Create("Whether the user is active")
            }, _ct)).ShouldBeTrue();

        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create("birth_date"),
                AttributeType = new ScalarAttributeType(ScalarDataType.Date),
                Description = AttributeDescription.Create("Date of birth")
            }, _ct)).ShouldBeTrue();

        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create("registered_at"),
                AttributeType = new ScalarAttributeType(ScalarDataType.DateTime),
                Description = AttributeDescription.Create("Account creation timestamp")
            }, _ct)).ShouldBeTrue();

        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create("score"),
                AttributeType = new ScalarAttributeType(ScalarDataType.Decimal),
                Description = AttributeDescription.Create("User score")
            }, _ct)).ShouldBeTrue();

        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create("level"),
                AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
                Description = AttributeDescription.Create("User level")
            }, _ct)).ShouldBeTrue();

        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create("department"),
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("Department name")
            }, _ct)).ShouldBeTrue();
    }

    private async Task<UserProfile> CreateUserProfile(
        IReadOnlyAttributeSchema schema,
        string name,
        bool? isActive = null,
        DateOnly? birthDate = null,
        DateTimeOffset? createdAt = null,
        decimal? score = null,
        int? level = null,
        string? department = null)
    {
        var attributes = new AttributeValueCollection(schema);

        attributes.Set(AttributeCode.Create("name"), name);

        if (isActive.HasValue)
        {
            attributes.Set(AttributeCode.Create("is_active"), isActive.Value);
        }

        if (birthDate.HasValue)
        {
            attributes.Set(AttributeCode.Create("birth_date"), birthDate.Value);
        }

        if (createdAt.HasValue)
        {
            attributes.Set(AttributeCode.Create("registered_at"), createdAt.Value);
        }

        if (score.HasValue)
        {
            attributes.Set(AttributeCode.Create("score"), score.Value);
        }

        if (level.HasValue)
        {
            attributes.Set(AttributeCode.Create("level"), level.Value);
        }

        if (department is not null)
        {
            attributes.Set(AttributeCode.Create("department"), department);
        }

        return (await _selfService.TryRegisterAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();
    }

    private Task<QueryResult<UserProfileListItem>> Query(string? filter, int pageNumber, int pageSize) =>
        _profileReader.QueryAsync(filter, null, SortDirection.Ascending, pageNumber, pageSize, _ct);

    private Task<QueryResult<UserProfileListItem>> Query(string? filter) =>
        _profileReader.QueryAsync(filter, null, SortDirection.Ascending, 1, 20, _ct);

    [Fact]
    public async Task Query_with_no_filter_returns_all_users()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", department: "Engineering");
        _ = await CreateUserProfile(schema, "bob", department: "Sales");
        _ = await CreateUserProfile(schema, "charlie", department: "Engineering");

        // Act
        var result = await Query(null);

        // Assert
        result.TotalCount.ShouldBe(3);
        result.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Query_with_empty_filter_returns_all_users()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice");
        _ = await CreateUserProfile(schema, "bob");

        // Act
        var result = await Query("");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_string_attribute_equality()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", department: "Engineering");
        _ = await CreateUserProfile(schema, "bob", department: "Sales");
        _ = await CreateUserProfile(schema, "charlie", department: "Engineering");

        // Act
        var result = await Query("department eq \"Engineering\"");

        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldAllBe(u => ((string)u.Attributes["department"]) == "Engineering");
    }

    [Fact]
    public async Task Query_by_UserName_equality()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice");
        _ = await CreateUserProfile(schema, "bob");

        // Act
        var result = await Query("name eq \"alice\"");

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items[0].Attributes.GetValueOrDefault("name").ShouldNotBeNull().ShouldBeOfType<string>().ShouldBe("alice");
    }

    [Fact]
    public async Task Query_by_string_contains()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", department: "Software Engineering");
        _ = await CreateUserProfile(schema, "bob", department: "Sales");
        _ = await CreateUserProfile(schema, "charlie", department: "Hardware Engineering");

        // Act
        var result = await Query("department co \"Engineering\"");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_string_starts_with()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", department: "Engineering");
        _ = await CreateUserProfile(schema, "bob", department: "Enterprise Sales");
        _ = await CreateUserProfile(schema, "charlie", department: "Marketing");

        // Act
        var result = await Query("department sw \"En\"");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_boolean_attribute_true()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", isActive: true);
        _ = await CreateUserProfile(schema, "bob", isActive: false);
        _ = await CreateUserProfile(schema, "charlie", isActive: true);

        // Act
        var result = await Query("is_active eq true");

        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldAllBe(u => (bool)u.Attributes["is_active"]);
    }

    [Fact]
    public async Task Query_by_boolean_attribute_false()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", isActive: true);
        _ = await CreateUserProfile(schema, "bob", isActive: false);

        // Act
        var result = await Query("is_active eq false");

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items[0].Attributes.GetValueOrDefault("name").ShouldNotBeNull().ShouldBeOfType<string>().ShouldBe("bob");
    }

    [Fact]
    public async Task Query_by_decimal_greater_than()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", score: 85.5m);
        _ = await CreateUserProfile(schema, "bob", score: 45.0m);
        _ = await CreateUserProfile(schema, "charlie", score: 92.3m);

        // Act
        var result = await Query("score gt 80");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_integer_equality()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", level: 5);
        _ = await CreateUserProfile(schema, "bob", level: 3);
        _ = await CreateUserProfile(schema, "charlie", level: 5);

        // Act
        var result = await Query("level eq 5");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_integer_less_than_or_equal()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", level: 1);
        _ = await CreateUserProfile(schema, "bob", level: 3);
        _ = await CreateUserProfile(schema, "charlie", level: 5);

        // Act
        var result = await Query("level le 3");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_date_time_greater_than()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice",
            createdAt: new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        _ = await CreateUserProfile(schema, "bob",
            createdAt: new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
        _ = await CreateUserProfile(schema, "charlie",
            createdAt: new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero));

        // Act
        var result = await Query("registered_at gt \"2024-03-01T00:00:00Z\"");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_date_equality()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", birthDate: new DateOnly(1990, 5, 15));
        _ = await CreateUserProfile(schema, "bob", birthDate: new DateOnly(1985, 3, 20));
        _ = await CreateUserProfile(schema, "charlie", birthDate: new DateOnly(1990, 5, 15));

        // Act — date-only strings must be interpreted as UTC to match storage
        var result = await Query("birth_date eq \"1990-05-15T00:00:00Z\"");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_by_date_greater_than()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", birthDate: new DateOnly(1980, 1, 1));
        _ = await CreateUserProfile(schema, "bob", birthDate: new DateOnly(1995, 6, 15));
        _ = await CreateUserProfile(schema, "charlie", birthDate: new DateOnly(2000, 12, 31));

        // Act
        var result = await Query("birth_date gt \"1990-01-01T00:00:00Z\"");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_with_and_operator()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", department: "Engineering", isActive: true);
        _ = await CreateUserProfile(schema, "bob", department: "Engineering", isActive: false);
        _ = await CreateUserProfile(schema, "charlie", department: "Sales", isActive: true);

        // Act
        var result = await Query("department eq \"Engineering\" and is_active eq true");

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items[0].Attributes.GetValueOrDefault("name").ShouldNotBeNull().ShouldBeOfType<string>().ShouldBe("alice");
    }

    [Fact]
    public async Task Query_with_or_operator()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", department: "Engineering");
        _ = await CreateUserProfile(schema, "bob", department: "Sales");
        _ = await CreateUserProfile(schema, "charlie", department: "Marketing");

        // Act
        var result = await Query("department eq \"Engineering\" or department eq \"Sales\"");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Query_with_not_operator()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice", department: "Engineering");
        _ = await CreateUserProfile(schema, "bob", department: "Sales");
        _ = await CreateUserProfile(schema, "charlie", department: "Marketing");

        // Act
        var result = await Query("not (department eq \"Engineering\")");

        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldNotContain(u => (string?)u.Attributes.GetValueOrDefault("name") == "alice");
    }

    [Fact]
    public async Task Query_respects_pagination()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        for (var i = 0; i < 5; i++)
        {
            _ = await CreateUserProfile(schema, $"user_{i}", department: "Engineering");
        }

        // Act
        var page1 = await Query(null, 1, 2);
        var page2 = await Query(null, 2, 2);
        var page3 = await Query(null, 3, 2);

        // Assert
        page1.Items.Count.ShouldBe(2);
        page1.TotalCount.ShouldBe(5);
        page2.Items.Count.ShouldBe(2);
        page3.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Query_clamps_page_size()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice");

        // Act — pass a negative page size; should be clamped to 1
        var result = await Query(null, 1, -5);

        // Assert
        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Query_result_contains_correct_attributes()
    {
        // Arrange
        await SetupSchema();
        var schema = await _selfService.GetSchemaAsync(_ct);
        _ = await CreateUserProfile(schema, "alice",
            isActive: true,
            department: "Engineering",
            score: 95.5m,
            level: 10);

        // Act
        var result = await Query("name eq \"alice\"");

        // Assert
        result.Items.Count.ShouldBe(1);
        var user = result.Items[0];
        user.Attributes.GetValueOrDefault("name").ShouldNotBeNull().ShouldBeOfType<string>().ShouldBe("alice");
        ((bool)user.Attributes["is_active"]).ShouldBeTrue();
        ((string)user.Attributes["department"]).ShouldBe("Engineering");
        ((decimal)user.Attributes["score"]).ShouldBe(95.5m);
        ((int)user.Attributes["level"]).ShouldBe(10);
    }

    [Fact]
    public async Task non_indexed_attribute_is_not_searchable_by_filter()
    {
        // Arrange — register a non-indexed attribute
        var secretAttr = AttributeCode.Create("secret_note");
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = secretAttr,
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("A non-indexed attribute"),
                IsQueryable = false
            }, _ct)).ShouldBeTrue();

        // Also register a regular indexed attribute so we can create a user
        var nameAttr = AttributeCode.Create("name");
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = nameAttr,
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("Name")
            }, _ct);

        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(nameAttr, "alice");
        attributes.Set(secretAttr, "topsecret");
        _ = (await _selfService.TryRegisterAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();

        // Act - filtering on the non-indexed attribute should throw NotSupportedException
        var ex = await Record.ExceptionAsync(() => Query("secret_note eq \"topsecret\""));

        // Assert
        _ = ex.ShouldNotBeNull();
        _ = ex.ShouldBeOfType<NotSupportedException>();
        ex.Message.ShouldContain("secret_note");
    }
    [Fact]
    public async Task non_indexed_attribute_not_stored_in_search_index()
    {
        // Arrange — register an indexed attribute and a non-indexed attribute
        var searchableCode = AttributeCode.Create("searchable");
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = searchableCode,
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("An indexed, searchable attribute"),
                IsQueryable = true
            }, _ct)).ShouldBeTrue();

        var hiddenCode = AttributeCode.Create("hidden");
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = hiddenCode,
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("A non-indexed attribute not written to the search index"),
                IsQueryable = false
            }, _ct)).ShouldBeTrue();

        // Create a user with both attributes set
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(searchableCode, "findme");
        attributes.Set(hiddenCode, "secret");
        _ = (await _selfService.TryRegisterAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();

        // Resolve the store directly to verify search-index behaviour
        var storeFactory = _serviceProvider.GetRequiredService<IStoreFactory>();
        var store = storeFactory.GetStore();

        // Assert 1: filtering by the indexed attribute finds the user
        var searchableField = new StringField("searchable");
        var indexedResult = await store.QueryFieldsAsync(
            UserProfileDso.EntityType,
            [searchableField],
            StoreQuery.Where(searchableField.Equals("findme")),
            SortParameter.Empty,
            DataRange.FromPage(1, 20),
            _ct);

        indexedResult.TotalCount.ShouldBe(1);

        // Assert 2: filtering by the non-indexed attribute returns no results because
        // the value was never written to the search index
        var hiddenField = new StringField("hidden");
        var nonIndexedFilterResult = await store.QueryFieldsAsync(
            UserProfileDso.EntityType,
            [hiddenField],
            StoreQuery.Where(hiddenField.Equals("secret")),
            SortParameter.Empty,
            DataRange.FromPage(1, 20),
            _ct);

        nonIndexedFilterResult.TotalCount.ShouldBe(0);

        // Assert 3: projecting the non-indexed field returns no data for it —
        // the field was never written to the search index so it cannot be projected
        var projectionResult = await store.QueryFieldsAsync(
            UserProfileDso.EntityType,
            [hiddenField],
            StoreQuery.All(),
            SortParameter.Empty,
            DataRange.FromPage(1, 20),
            _ct);

        // The user exists but the hidden field has no search-index entry, so every
        // projected row either omits the field or returns null for it.
        foreach (var row in projectionResult.Items)
        {
            var hasValue = row.Fields.TryGetValue("hidden", out var v) && v is not null;
            hasValue.ShouldBeFalse();
        }
    }

}
