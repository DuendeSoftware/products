// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimSearchUsersEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);
    private static readonly string SearchRoute = $"{ScimHttpClient.UsersRoute}/.search";

    [Fact]
    public async Task Search_users_returns_200_with_list_response_shape()
    {
        await Fixture.InitializeAsync();
        var (r, _) = await Fixture.Client.CreateUserAsync("alice");
        r.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn } };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var schemas = body.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        schemas.ShouldContain(ScimHttpClient.ListResponseSchemaUrn);
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        _ = body.RootElement.GetProperty("startIndex");
        _ = body.RootElement.GetProperty("itemsPerPage");
        _ = body.RootElement.GetProperty("Resources");
    }

    [Fact]
    public async Task Search_with_filter_returns_matching_users()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("department", ScalarDataType.String, "Department");

        var (r1, _) = await Fixture.Client.CreateUserAsync("alice", new Dictionary<string, object> { ["department"] = "engineering" });
        var (r2, _) = await Fixture.Client.CreateUserAsync("bob", new Dictionary<string, object> { ["department"] = "marketing" });
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn }, filter = "department eq \"engineering\"" };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(1);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources[0].GetProperty("department").GetString().ShouldBe("engineering");
    }

    [Fact]
    public async Task Search_with_invalid_filter_returns_400()
    {
        await Fixture.InitializeAsync();

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn }, filter = "!!!" };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidFilter");
    }

    [Fact]
    public async Task Search_with_pagination()
    {
        await Fixture.InitializeAsync();
        var (r1, _) = await Fixture.Client.CreateUserAsync("user1");
        var (r2, _) = await Fixture.Client.CreateUserAsync("user2");
        var (r3, _) = await Fixture.Client.CreateUserAsync("user3");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);
        r3.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn }, startIndex = 2, count = 1 };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("startIndex").GetInt32().ShouldBe(2);
        body.RootElement.GetProperty("itemsPerPage").GetInt32().ShouldBe(1);
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Search_with_sortBy_and_sortOrder()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("department", ScalarDataType.String, "Department");

        var (r1, _) = await Fixture.Client.CreateUserAsync("charlie", new Dictionary<string, object> { ["department"] = "zebra" });
        var (r2, _) = await Fixture.Client.CreateUserAsync("alice", new Dictionary<string, object> { ["department"] = "alpha" });
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn }, sortBy = "department", sortOrder = "ascending" };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBeGreaterThanOrEqualTo(2);
        var first = resources[0].GetProperty("department").GetString()!;
        var second = resources[1].GetProperty("department").GetString()!;
        string.CompareOrdinal(first, second).ShouldBeLessThanOrEqualTo(0);
    }

    [Fact]
    public async Task Search_with_attributes_projection()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (r, _) = await Fixture.Client.CreateUserAsync("alice", "ext-proj");
        r.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Project only externalId
        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn }, attributes = new[] { "externalId" } };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBeGreaterThanOrEqualTo(1);
        // id and schemas are always returned
        _ = resources[0].GetProperty("id");
        _ = resources[0].GetProperty("schemas");
    }

    [Fact]
    public async Task Search_with_excludedAttributes_projection()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (r, createBody) = await Fixture.Client.CreateUserAsync("bob", "ext-222");
        r.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdId = ScimHttpClient.GetUserId(createBody);

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn }, excludedAttributes = new[] { "externalId" } };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBeGreaterThanOrEqualTo(1);
        var bobResource = resources.FirstOrDefault(res => res.GetProperty("id").GetString() == createdId);
        bobResource.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        bobResource.TryGetProperty("externalId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Search_with_null_body_returns_200_with_defaults()
    {
        await Fixture.InitializeAsync();

        // POST with empty body (treated as null/defaults)
        var content = new StringContent("", Encoding.UTF8, ScimHttpClient.ScimContentType);
        var response = await Fixture.Client.PostAsync(SearchRoute, content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("startIndex").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Search_response_content_type_is_scim_json()
    {
        await Fixture.InitializeAsync();

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn } };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ScimHttpClient.ScimContentType);
    }

    [Fact]
    public async Task Search_filter_attribute_name_is_case_insensitive()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("department", ScalarDataType.String, "Department");

        var (r1, _) = await Fixture.Client.CreateUserAsync("alice", new Dictionary<string, object> { ["department"] = "engineering" });
        var (r2, _) = await Fixture.Client.CreateUserAsync("bob", new Dictionary<string, object> { ["department"] = "marketing" });
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Filter uses "Department" (PascalCase) but attribute was registered as "department" (lowercase)
        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn }, filter = "Department eq \"engineering\"" };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(1);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources[0].GetProperty("department").GetString().ShouldBe("engineering");
    }

    [Fact]
    public async Task Search_stores_attribute_with_original_casing_and_finds_case_insensitively()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("jobTitle", ScalarDataType.String, "Job title");

        // Store with camelCase attribute name
        var (r1, _) = await Fixture.Client.CreateUserAsync("alice", new Dictionary<string, object> { ["jobTitle"] = "Engineer" });
        var (r2, _) = await Fixture.Client.CreateUserAsync("bob", new Dictionary<string, object> { ["jobTitle"] = "Manager" });
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Filter uses all-uppercase attribute name — should still match
        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn }, filter = "JOBTITLE eq \"Engineer\"" };
        var response = await Fixture.Client.PostAsync(SearchRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(1);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        // Response preserves the original camelCase from registration/creation
        resources[0].GetProperty("jobTitle").GetString().ShouldBe("Engineer");
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
