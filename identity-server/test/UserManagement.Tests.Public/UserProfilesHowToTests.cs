// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

using Duende.IdentityServer.Models;

namespace Duende.Platform.UserManagement;

public sealed class UserProfilesHowToTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IUserProfileAdmin _profileAdmin = null!;
    private IUserProfileSelfService _profileSelfService = null!;
    private ISchemaAdmin _schemaAdmin = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _profileAdmin = _serviceProvider.GetRequiredService<IUserProfileAdmin>();
        _profileSelfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _schemaAdmin = _serviceProvider.GetRequiredService<ISchemaAdmin>();
        await EnsureProfileSchemaAsync(_ct);
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    /// <summary>
    /// Fixture setup only: registers an email/department/name schema, mirroring what the registration
    /// how-tos (<c>BuiltInSchemas.UserProfile</c> + <c>Extend</c>) set up through
    /// <c>AddInMemoryDataExtensionSchemas</c>/<c>AddDynamicSchemas</c> in an IdentityServer host.
    /// </summary>
    private async Task EnsureProfileSchemaAsync(Ct ct)
    {
        var schema = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            AttributeDefinitions =
            [
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("email"),
                    AttributeType = new ScalarAttributeType(ScalarDataType.String),
                    IsUnique = true,
                    IsRequired = true
                },
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("name"),
                    AttributeType = new ScalarAttributeType(ScalarDataType.String)
                },
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("department"),
                    AttributeType = new ScalarAttributeType(ScalarDataType.String)
                }
            ]
        };
        var saveResult = await _schemaAdmin.CreateAsync(schema, ct);
        saveResult.IsSuccess.ShouldBeTrue($"Failed to create schema: {saveResult}");
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static async Task<UserProfile> CreateProfile(
        IUserProfileAdmin profileAdmin, UserSubjectId subjectId, string email, string name, string department, Ct ct)
    {
        // begin-snippet: profile-create
        var schema = await profileAdmin.GetSchemaAsync(ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(AttributeCode.Create("email"), email);
        attributes.Set(AttributeCode.Create("name"), name);
        attributes.Set(AttributeCode.Create("department"), department);

        var profile = await profileAdmin.TryAddAsync(subjectId, attributes.Validate(), ct);
        // end-snippet

        return profile ?? throw new InvalidOperationException("A profile for this subject already exists.");
    }

    private static async Task<UserProfile> UpdateOwnName(
        IUserProfileSelfService profileSelfService, UserSubjectId subjectId, string newName, Ct ct)
    {
        // begin-snippet: profile-self-service-update
        var profile = await profileSelfService.TryGetAsync(subjectId, ct)
            ?? throw new InvalidOperationException("No profile found for this subject.");

        var attributes = profile.ToUpdate();
        attributes.Set(AttributeCode.Create("name"), newName);

        var updated = await profileSelfService.TryUpdateAsync(subjectId, attributes.Validate(), ct);
        // end-snippet

        return updated ?? throw new InvalidOperationException("No profile found for this subject.");
    }

    private static async Task<UserProfile?> FindByEmail(IUserProfileAdmin profileAdmin, string email, Ct ct)
    {
        // begin-snippet: profile-find-by-unique-attribute
        var found = await profileAdmin.TryGetAsync(AttributeCode.Create("email"), email, ct);
        // end-snippet

        return found;
    }

    private static async Task<(IReadOnlyList<UserProfileAttributeProjection> Page, string? NextToken)> QueryDepartmentPage(
        IUserProfileAdmin profileAdmin, string department, string? continuationToken, Ct ct)
    {
        // begin-snippet: profile-query-projection
        var range = DataRange.FromContinuationToken(continuationToken is null ? null : (ContinuationToken)continuationToken, 1);

        // The filter language's string literals use backslash escapes, so a literal containing a
        // quote or a backslash must escape both before being embedded in the search expression.
        var escapedDepartment = department.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

        var page = await profileAdmin.QueryAsync(
            QueryRequest.Create(
                FilterBy.FromSearchExpression(SearchExpression.Create($"department eq \"{escapedDepartment}\"")),
                SortBy.Attribute(AttributeCode.Create("email")),
                range),
            [AttributeCode.Create("email"), AttributeCode.Create("department")],
            ct);
        // end-snippet

        return (page.Results.ToList(), page.ResultsToken);
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Creating_a_profile_returns_the_stored_attributes()
    {
        var subjectId = UserSubjectId.New();

        var profile = await CreateProfile(_profileAdmin, subjectId, "lee@example.com", "Lee Example", "engineering", _ct);

        profile.SubjectId.ShouldBe(subjectId);
        profile.Attributes[AttributeCode.Create("email")].UntypedValue.ShouldBe("lee@example.com");
    }

    [Fact]
    public async Task Self_service_update_changes_only_the_edited_attribute()
    {
        var subjectId = UserSubjectId.New();
        _ = await CreateProfile(_profileAdmin, subjectId, "morgan@example.com", "Morgan Example", "sales", _ct);

        var updated = await UpdateOwnName(_profileSelfService, subjectId, "M. Example", _ct);

        updated.Attributes[AttributeCode.Create("name")].UntypedValue.ShouldBe("M. Example");
        updated.Attributes[AttributeCode.Create("email")].UntypedValue.ShouldBe("morgan@example.com");
    }

    [Fact]
    public async Task Finding_by_a_unique_attribute_returns_the_matching_profile()
    {
        var subjectId = UserSubjectId.New();
        _ = await CreateProfile(_profileAdmin, subjectId, "robin@example.com", "Robin Example", "support", _ct);

        var found = await FindByEmail(_profileAdmin, "robin@example.com", _ct);

        _ = found.ShouldNotBeNull();
        found.SubjectId.ShouldBe(subjectId);
    }
    [Fact]
    public async Task Finding_by_a_unique_attribute_with_no_match_returns_null()
    {
        var found = await FindByEmail(_profileAdmin, $"missing-{Guid.NewGuid():N}@example.com", _ct);

        found.ShouldBeNull();
    }

    [Fact]
    public async Task Query_projection_returns_only_the_requested_attributes()
    {
        var subjectId = UserSubjectId.New();
        _ = await CreateProfile(_profileAdmin, subjectId, "sam@example.com", "Sam Example", "engineering", _ct);

        var (page, _) = await QueryDepartmentPage(_profileAdmin, "engineering", null, _ct);

        var projection = page.Single(p => p.SubjectId == subjectId);
        projection.Contains(AttributeCode.Create("email")).ShouldBeTrue();
        projection.Contains(AttributeCode.Create("department")).ShouldBeTrue();
        projection.Contains(AttributeCode.Create("name")).ShouldBeFalse();
    }

    [Fact]
    public async Task Query_projection_pages_through_results_with_a_continuation_token()
    {
        var department = $"dept-{Guid.NewGuid():N}";
        var first = UserSubjectId.New();
        var second = UserSubjectId.New();
        _ = await CreateProfile(_profileAdmin, first, "first@example.com", "First Example", department, _ct);
        _ = await CreateProfile(_profileAdmin, second, "second@example.com", "Second Example", department, _ct);

        var (firstPage, nextToken) = await QueryDepartmentPage(_profileAdmin, department, null, _ct);
        _ = nextToken.ShouldNotBeNull();
        var (secondPage, _) = await QueryDepartmentPage(_profileAdmin, department, nextToken, _ct);

        firstPage.Count.ShouldBe(1);
        secondPage.Count.ShouldBe(1);
        var seenSubjects = firstPage.Concat(secondPage).Select(p => p.SubjectId).ToList();
        seenSubjects.ShouldContain(first);
        seenSubjects.ShouldContain(second);
    }

    [Fact]
    public async Task Query_projection_with_a_quote_in_the_value_matches_only_that_literal_department()
    {
        var department = $"R&D \"{Guid.NewGuid():N}\"";
        var otherDepartment = $"dept-{Guid.NewGuid():N}";
        var matching = UserSubjectId.New();
        var other = UserSubjectId.New();
        _ = await CreateProfile(_profileAdmin, matching, "quoted@example.com", "Quoted Example", department, _ct);
        _ = await CreateProfile(_profileAdmin, other, "plain@example.com", "Plain Example", otherDepartment, _ct);

        var (page, _) = await QueryDepartmentPage(_profileAdmin, department, null, _ct);

        var projection = page.ShouldHaveSingleItem();
        projection.SubjectId.ShouldBe(matching);
    }
}
