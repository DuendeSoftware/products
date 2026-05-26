// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimReplaceUserEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task Replace_user_returns_200_with_updated_resource()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("alice");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var originalEtag = ScimHttpClient.GetETag(createResponse);

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "alice-updated" };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("userName").GetString().ShouldBe("alice-updated");

        var newEtag = ScimHttpClient.GetETag(response);
        newEtag.ShouldNotBe(originalEtag);
    }

    [Fact]
    public async Task Replace_user_returns_new_etag()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("bob");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var originalEtag = ScimHttpClient.GetETag(createResponse);

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bob-v2" };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newEtag = ScimHttpClient.GetETag(response);
        newEtag.ShouldNotBeNullOrEmpty();
        newEtag.ShouldNotBe(originalEtag);
    }

    [Fact]
    public async Task Replace_user_with_if_match_matching_returns_200()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("charlie");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var etag = createResponse.Headers.ETag!;

        var request = new HttpRequestMessage(HttpMethod.Put, $"{ScimHttpClient.UsersRoute}/{id}")
        {
            Content = ScimHttpClient.ScimJsonContent(new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "charlie-v2" })
        };
        request.Headers.IfMatch.Add(etag);
        var response = await Fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Replace_user_with_if_match_mismatched_returns_412()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("dave");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var request = new HttpRequestMessage(HttpMethod.Put, $"{ScimHttpClient.UsersRoute}/{id}")
        {
            Content = ScimHttpClient.ScimJsonContent(new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "dave-v2" })
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"99999\"", isWeak: true));
        var response = await Fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        ShouldlyExtensions.ShouldContain(body.RootElement.GetProperty("detail").GetString()!, "Precondition failed");
    }

    [Fact]
    public async Task Replace_user_nonexistent_returns_404()
    {
        await Fixture.InitializeAsync();

        var randomId = Guid.NewGuid();
        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "ghost" };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{randomId}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Replace_user_with_invalid_id_returns_404()
    {
        await Fixture.InitializeAsync();

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "test" };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/not-a-guid", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Replace_user_without_userName_returns_400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("eve");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn } };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidValue");
    }

    [Fact]
    public async Task Replace_user_with_null_body_returns_400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("frank");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var content = new StringContent("", Encoding.UTF8, ScimHttpClient.ScimContentType);
        var response = await Fixture.Client.PutAsync($"{ScimHttpClient.UsersRoute}/{id}", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidSyntax");
    }

    [Fact]
    public async Task Replace_user_with_wrong_schema_succeeds()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("grace");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new { schemas = new[] { "urn:wrong:schema" }, userName = "grace" };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        // PUT does not validate the schemas array — the request succeeds
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Replace_user_with_duplicate_userName_returns_409()
    {
        await Fixture.InitializeAsync();
        var (r1, b1) = await Fixture.Client.CreateUserAsync("alice");
        var (r2, b2) = await Fixture.Client.CreateUserAsync("bob");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bobId = ScimHttpClient.GetUserId(b2);

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "alice" };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{bobId}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("uniqueness");
    }

    [Fact]
    public async Task Replace_user_with_custom_attribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("department", ScalarDataType.String, "Department");

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("harry");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "harry",
            department = "Engineering"
        };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("department").GetString().ShouldBe("Engineering");
    }

    [Fact]
    public async Task Replace_user_with_externalId()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("ivan");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "ivan",
            externalId = "ext-123"
        };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("externalId").GetString().ShouldBe("ext-123");
    }

    [Fact]
    public async Task Replace_user_response_content_type_is_scim_json()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("jane");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "jane-updated" };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ScimHttpClient.ScimContentType);
    }

    [Fact]
    public async Task Replace_user_with_complex_attribute()
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

        var replacePayload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "alice",
            name = new { givenName = "Alice", familyName = "Johnson" }
        };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(replacePayload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<ScimUserWithName>();
        _ = user.ShouldNotBeNull();
        user.Name.ShouldBeEquivalentTo(new ScimName { GivenName = "Alice", FamilyName = "Johnson" });
    }

    [Fact]
    public async Task Replace_user_with_list_attribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync(
            "bob",
            new
            {
                emails = new[]
                {
                    new { value = "bob@old.com", type = "work", primary = true }
                }
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var replacePayload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "bob",
            emails = new[]
            {
                new { value = "bob@new.com", type = "work", primary = true },
                new { value = "bob@personal.com", type = "home", primary = false }
            }
        };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(replacePayload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<ScimUserWithEmails>();
        _ = user.ShouldNotBeNull();
        user.Emails.ShouldBeEquivalentTo(new[]
        {
            new ScimEmail { Value = "bob@new.com", Type = "work", Primary = true },
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
