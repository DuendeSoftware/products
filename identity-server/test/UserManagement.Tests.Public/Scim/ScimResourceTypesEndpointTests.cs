// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimResourceTypesEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private const string ListRoute = "/scim/ResourceTypes";
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task list_includes_User_resource_type()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync(ListRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBeGreaterThan(0);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.ShouldContain(r => r.GetProperty("name").GetString() == "User");
    }

    [Fact]
    public async Task get_User_returns_correct_resource_type()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ListRoute}/User");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("id").GetString().ShouldBe("User");
        body.RootElement.GetProperty("schema").GetString()
            .ShouldBe("urn:ietf:params:scim:schemas:core:2.0:User");
        body.RootElement.GetProperty("endpoint").GetString().ShouldBe("/scim/Users");
    }

    [Fact]
    public async Task get_unknown_resource_type_returns_404()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ListRoute}/DoesNotExist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
