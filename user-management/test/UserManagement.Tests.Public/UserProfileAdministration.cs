// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class UserProfileAdministration : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<ExternalAuthenticator> _externalAuthenticators = [.. TestData.SubjectIdTypes.Select(TestData.CreateExternalAuthenticator)];
    private readonly List<OtpAddress> _otpAddresses = [.. TestData.SubjectIdTypes.Select(TestData.CreateOtpAddress)];
    private IUserProfileAdmin _admin = null!;
    private IUserAdmin _userAdmin = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private ServiceProvider _serviceProvider = null!;

    public static TheoryData<string> AttributeNames { get; } = [.. TestData.CreateAttributeDefinitions().Select(d => d.Code.ToString())];

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _admin = _serviceProvider.GetRequiredService<IUserProfileAdmin>();
        _userAdmin = _serviceProvider.GetRequiredService<IUserAdmin>();
        _externalAuthenticators.Count.ShouldBeGreaterThan(1);
        _otpAddresses.Count.ShouldBeGreaterThan(1);
        _schemaAdmin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_add_user()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _admin.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);

        var user = await _admin.TryAddAsync(UserSubjectId.New(), attributes.Validate(), _ct);

        _ = user.ShouldNotBeNull();
        user.Attributes.Values.ShouldBe(attributes, ignoreOrder: true);
    }

    [Fact]
    public async Task Cannot_add_two_users_with_the_same_unique_attributes()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _admin.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        _ = (await _admin.TryAddAsync(UserSubjectId.New(), attributes.Validate(), ct: _ct)).ShouldNotBeNull();

        var profile = await _admin.TryAddAsync(UserSubjectId.New(), attributes.Validate(), ct: _ct);

        profile.ShouldBeNull();
    }

    [Fact]
    public async Task Can_get_user_by_SubjectId()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _admin.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var subjectId = (await _admin.TryAddAsync(UserSubjectId.New(), attributes.Validate(), ct: _ct)).ShouldNotBeNull().SubjectId;

        var user = await _admin.TryGetAsync(subjectId, _ct);

        user.ShouldNotBeNull().Attributes.Values.ShouldBe(attributes, ignoreOrder: true);
    }

    [Fact]
    public async Task Cannot_get_removed_user()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _admin.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var subjectId = (await _admin.TryAddAsync(UserSubjectId.New(), attributes.Validate(), ct: _ct)).ShouldNotBeNull().SubjectId;
        (await _userAdmin.TryRemoveAsync(subjectId, _ct)).ShouldBeTrue();

        var user = await _admin.TryGetAsync(subjectId, _ct);

        user.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(AttributeNames))]
    public async Task Can_get_by_attribute(string AttributeCode)
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _admin.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var subjectId = (await _admin.TryAddAsync(UserSubjectId.New(), attributes.Validate(), ct: _ct)).ShouldNotBeNull().SubjectId;
        var attribute = attributes.Single(a => a.Code.ToString() == AttributeCode);

        var user = await _admin.TryGetAsync(attribute.Code, attribute.UntypedValue, _ct);

        user.ShouldNotBeNull().SubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task Can_query_all_users()
    {
        await AddNonUniqueSchema(_ct);
        var subjectId1 = (await AddUserWithName("Alice", _ct)).SubjectId;
        var subjectId2 = (await AddUserWithName("Bob", _ct)).SubjectId;

        var result = await _admin.QueryAsync(QueryRequest.Create(), _ct);

        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        result.Items.ShouldContain(u => u.SubjectId == subjectId1);
        result.Items.ShouldContain(u => u.SubjectId == subjectId2);
    }

    [Fact]
    public async Task Can_query_with_filter()
    {
        await AddNonUniqueSchema(_ct);
        var target = await AddUserWithName("FilterTarget", _ct);
        _ = await AddUserWithName("Other", _ct);
        var filter = FilterBy.FromSearchExpression(SearchExpression.Create("name eq \"FilterTarget\""));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.ShouldContain(u => u.SubjectId == target.SubjectId);
        result.Items.ShouldNotContain(u => u.Attributes[AttributeCode.Create("name")].UntypedValue as string == "Other");
    }

    [Fact]
    public async Task Can_query_with_paging()
    {
        await AddNonUniqueSchema(_ct);
        _ = await AddUserWithName("Page1", _ct);
        _ = await AddUserWithName("Page2", _ct);
        _ = await AddUserWithName("Page3", _ct);

        var page1 = await _admin.QueryAsync(QueryRequest.Create(DataRange.FromPage(1, 2)), _ct);
        var page2 = await _admin.QueryAsync(QueryRequest.Create(DataRange.FromPage(2, 2)), _ct);

        page1.Items.Count.ShouldBe(2);
        page2.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
        page1.Items.Select(u => u.SubjectId).Intersect(page2.Items.Select(u => u.SubjectId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_query_with_sort()
    {
        await AddNonUniqueSchema(_ct);
        _ = await AddUserWithName("Charlie", _ct);
        _ = await AddUserWithName("Alice", _ct);
        _ = await AddUserWithName("Bob", _ct);
        var sort = SortBy.Attribute(AttributeCode.Create("name"));

        var result = await _admin.QueryAsync(QueryRequest.Create(sort), _ct);

        var names = result.Items.Select(u => u.Attributes[AttributeCode.Create("name")].UntypedValue as string).ToList();
        names.ShouldBe(names.Order().ToList());
    }

    [Fact]
    public async Task Can_query_with_attribute_projection()
    {
        await AddNonUniqueSchema(_ct);
        _ = await AddUserWithName("Projected", _ct);
        var projectedNames = new HashSet<AttributeCode> { AttributeCode.Create("name") };

        var result = await _admin.QueryAsync(QueryRequest.Create(), projectedNames, _ct);

        result.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
        foreach (var projection in result.Items)
        {
            projection.SubjectId.ShouldNotBe(default);
            foreach (var attr in projection.Attributes)
            {
                projectedNames.ShouldContain(attr.Code);
            }
        }
    }

    [Fact]
    public async Task Can_query_with_starts_with_filter_on_string()
    {
        await AddNonUniqueSchema(_ct);
        var target = await AddUserWithName("BobSmith", _ct);
        _ = await AddUserWithName("Alice", _ct);
        var filter = FilterBy.FromSearchExpression(SearchExpression.Create("name sw \"Bob\""));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.ShouldContain(u => u.SubjectId == target.SubjectId);
        result.Items.ShouldNotContain(u => u.Attributes[AttributeCode.Create("name")].UntypedValue as string == "Alice");
    }

    [Fact]
    public async Task Can_query_with_equality_filter_on_boolean()
    {
        var boolAttr = AttributeCode.Create("active");
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = boolAttr, AttributeType = new ScalarAttributeType(ScalarDataType.Boolean), Description = AttributeDescription.Create("Active flag") }, _ct);
        var attrs = new AttributeValueCollection(await _admin.GetSchemaAsync(_ct));
        attrs.Set(boolAttr, true);
        var target = (await _admin.TryAddAsync(UserSubjectId.New(), attrs.Validate(), _ct)).ShouldNotBeNull();

        var filter = FilterBy.FromSearchExpression(SearchExpression.Create("active eq true"));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.ShouldContain(u => u.SubjectId == target.SubjectId);
    }

    [Fact]
    public async Task Can_query_with_filter_on_integer()
    {
        var intAttr = AttributeCode.Create("age");
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = intAttr, AttributeType = new ScalarAttributeType(ScalarDataType.Integer), Description = AttributeDescription.Create("Age") }, _ct);
        var attrs = new AttributeValueCollection(await _admin.GetSchemaAsync(_ct));
        attrs.Set(intAttr, 42);
        var target = (await _admin.TryAddAsync(UserSubjectId.New(), attrs.Validate(), _ct)).ShouldNotBeNull();

        var filter = FilterBy.FromSearchExpression(SearchExpression.Create("age eq 42"));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.ShouldContain(u => u.SubjectId == target.SubjectId);
    }

    [Fact]
    public async Task Can_query_with_filter_on_decimal()
    {
        var decAttr = AttributeCode.Create("score");
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = decAttr, AttributeType = new ScalarAttributeType(ScalarDataType.Decimal), Description = AttributeDescription.Create("Score") }, _ct);
        var attrs = new AttributeValueCollection(await _admin.GetSchemaAsync(_ct));
        attrs.Set(decAttr, 99.5m);
        var target = (await _admin.TryAddAsync(UserSubjectId.New(), attrs.Validate(), _ct)).ShouldNotBeNull();

        var filter = FilterBy.FromSearchExpression(SearchExpression.Create("score eq 99.5"));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.ShouldContain(u => u.SubjectId == target.SubjectId);
    }

    [Fact]
    public async Task Can_query_with_filter_on_date()
    {
        var dateAttr = AttributeCode.Create("birthdate");
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = dateAttr, AttributeType = new ScalarAttributeType(ScalarDataType.Date), Description = AttributeDescription.Create("Birth date") }, _ct);
        var attrs = new AttributeValueCollection(await _admin.GetSchemaAsync(_ct));
        attrs.Set(dateAttr, new DateOnly(1990, 6, 15));
        var target = (await _admin.TryAddAsync(UserSubjectId.New(), attrs.Validate(), _ct)).ShouldNotBeNull();

        var filter = FilterBy.FromSearchExpression(SearchExpression.Create("birthdate eq \"1990-06-15\""));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.ShouldContain(u => u.SubjectId == target.SubjectId);
    }

    [Fact]
    public async Task Can_query_with_filter_on_datetime()
    {
        var dtAttr = AttributeCode.Create("some_date");
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = dtAttr, AttributeType = new ScalarAttributeType(ScalarDataType.DateTime), Description = AttributeDescription.Create("some_date") }, _ct)).ShouldBeTrue();
        var attrs = new AttributeValueCollection(await _admin.GetSchemaAsync(_ct));
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        attrs.Set(dtAttr, timestamp);
        var target = (await _admin.TryAddAsync(UserSubjectId.New(), attrs.Validate(), _ct)).ShouldNotBeNull();

        var filter = FilterBy.FromSearchExpression(SearchExpression.Create("some_date eq \"2024-01-15T10:30:00Z\""));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.ShouldContain(u => u.SubjectId == target.SubjectId);
    }

    [Fact]
    public async Task Can_query_with_filter_created_at()
    {
        var target = (await _admin.TryAddAsync(UserSubjectId.New(), ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();

        var timeProvider = _serviceProvider.GetRequiredService<TimeProvider>();

        // This is the time the user was created. 
        var userCreationTime = timeProvider.GetUtcNow();

        // when filtering with created_at gt (now - 1 hour), the user should be included
        var filter = FilterBy.FromSearchExpression(SearchExpression.Create($"created_at gt \"{userCreationTime.AddHours(-1):s}\""));
        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);
        result.Items.ShouldContain(u => u.SubjectId == target.SubjectId);

        // but when filtering with created_at gt (now + 1 hour), the user should not be included
        filter = FilterBy.FromSearchExpression(SearchExpression.Create($"created_at gt \"{userCreationTime.AddHours(1):s}\""));
        result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);
        result.Items.Count.ShouldBe(0);

    }

    [Fact]
    public async Task Can_query_complex_attribute_by_sub_property()
    {
        var emailAttr = AttributeCode.Create("email");
        var emailType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("value")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean)
        });
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = emailAttr, AttributeType = emailType, Description = AttributeDescription.Create("Email address") }, _ct);

        var schema = await _admin.GetSchemaAsync(_ct);
        var attrs1 = new AttributeValueCollection(schema);
        attrs1.Set(emailAttr,
            (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
            {
                ["value"] = "bob@example.com",
                ["type"] = "work",
                ["primary"] = true
            });
        var bobUserId = UserSubjectId.New();
        var target = (await _admin.TryAddAsync(bobUserId, attrs1.Validate(), _ct)).ShouldNotBeNull();

        var attrs2 = new AttributeValueCollection(schema);
        attrs2.Set(emailAttr,
            (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
            {
                ["value"] = "alice@other.com",
                ["type"] = "home",
                ["primary"] = false
            });
        _ = (await _admin.TryAddAsync(UserSubjectId.New(), attrs2.Validate(), _ct)).ShouldNotBeNull();

        var filter = FilterBy.FromSearchExpression(SearchExpression.Create("email.type eq \"work\" and email.primary eq true"));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.Count.ShouldBe(1);
        result.Items.ShouldContain(u => u.SubjectId == bobUserId);
    }

    [Theory]
    [InlineData("emails[type eq \"work\" and primary eq true]", 1, true, "only bob has an email.type == work and email.primary == true")]
    [InlineData("emails[type eq \"home\" and primary eq true]", 0, false, "nobody has a home email that is primary")]
    [InlineData("emails[type eq \"work\"]", 2, true, "alice also has a work email")]
    [InlineData("emails.type eq \"work\"", 2, true, "we can find alice without bracket syntax")]
    [InlineData("emails.type eq \"work\" and emails.primary eq false", 2, true, "without bracket syntax, this query means: the user should have a work email and should have a non primary email")]
    [InlineData("emails[value co \"bob@\"]", 1, true, "only bob has an email that contains bob@")]
    public async Task Can_query_list_of_complex_attribute_by_sub_property(string query, int expectedCount, bool foundBob, string message)
    {
        var emailsAttr = AttributeCode.Create("emails");
        var emailType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("value")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean)
        });
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = emailsAttr,
                AttributeType = new ListAttributeType(emailType),
                Description = AttributeDescription.Create("Email addresses")
            }, _ct);

        var schema = await _admin.GetSchemaAsync(_ct);
        var attrs1 = new AttributeValueCollection(schema);
        attrs1.Set(emailsAttr,
            (IReadOnlyList<object>)new List<object>
            {
                new Dictionary<string, object> { ["value"] = "bob@work.com", ["type"] = "work", ["primary"] = true },
                new Dictionary<string, object> { ["value"] = "bob@home.com", ["type"] = "home", ["primary"] = false }
            });
        var bob = (await _admin.TryAddAsync(UserSubjectId.New(), attrs1.Validate(), _ct)).ShouldNotBeNull();

        var attrs2 = new AttributeValueCollection(schema);
        attrs2.Set(emailsAttr,
            (IReadOnlyList<object>)new List<object>
            {
                new Dictionary<string, object> { ["value"] = "alice@work.com", ["type"] = "work", ["primary"] = false }
            });
        _ = (await _admin.TryAddAsync(UserSubjectId.New(), attrs2.Validate(), _ct)).ShouldNotBeNull();

        var filter = FilterBy.FromSearchExpression(SearchExpression.Create(query));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.Count.ShouldBe(expectedCount, message);
        if (foundBob)
        {
            result.Items.ShouldContain(u => u.SubjectId == bob.SubjectId, message);
        }
        else
        {
            result.Items.ShouldNotContain(u => u.SubjectId == bob.SubjectId, message);
        }
    }

    [Fact]
    public async Task Can_query_list_of_complex_attribute_by_eq_on_sub_property()
    {
        var emailsAttr = AttributeCode.Create("emails2");
        var emailType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("value")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean)
        });
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = emailsAttr,
                AttributeType = new ListAttributeType(emailType),
                Description = AttributeDescription.Create("Email addresses 2")
            }, _ct);

        var schema = await _admin.GetSchemaAsync(_ct);
        var attrs1 = new AttributeValueCollection(schema);
        attrs1.Set(emailsAttr,
            (IReadOnlyList<object>)new List<object>
            {
                new Dictionary<string, object> { ["value"] = "specific@example.com", ["type"] = "work", ["primary"] = true }
            });
        var target = (await _admin.TryAddAsync(UserSubjectId.New(), attrs1.Validate(), _ct)).ShouldNotBeNull();

        var attrs2 = new AttributeValueCollection(schema);
        attrs2.Set(emailsAttr,
            (IReadOnlyList<object>)new List<object>
            {
                new Dictionary<string, object> { ["value"] = "other@example.com", ["type"] = "home", ["primary"] = false }
            });
        _ = (await _admin.TryAddAsync(UserSubjectId.New(), attrs2.Validate(), _ct)).ShouldNotBeNull();

        var filter = FilterBy.FromSearchExpression(SearchExpression.Create("emails2.value eq \"specific@example.com\""));

        var result = await _admin.QueryAsync(QueryRequest.Create(filter), _ct);

        result.Items.ShouldContain(u => u.SubjectId == target.SubjectId);
        result.Items.ShouldAllBe(u => u.SubjectId == target.SubjectId);
    }

    private async Task AddNonUniqueSchema(Ct ct)
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("name"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("User name attribute")
        };
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(definition, ct);
    }

    private async Task<UserProfile> AddUserWithName(string name, Ct ct)
    {
        var schema = await _admin.GetSchemaAsync(ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(AttributeCode.Create("name"), name);
        return (await _admin.TryAddAsync(UserSubjectId.New(), attributes.Validate(), ct)).ShouldNotBeNull();
    }
}
