// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimReplaceGroupEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task ReplaceGroupUpdatesDisplayName()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("OldName");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);
        var originalEtag = ScimGroupHttpClient.GetETag(createResponse);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn },
            displayName = "NewName"
        };
        var response = await Fixture.GroupClient.PutAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("displayName").GetString().ShouldBe("NewName");

        var newEtag = ScimGroupHttpClient.GetETag(response);
        newEtag.ShouldNotBe(originalEtag);
    }

    [Fact]
    public async Task ReplaceGroupUpdatesMembers()
    {
        await Fixture.InitializeAsync();

        var userId1 = await Fixture.CreateUserAsync("replace-member-1");
        var userId2 = await Fixture.CreateUserAsync("replace-member-2");

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "ReplaceGroup", new[] { userId1 });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn },
            displayName = "ReplaceGroup",
            members = new[] { new { value = userId2 } }
        };
        var response = await Fixture.GroupClient.PutAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        var members = body.RootElement.GetProperty("members").EnumerateArray().ToList();
        members.Count.ShouldBe(1);
        members[0].GetProperty("value").GetString().ShouldBe(userId2);
    }

    [Fact]
    public async Task ReplaceNonexistentGroupReturns404()
    {
        await Fixture.InitializeAsync();

        var randomId = Guid.NewGuid();
        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn },
            displayName = "Ghost"
        };
        var response = await Fixture.GroupClient.PutAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{randomId}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReplaceGroupWithNonexistentMemberSucceeds()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("ReplaceNonexistentMember");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var nonExistentUserId = Guid.NewGuid().ToString();
        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn },
            displayName = "ReplaceNonexistentMember",
            members = new[] { new { value = nonExistentUserId } }
        };
        var response = await Fixture.GroupClient.PutAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReplaceGroupWithoutDisplayNameReturns400()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("GroupToReplace");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var payload = new { schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn } };
        var response = await Fixture.GroupClient.PutAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
