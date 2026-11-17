// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimSearchGroupsEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);
    private static readonly string SearchRoute = $"{ScimGroupHttpClient.GroupsRoute}/.search";

    [Fact]
    public async Task SearchGroupsWithFilterReturnsMatchingGroups()
    {
        await Fixture.InitializeAsync();

        var (r1, _) = await Fixture.GroupClient.CreateGroupAsync("SearchEngineers");
        var (r2, _) = await Fixture.GroupClient.CreateGroupAsync("SearchMarketing");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.SearchRequestSchemaUrn },
            filter = "displayName eq \"SearchEngineers\""
        };
        var response = await Fixture.GroupClient.PostAsync(SearchRoute, ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(1);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources[0].GetProperty("displayName").GetString().ShouldBe("SearchEngineers");
    }

    [Fact]
    public async Task SearchGroupsWithPagination()
    {
        await Fixture.InitializeAsync();

        var (r1, _) = await Fixture.GroupClient.CreateGroupAsync("SearchPage1");
        var (r2, _) = await Fixture.GroupClient.CreateGroupAsync("SearchPage2");
        var (r3, _) = await Fixture.GroupClient.CreateGroupAsync("SearchPage3");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);
        r3.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.SearchRequestSchemaUrn },
            startIndex = 2,
            count = 1
        };
        var response = await Fixture.GroupClient.PostAsync(SearchRoute, ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("startIndex").GetInt32().ShouldBe(2);
        body.RootElement.GetProperty("itemsPerPage").GetInt32().ShouldBe(1);
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBe(3);
        body.RootElement.GetProperty("Resources").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task SearchGroupsWithInvalidFilterReturns400()
    {
        await Fixture.InitializeAsync();

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.SearchRequestSchemaUrn },
            filter = "!!!"
        };
        var response = await Fixture.GroupClient.PostAsync(SearchRoute, ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidFilter");
    }

    [Fact]
    public async Task SearchGroupsIncludesMembersWhenRequested()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("search-member");
        var (createResponse, _) = await Fixture.GroupClient.CreateGroupAsync("SearchAttrTeam", new[] { userId });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.SearchRequestSchemaUrn },
            filter = "displayName eq \"SearchAttrTeam\"",
            attributes = new[] { "members" }
        };
        var response = await Fixture.GroupClient.PostAsync(SearchRoute, ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBe(1);
        var group = resources[0];
        group.TryGetProperty("members", out var members).ShouldBeTrue();
        members.GetArrayLength().ShouldBe(1);
        members[0].GetProperty("value").GetString().ShouldBe(userId);
    }

    [Fact]
    public async Task SearchGroupsDoesNotIncludeMembersByDefault()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("search-no-member");
        var (createResponse, _) = await Fixture.GroupClient.CreateGroupAsync("SearchNoMemTeam", new[] { userId });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.SearchRequestSchemaUrn },
            filter = "displayName eq \"SearchNoMemTeam\""
        };
        var response = await Fixture.GroupClient.PostAsync(SearchRoute, ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.Count.ShouldBe(1);
        resources[0].TryGetProperty("members", out _).ShouldBeFalse();
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
