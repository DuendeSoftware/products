// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimListGroupsEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task ListGroupsReturnsAllGroups()
    {
        await Fixture.InitializeAsync();

        var (r1, _) = await Fixture.GroupClient.CreateGroupAsync("GroupA");
        var (r2, _) = await Fixture.GroupClient.CreateGroupAsync("GroupB");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.GroupClient.GetAsync(ScimGroupHttpClient.GroupsRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(2);
        _ = body.RootElement.GetProperty("Resources");
    }

    [Fact]
    public async Task ListGroupsWithPagination()
    {
        await Fixture.InitializeAsync();

        var (r1, _) = await Fixture.GroupClient.CreateGroupAsync("Page1");
        var (r2, _) = await Fixture.GroupClient.CreateGroupAsync("Page2");
        var (r3, _) = await Fixture.GroupClient.CreateGroupAsync("Page3");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);
        r3.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}?startIndex=2&count=1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("startIndex").GetInt32().ShouldBe(2);
        body.RootElement.GetProperty("itemsPerPage").GetInt32().ShouldBe(1);
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(3);
        body.RootElement.GetProperty("Resources").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task ListGroupsWithDisplayNameFilter()
    {
        await Fixture.InitializeAsync();

        var (r1, _) = await Fixture.GroupClient.CreateGroupAsync("Engineers");
        var (r2, _) = await Fixture.GroupClient.CreateGroupAsync("Marketing");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.GroupClient.GetAsync(
            $"{ScimGroupHttpClient.GroupsRoute}?filter=displayName eq \"Engineers\"");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(1);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources[0].GetProperty("displayName").GetString().ShouldBe("Engineers");
    }

    [Fact]
    public async Task ListGroupsWithContainsFilter()
    {
        await Fixture.InitializeAsync();

        var (r1, _) = await Fixture.GroupClient.CreateGroupAsync("Engineers");
        var (r2, _) = await Fixture.GroupClient.CreateGroupAsync("Marketing");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.GroupClient.GetAsync(
            $"{ScimGroupHttpClient.GroupsRoute}?filter=displayName co \"Eng\"");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.ShouldContain(r => r.GetProperty("displayName").GetString()!.Contains("Eng"));
    }

    [Fact]
    public async Task ListGroupsEmptyResultReturnsZeroTotalResults()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.GroupClient.GetAsync(ScimGroupHttpClient.GroupsRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(0);
        body.RootElement.GetProperty("Resources").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ListGroupsDoesNotIncludeMembers()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("member1");
        var (createResponse, _) = await Fixture.GroupClient.CreateGroupAsync("TeamC", new[] { userId });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Fixture.GroupClient.GetAsync(ScimGroupHttpClient.GroupsRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBeGreaterThanOrEqualTo(1);
        foreach (var resource in resources)
        {
            resource.TryGetProperty("members", out _).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task ListGroupsIncludesMembersWhenRequested()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("attr-member");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("AttrTeam", new[] { userId });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var groupId = createBody.RootElement.GetProperty("id").GetString();

        var response = await Fixture.GroupClient.GetAsync(
            $"{ScimGroupHttpClient.GroupsRoute}?attributes=members");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBeGreaterThanOrEqualTo(1);
        var group = resources.Single(r => r.GetProperty("id").GetString() == groupId);
        group.TryGetProperty("members", out var members).ShouldBeTrue();
        members.GetArrayLength().ShouldBe(1);
        members[0].GetProperty("value").GetString().ShouldBe(userId);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
