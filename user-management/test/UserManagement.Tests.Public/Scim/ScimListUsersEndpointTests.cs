// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimListUsersEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task List_users_returns_200_with_list_response_shape()
    {
        await Fixture.InitializeAsync();
        var (created, _) = await Fixture.Client.CreateUserAsync("alice");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.Client.GetAsync(ScimHttpClient.UsersRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var schemas = body.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        schemas.ShouldContain(ScimHttpClient.ListResponseSchemaUrn);
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        body.RootElement.GetProperty("startIndex").GetInt32().ShouldBe(1);
        _ = body.RootElement.GetProperty("itemsPerPage");
        _ = body.RootElement.GetProperty("Resources");
    }

    [Fact]
    public async Task List_users_returns_empty_list_when_no_users()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync(ScimHttpClient.UsersRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(0);
        body.RootElement.GetProperty("Resources").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task List_users_with_filter_by_custom_attribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("department", ScalarDataType.String, "Department");

        var (a, _) = await Fixture.Client.CreateUserAsync("alice", new { department = "engineering" });
        var (b, _) = await Fixture.Client.CreateUserAsync("bob", new { department = "marketing" });
        a.StatusCode.ShouldBe(HttpStatusCode.Created);
        b.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.Client.GetAsync(
            $"{ScimHttpClient.UsersRoute}?filter=department eq \"engineering\"");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(1);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources[0].GetProperty("department").GetString().ShouldBe("engineering");
    }

    [Fact]
    public async Task List_users_with_invalid_filter_returns_400()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}?filter=!!!invalid");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidFilter");
    }

    [Fact]
    public async Task List_users_with_pagination_startIndex_and_count()
    {
        await Fixture.InitializeAsync();
        var (r1, _) = await Fixture.Client.CreateUserAsync("user1");
        var (r2, _) = await Fixture.Client.CreateUserAsync("user2");
        var (r3, _) = await Fixture.Client.CreateUserAsync("user3");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);
        r3.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}?startIndex=2&count=1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("startIndex").GetInt32().ShouldBe(2);
        body.RootElement.GetProperty("itemsPerPage").GetInt32().ShouldBe(1);
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task List_users_with_count_zero_is_clamped_to_1()
    {
        await Fixture.InitializeAsync();
        var (r, _) = await Fixture.Client.CreateUserAsync("alice");
        r.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}?count=0");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("itemsPerPage").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task List_users_with_sortBy_custom_attribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("department", ScalarDataType.String, "Department");

        var (r1, _) = await Fixture.Client.CreateUserAsync("charlie", new { department = "zebra" });
        var (r2, _) = await Fixture.Client.CreateUserAsync("alice", new { department = "alpha" });
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.Client.GetAsync(
            $"{ScimHttpClient.UsersRoute}?sortBy=department&sortOrder=ascending");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBeGreaterThanOrEqualTo(2);
        var first = resources[0].GetProperty("department").GetString()!;
        var second = resources[1].GetProperty("department").GetString()!;
        string.CompareOrdinal(first, second).ShouldBeLessThanOrEqualTo(0);
    }

    [Fact]
    public async Task List_users_with_sortOrder_descending()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("department", ScalarDataType.String, "Department");

        var (r1, _) = await Fixture.Client.CreateUserAsync("alice", new { department = "alpha" });
        var (r2, _) = await Fixture.Client.CreateUserAsync("charlie", new { department = "zebra" });
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.Client.GetAsync(
            $"{ScimHttpClient.UsersRoute}?sortBy=department&sortOrder=descending");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBeGreaterThanOrEqualTo(2);
        var first = resources[0].GetProperty("department").GetString()!;
        var second = resources[1].GetProperty("department").GetString()!;
        string.CompareOrdinal(first, second).ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task List_users_with_attributes_projection()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (r, _) = await Fixture.Client.CreateUserAsync("alice", "ext-proj");
        r.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Project only externalId; other custom attributes should be excluded
        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}?attributes=externalId");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBeGreaterThanOrEqualTo(1);
        // id and schemas are always returned; externalId was requested
        _ = resources[0].GetProperty("id");
        _ = resources[0].GetProperty("schemas");
    }

    [Fact]
    public async Task List_users_with_excludedAttributes_projection()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (r, createBody) = await Fixture.Client.CreateUserAsync("bob", "ext-999");
        r.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdId = ScimHttpClient.GetUserId(createBody);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}?excludedAttributes=externalId");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBeGreaterThanOrEqualTo(1);
        var bobResource = resources.FirstOrDefault(r2 => r2.GetProperty("id").GetString() == createdId);
        bobResource.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        bobResource.TryGetProperty("externalId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task List_users_response_content_type_is_scim_json()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync(ScimHttpClient.UsersRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ScimHttpClient.ScimContentType);
    }

    [Fact]
    public async Task List_users_returns_complex_attributes_in_resources()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (created, _) = await Fixture.Client.CreateUserAsync(
            "alice",
            new
            {
                name = new { givenName = "Alice", familyName = "Smith" }
            });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.Client.GetAsync(ScimHttpClient.UsersRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<ScimListResponse>();
        _ = list.ShouldNotBeNull();
        var alice = list.Resources.ShouldNotBeNull()
            .FirstOrDefault(r => r.UserName == "alice");
        _ = alice.ShouldNotBeNull();
        alice.Name.ShouldBeEquivalentTo(new ScimName
        {
            GivenName = "Alice",
            FamilyName = "Smith"
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private sealed record ScimListResponse
    {
        [JsonPropertyName("Resources")]
        public ScimUserResource[]? Resources { get; init; }
    }

    private sealed record ScimUserResource
    {
        [JsonPropertyName("userName")]
        public string? UserName { get; init; }

        [JsonPropertyName("name")]
        public ScimName? Name { get; init; }
    }

    private sealed record ScimName
    {
        [JsonPropertyName("givenName")]
        public string? GivenName { get; init; }

        [JsonPropertyName("familyName")]
        public string? FamilyName { get; init; }
    }
}
