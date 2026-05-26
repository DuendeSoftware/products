// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Querying;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;
using SortDirection = Duende.Storage.Querying.SortDirection;

namespace Duende.Platform.UserManagement;

/// <summary>
///     Integration tests for enum, constrained-string, complex, and list attribute types.
///     Covers tasks 20–24: search field indexing, query type resolver, and end-to-end workflows.
/// </summary>
public sealed class ComplexAttributeIntegrationTests : IAsyncLifetime
{
    private static readonly AttributeCode NameAttr = AttributeCode.Create("name");

    private ServiceProvider _serviceProvider = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private IUserProfileSelfService _selfService = null!;
    private UserProfileReader _reader = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _schemaAdmin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        _selfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _reader = _serviceProvider.GetRequiredService<UserProfileReader>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private Task<QueryResult<UserProfileListItem>> Query(string? filter) =>
        _reader.QueryAsync(filter, "", SortDirection.Ascending, 1, 20, _ct);

    private async Task AddDefinition(AttributeDefinition definition) =>
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(definition, _ct)).ShouldBeTrue();

    /// <summary>
    ///     Registers a "name" string attribute in the schema (idempotent) and
    ///     creates a user profile with that name plus any supplied attributes.
    /// </summary>
    private async Task CreateUserWithAttributes(string userName, AttributeValueCollection attributes)
    {
        // Ensure the "name" attribute is defined (TryAdd is idempotent)
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = NameAttr,
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("User name")
            }, _ct);

        var schema = await _selfService.GetSchemaAsync(_ct);
        var collection = new AttributeValueCollection(schema);
        foreach (var attr in attributes)
        {
            collection.Set(attr);
        }

        collection.Set(NameAttr, userName);

        _ = (await _selfService.TryRegisterAsync(UserSubjectId.New(), collection.Validate(), _ct)).ShouldNotBeNull();
    }

    [Fact]
    public async Task ComplexAttributeDottedPathQueryFindsMatchingUsers()
    {
        // Arrange
        var addressName = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        await AddDefinition(new AttributeDefinition { Code = addressName, AttributeType = complexType, Description = AttributeDescription.Create("Address") });

        var schema = await _selfService.GetSchemaAsync(_ct);

        IReadOnlyDictionary<string, object> SeattleAddr() =>
            new Dictionary<string, object> { ["city"] = "Seattle", ["zip"] = "98101" };
        IReadOnlyDictionary<string, object> PortlandAddr() =>
            new Dictionary<string, object> { ["city"] = "Portland", ["zip"] = "97201" };

        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyDictionary<string, object>>(addressName, SeattleAddr())]));
        await CreateUserWithAttributes("bob",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyDictionary<string, object>>(addressName, PortlandAddr())]));
        await CreateUserWithAttributes("charlie",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyDictionary<string, object>>(addressName, SeattleAddr())]));

        // Act — dotted-path SCIM filter
        var result = await Query("address.city eq \"Seattle\"");

        // Assert
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task ComplexAttributeRoundTripsValueCorrectly()
    {
        // Arrange
        var addressName = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        await AddDefinition(new AttributeDefinition { Code = addressName, AttributeType = complexType, Description = AttributeDescription.Create("Address") });

        var schema = await _selfService.GetSchemaAsync(_ct);
        IReadOnlyDictionary<string, object> value =
            new Dictionary<string, object> { ["city"] = "Seattle", ["zip"] = "98101" };
        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyDictionary<string, object>>(addressName, value)]));

        // Act — only one user, so null filter suffices
        var result = await Query(null);

        // Assert
        result.TotalCount.ShouldBe(1);
        var addr = (IReadOnlyDictionary<string, object>)result.Items[0].Attributes["address"];
        addr["city"].ShouldBe("Seattle");
        addr["zip"].ShouldBe("98101");
    }

    [Fact]
    public async Task NestedComplexAttributeDottedPathQueryFindsCorrectUser()
    {
        // Arrange — address { geo { lat: decimal, lng: decimal } }
        var addressName = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("geo")] = ComplexAttributeProperty.Of(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("lat")] = ComplexAttributeProperty.Of(ScalarDataType.Decimal),
                [AttributeCode.Create("lng")] = ComplexAttributeProperty.Of(ScalarDataType.Decimal)
            }))
        });
        await AddDefinition(new AttributeDefinition { Code = addressName, AttributeType = complexType, Description = AttributeDescription.Create("Address with geo") });

        var schema = await _selfService.GetSchemaAsync(_ct);

        IReadOnlyDictionary<string, object> SeattleWithGeo() =>
            new Dictionary<string, object>
            {
                ["city"] = "Seattle",
                ["geo"] = new Dictionary<string, object> { ["lat"] = 47.6m, ["lng"] = -122.3m }
            };

        IReadOnlyDictionary<string, object> PortlandWithGeo() =>
            new Dictionary<string, object>
            {
                ["city"] = "Portland",
                ["geo"] = new Dictionary<string, object> { ["lat"] = 45.5m, ["lng"] = -122.7m }
            };

        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyDictionary<string, object>>(addressName, SeattleWithGeo())]));
        await CreateUserWithAttributes("bob",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyDictionary<string, object>>(addressName, PortlandWithGeo())]));

        // Act — deeply nested numeric dotted-path query
        var result = await Query("address.geo.lat gt 46.0");

        // Assert — only Seattle (47.6 > 46.0) qualifies
        result.TotalCount.ShouldBe(1);
        ((string)result.Items[0].Attributes["name"]).ShouldBe("alice");
    }

    [Fact]
    public async Task ListOfStringsContainsQueryFindsMatchingUsers()
    {
        // Arrange
        var tagsName = AttributeCode.Create("tags");
        var listType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));
        await AddDefinition(new AttributeDefinition { Code = tagsName, AttributeType = listType, Description = AttributeDescription.Create("Tags") });

        var schema = await _selfService.GetSchemaAsync(_ct);

        IReadOnlyList<object> Tags(params string[] values) => values.Cast<object>().ToList();

        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyList<object>>(tagsName, Tags("admin", "power-user"))]));
        await CreateUserWithAttributes("bob",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyList<object>>(tagsName, Tags("user", "viewer"))]));
        await CreateUserWithAttributes("charlie",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyList<object>>(tagsName, Tags("admin", "viewer"))]));

        // Act
        var result = await Query("tags co \"admin\"");

        // Assert — alice and charlie have "admin"
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task ListOfStringsRoundTripsCorrectly()
    {
        // Arrange
        var tagsName = AttributeCode.Create("tags");
        var listType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));
        await AddDefinition(new AttributeDefinition { Code = tagsName, AttributeType = listType, Description = AttributeDescription.Create("Tags") });

        var schema = await _selfService.GetSchemaAsync(_ct);
        IReadOnlyList<object> tags = new List<object> { "admin", "power-user" };
        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyList<object>>(tagsName, tags)]));

        // Act — only one user, so null filter suffices
        var result = await Query(null);

        // Assert
        result.TotalCount.ShouldBe(1);
        var returnedTags = (IReadOnlyList<object>)result.Items[0].Attributes["tags"];
        returnedTags.Count.ShouldBe(2);
        returnedTags.ShouldContain("admin");
        returnedTags.ShouldContain("power-user");
    }

    [Fact]
    public async Task ListOfComplexDottedPathQueryFindsCorrectUser()
    {
        // Arrange
        var phonesName = AttributeCode.Create("phones");
        var listType = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("number")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        }));
        await AddDefinition(new AttributeDefinition { Code = phonesName, AttributeType = listType, Description = AttributeDescription.Create("Phone numbers") });

        var schema = await _selfService.GetSchemaAsync(_ct);

        IReadOnlyList<object> AlicePhones() => new List<object>
        {
            new Dictionary<string, object> { ["type"] = "mobile", ["number"] = "555-0001" },
            new Dictionary<string, object> { ["type"] = "home", ["number"] = "555-0002" }
        };

        IReadOnlyList<object> BobPhones() => new List<object>
        {
            new Dictionary<string, object> { ["type"] = "work", ["number"] = "555-0003" }
        };

        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyList<object>>(phonesName, AlicePhones())]));
        await CreateUserWithAttributes("bob",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyList<object>>(phonesName, BobPhones())]));

        // Act — dotted-path query on list element sub-property
        var result = await Query("phones.type eq \"mobile\"");

        // Assert — only alice has a "mobile" phone
        result.TotalCount.ShouldBe(1);
        ((string)result.Items[0].Attributes["name"]).ShouldBe("alice");
    }

    [Fact]
    public async Task ListOfComplexRoundTripsCorrectly()
    {
        // Arrange
        var phonesName = AttributeCode.Create("phones");
        var listType = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("number")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        }));
        await AddDefinition(new AttributeDefinition { Code = phonesName, AttributeType = listType, Description = AttributeDescription.Create("Phone numbers") });

        var schema = await _selfService.GetSchemaAsync(_ct);
        IReadOnlyList<object> phones = new List<object>
        {
            new Dictionary<string, object> { ["type"] = "mobile", ["number"] = "555-1234" }
        };
        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyList<object>>(phonesName, phones)]));

        // Act — only one user, so null filter suffices
        var result = await Query(null);

        // Assert
        result.TotalCount.ShouldBe(1);
        var returnedPhones = (IReadOnlyList<object>)result.Items[0].Attributes["phones"];
        returnedPhones.Count.ShouldBe(1);
        var phone = (IReadOnlyDictionary<string, object>)returnedPhones[0];
        phone["type"].ShouldBe("mobile");
        phone["number"].ShouldBe("555-1234");
    }

    [Fact]
    public async Task ComplexAttributeIndexesBothSubFieldsWithDottedPaths()
    {
        // Arrange — verify both city and zip are separately indexed
        var addressName = AttributeCode.Create("address");
        var complexType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });
        await AddDefinition(new AttributeDefinition { Code = addressName, AttributeType = complexType, Description = AttributeDescription.Create("Address") });

        var schema = await _selfService.GetSchemaAsync(_ct);
        IReadOnlyDictionary<string, object> seattle =
            new Dictionary<string, object> { ["city"] = "Seattle", ["zip"] = "98101" };
        IReadOnlyDictionary<string, object> portland =
            new Dictionary<string, object> { ["city"] = "Portland", ["zip"] = "97201" };

        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyDictionary<string, object>>(addressName, seattle)]));
        await CreateUserWithAttributes("bob",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyDictionary<string, object>>(addressName, portland)]));

        // Both sub-fields indexed with dotted paths
        (await Query("address.city eq \"Seattle\"")).TotalCount.ShouldBe(1);
        (await Query("address.zip eq \"98101\"")).TotalCount.ShouldBe(1);
        (await Query("address.city eq \"Portland\"")).TotalCount.ShouldBe(1);
        (await Query("address.zip eq \"97201\"")).TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task ListAttributeIndexesAllItemsWithItemIndex()
    {
        // Arrange — alice has 3 phones; all must be indexed for queries to work
        var phonesName = AttributeCode.Create("phones");
        var listType = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("number")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        }));
        await AddDefinition(new AttributeDefinition { Code = phonesName, AttributeType = listType, Description = AttributeDescription.Create("Phones") });

        var schema = await _selfService.GetSchemaAsync(_ct);
        IReadOnlyList<object> alicePhones = new List<object>
        {
            new Dictionary<string, object> { ["type"] = "mobile", ["number"] = "555-0001" },
            new Dictionary<string, object> { ["type"] = "home", ["number"] = "555-0002" },
            new Dictionary<string, object> { ["type"] = "work", ["number"] = "555-0003" }
        };
        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyList<object>>(phonesName, alicePhones)]));

        // Each item indexed — all 3 types should find alice
        (await Query("phones.type eq \"mobile\"")).TotalCount.ShouldBe(1);
        (await Query("phones.type eq \"home\"")).TotalCount.ShouldBe(1);
        (await Query("phones.type eq \"work\"")).TotalCount.ShouldBe(1);
        // Non-existent type should not match
        (await Query("phones.type eq \"fax\"")).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task TypeResolverHandlesListOfComplexSubPropertyAsMultiValuedField()
    {
        // Arrange — phones.type resolves to a multi-valued StringField, queryable with eq
        var phonesName = AttributeCode.Create("phones");
        var listType = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("number")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        }));
        await AddDefinition(new AttributeDefinition { Code = phonesName, AttributeType = listType, Description = AttributeDescription.Create("Phones") });

        var schema = await _selfService.GetSchemaAsync(_ct);
        IReadOnlyList<object> phones = new List<object>
        {
            new Dictionary<string, object> { ["type"] = "mobile", ["number"] = "555-9999" }
        };
        await CreateUserWithAttributes("alice",
            new AttributeValueCollection(schema, [AttributeValue.Load<IReadOnlyList<object>>(phonesName, phones)]));

        // Act & Assert — resolver must return multi-valued field for phones.type
        var result = await Query("phones.type eq \"mobile\"");
        result.TotalCount.ShouldBe(1);
    }
}
