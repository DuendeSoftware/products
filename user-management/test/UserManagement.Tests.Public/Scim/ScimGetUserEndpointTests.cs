// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimGetUserEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task Get_user_returns_200_with_user_resource()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("alice");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("id").GetString().ShouldBe(id);
        body.RootElement.GetProperty("userName").GetString().ShouldBe("alice");
        _ = body.RootElement.GetProperty("schemas");
        _ = body.RootElement.GetProperty("meta");
    }

    [Fact]
    public async Task Get_user_returns_etag_header()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("bob");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag = ScimHttpClient.GetETag(response);
        etag.ShouldNotBeNullOrEmpty();
        etag.ShouldStartWith("W/\"");
    }

    [Fact]
    public async Task Get_user_with_matching_etag_returns_304()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("charlie");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        // First GET to obtain the ETag
        var firstGet = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");
        firstGet.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag = firstGet.Headers.ETag!;

        // Second GET with If-None-Match
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ScimHttpClient.UsersRoute}/{id}");
        request.Headers.IfNoneMatch.Add(etag);
        var secondGet = await Fixture.Client.SendAsync(request);

        secondGet.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Get_user_with_non_matching_etag_returns_200()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("dave");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var request = new HttpRequestMessage(HttpMethod.Get, $"{ScimHttpClient.UsersRoute}/{id}");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"99999\"", isWeak: true));
        var response = await Fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("id").GetString().ShouldBe(id);
    }

    [Fact]
    public async Task Get_nonexistent_user_returns_404()
    {
        await Fixture.InitializeAsync();

        var randomId = Guid.NewGuid();
        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{randomId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var schemas = body.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        schemas.ShouldContain(ScimHttpClient.ErrorSchemaUrn);
    }

    [Fact]
    public async Task Get_user_with_invalid_id_returns_404()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/not-a-guid");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_user_with_attributes_projection()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("eve", externalId: "ext-456");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}?attributes=userName");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("userName").GetString().ShouldBe("eve");
        body.RootElement.TryGetProperty("externalId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Get_user_with_excludedAttributes_projection()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("frank", externalId: "ext-789");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}?excludedAttributes=externalId");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("userName").GetString().ShouldBe("frank");
        body.RootElement.TryGetProperty("externalId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Get_user_response_content_type_is_scim_json()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("grace");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ScimHttpClient.ScimContentType);
    }

    [Fact]
    public async Task Get_user_with_complex_attribute_returns_nested_object()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync(
            "alice",
            new
            {
                name = new { givenName = "Alice", familyName = "Smith" }
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<ScimUserWithName>();
        _ = user.ShouldNotBeNull();
        user.Name.ShouldBeEquivalentTo(new ScimName
        {
            GivenName = "Alice",
            FamilyName = "Smith"
        });
    }

    [Fact]
    public async Task Get_user_with_list_attribute_returns_array()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync(
            "bob",
            new
            {
                emails = new[]
                {
                    new { value = "bob@example.com", type = "work", primary = true },
                    new { value = "bob@personal.com", type = "home", primary = false }
                }
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<ScimUserWithEmails>();
        _ = user.ShouldNotBeNull();
        user.Emails.ShouldBeEquivalentTo(new[]
        {
            new ScimEmail { Value = "bob@example.com", Type = "work", Primary = true },
            new ScimEmail { Value = "bob@personal.com", Type = "home", Primary = false }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private sealed record ScimUserWithName
    {
        [JsonPropertyName("name")]
        public ScimName? Name { get; init; }
    }

    private sealed record ScimUserWithEmails
    {
        [JsonPropertyName("emails")]
        public ScimEmail[]? Emails { get; init; }
    }

    private sealed record ScimName
    {
        [JsonPropertyName("givenName")]
        public string? GivenName { get; init; }

        [JsonPropertyName("familyName")]
        public string? FamilyName { get; init; }
    }

    private sealed record ScimEmail
    {
        [JsonPropertyName("value")]
        public string? Value { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("primary")]
        public bool? Primary { get; init; }
    }
}
