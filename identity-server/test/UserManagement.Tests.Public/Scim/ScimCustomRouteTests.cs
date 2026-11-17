// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

/// <summary>
/// Verifies that all 7 SCIM user endpoints honour a custom base route configured
/// via <c>ScimEndpointOptions.Route</c>.
/// </summary>
public sealed class ScimCustomRouteTests(ITestOutputHelper output, WebServerFixture serverFixture) : IAsyncLifetime, IAsyncDisposable
{
    private const string CustomRoute = "/api/different";
    private readonly ScimFixture Fixture = new(output, serverFixture);

    private ScimHttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        Fixture.ConfigureScimOptions += options =>
        {
            options.Route = CustomRoute;
        };

        await Fixture.InitializeAsync();

        _client = Fixture.BuildScimClient(CustomRoute);
        _client.SetBearerToken(ScimFixture.CreateAccessToken("scim"));
    }

    [Fact]
    public async Task Create_user_at_custom_route_returns_201()
    {
        var (response, body) = await _client.CreateUserAsync("alice");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var location = response.Headers.Location?.ToString();
        _ = location.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(location, CustomRoute);
    }

    [Fact]
    public async Task Create_user_meta_location_uses_custom_route()
    {
        var (response, body) = await _client.CreateUserAsync("bob");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var metaLocation = body.RootElement.GetProperty("meta").GetProperty("location").GetString();
        _ = metaLocation.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(metaLocation, CustomRoute);
    }

    [Fact]
    public async Task Get_user_at_custom_route_returns_200()
    {
        var (created, createBody) = await _client.CreateUserAsync("charlie");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await _client.GetAsync($"{CustomRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var metaLocation = (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("meta").GetProperty("location").GetString();
        _ = metaLocation.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(metaLocation, CustomRoute);
    }

    [Fact]
    public async Task List_users_at_custom_route_returns_200()
    {
        var (created, _) = await _client.CreateUserAsync("dave");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await _client.GetAsync(CustomRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        var metaLocation = resources[0].GetProperty("meta").GetProperty("location").GetString();
        _ = metaLocation.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(metaLocation, CustomRoute);
    }

    [Fact]
    public async Task Search_users_at_custom_route_returns_200()
    {
        var (created, _) = await _client.CreateUserAsync("eve");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn } };
        var response = await _client.PostAsync(
            $"{CustomRoute}/.search", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        var metaLocation = resources[0].GetProperty("meta").GetProperty("location").GetString();
        _ = metaLocation.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(metaLocation, CustomRoute);
    }

    [Fact]
    public async Task Replace_user_at_custom_route_returns_200()
    {
        var (created, createBody) = await _client.CreateUserAsync("frank");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "frank-v2" };
        var response = await _client.PutAsync(
            $"{CustomRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var metaLocation = body.RootElement.GetProperty("meta").GetProperty("location").GetString();
        _ = metaLocation.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(metaLocation, CustomRoute);
    }

    [Fact]
    public async Task Patch_user_at_custom_route_returns_200()
    {
        var (created, createBody) = await _client.CreateUserAsync("grace");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "userName", value = "grace-v2" } }
        };
        var response = await _client.PatchAsync(
            $"{CustomRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var metaLocation = body.RootElement.GetProperty("meta").GetProperty("location").GetString();
        _ = metaLocation.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(metaLocation, CustomRoute);
    }

    [Fact]
    public async Task Delete_user_at_custom_route_returns_204()
    {
        var (created, createBody) = await _client.CreateUserAsync("harry");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var response = await _client.DeleteAsync($"{CustomRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Default_route_returns_404_when_custom_route_configured()
    {
        var response = await _client.GetAsync(ScimHttpClient.UsersRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }

}
