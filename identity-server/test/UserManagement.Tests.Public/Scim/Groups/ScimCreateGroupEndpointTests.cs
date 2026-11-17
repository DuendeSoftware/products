// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimCreateGroupEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task CreateGroupReturns201WithLocationAndEtag()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.GroupClient.CreateGroupAsync("NewGroup");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var id = ScimGroupHttpClient.GetGroupId(body);
        id.ShouldNotBeNullOrEmpty();

        _ = response.Headers.Location.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(response.Headers.Location!.ToString(), $"/scim/Groups/{id}");

        var etag = ScimGroupHttpClient.GetETag(response);
        etag.ShouldNotBeNullOrEmpty();
        etag.ShouldStartWith("W/\"");

        var schemas = body.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        schemas.ShouldContain(ScimGroupHttpClient.GroupSchemaUrn);

        body.RootElement.GetProperty("displayName").GetString().ShouldBe("NewGroup");

        var meta = body.RootElement.GetProperty("meta");
        meta.GetProperty("resourceType").GetString().ShouldBe("Group");
        meta.GetProperty("location").GetString().ShouldNotBeNullOrEmpty();
        meta.GetProperty("version").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateGroupWithoutDisplayNameReturns400()
    {
        await Fixture.InitializeAsync();

        var payload = new { schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn } };
        var response = await Fixture.GroupClient.PostAsync(
            ScimGroupHttpClient.GroupsRoute, ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateGroupWithMembersCreatesMemberships()
    {
        await Fixture.InitializeAsync();

        var userId1 = await Fixture.CreateUserAsync("member-a");
        var userId2 = await Fixture.CreateUserAsync("member-b");

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "MemberGroup", new[] { userId1, userId2 });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var members = body.RootElement.GetProperty("members").EnumerateArray().ToList();
        members.Count.ShouldBe(2);
        var memberIds = members.Select(m => m.GetProperty("value").GetString()).ToList();
        memberIds.ShouldContain(userId1);
        memberIds.ShouldContain(userId2);
    }

    [Fact]
    public async Task CreateGroupWithNonexistentMemberSucceeds()
    {
        await Fixture.InitializeAsync();

        var nonExistentUserId = Guid.NewGuid().ToString();

        var (response, _) = await Fixture.GroupClient.CreateGroupAsync(
            "BadMemberGroup", [nonExistentUserId]);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateGroupWithMixOfExistingAndNonexistentMembersSucceeds()
    {
        await Fixture.InitializeAsync();

        var realUserId = await Fixture.CreateUserAsync("create-mix-member");
        var nonExistentUserId = Guid.NewGuid().ToString();

        var (response, _) = await Fixture.GroupClient.CreateGroupAsync(
            "MixedMemberGroup", [realUserId, nonExistentUserId]);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatedGroupIsRetrievableViaGet()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("RetrievableGroup");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var getResponse = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("id").GetString().ShouldBe(id);
        body.RootElement.GetProperty("displayName").GetString().ShouldBe("RetrievableGroup");
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
