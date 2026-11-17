// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimDeleteUserEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task Delete_user_returns_204_no_content()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("alice");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await Fixture.Client.DeleteAsync($"{ScimHttpClient.UsersRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task Delete_user_then_get_returns_404()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("bob");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var deleteResponse = await Fixture.Client.DeleteAsync($"{ScimHttpClient.UsersRoute}/{id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_nonexistent_user_returns_404()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.DeleteAsync($"{ScimHttpClient.UsersRoute}/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var schemas = body.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        schemas.ShouldContain(ScimHttpClient.ErrorSchemaUrn);
    }

    [Fact]
    public async Task Delete_user_with_invalid_id_returns_404()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.DeleteAsync($"{ScimHttpClient.UsersRoute}/not-a-guid");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_user_with_if_match_matching_returns_204()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("charlie");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var etag = createResponse.Headers.ETag!;

        var request = new HttpRequestMessage(HttpMethod.Delete, $"{ScimHttpClient.UsersRoute}/{id}");
        request.Headers.IfMatch.Add(etag);
        var response = await Fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_user_with_if_match_mismatched_returns_412()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("dave");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"{ScimHttpClient.UsersRoute}/{id}");
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"99999\"", isWeak: true));
        var response = await Fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        ShouldlyExtensions.ShouldContain(body.RootElement.GetProperty("detail").GetString()!, "Precondition failed");
    }

    [Fact]
    public async Task Delete_user_is_idempotent_second_delete_returns_404()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("eve");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var firstDelete = await Fixture.Client.DeleteAsync($"{ScimHttpClient.UsersRoute}/{id}");
        firstDelete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var secondDelete = await Fixture.Client.DeleteAsync($"{ScimHttpClient.UsersRoute}/{id}");
        secondDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
