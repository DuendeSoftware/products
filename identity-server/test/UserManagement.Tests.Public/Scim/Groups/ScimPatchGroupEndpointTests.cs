// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimPatchGroupEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task PatchReplaceDisplayName()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("PatchGroup");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "replace", path = "displayName", value = "PatchedGroup" }
            }
        };
        var response = await Fixture.GroupClient.PatchAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("displayName").GetString().ShouldBe("PatchedGroup");
    }

    [Fact]
    public async Task PatchAddMembers()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("patch-add-member");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("PatchAddGroup");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "add", path = "members", value = new[] { new { value = userId } } }
            }
        };
        var response = await Fixture.GroupClient.PatchAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        var members = body.RootElement.GetProperty("members").EnumerateArray().ToList();
        members.Count.ShouldBeGreaterThanOrEqualTo(1);
        members.Select(m => m.GetProperty("value").GetString()).ShouldContain(userId);
    }

    [Fact]
    public async Task PatchRemoveMembersByValue()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("patch-remove-member");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "PatchRemoveGroup", new[] { userId });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "remove", path = "members", value = new[] { new { value = userId } } }
            }
        };
        var response = await Fixture.GroupClient.PatchAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        body.RootElement.TryGetProperty("members", out var membersElement).ShouldBeFalse();
        _ = membersElement;
    }

    [Fact]
    public async Task PatchRemoveMemberByFilter()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("patch-filter-member");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "PatchFilterGroup", new[] { userId });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var filterPath = $"members[value eq \"{userId}\"]";
        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "remove", path = filterPath }
            }
        };
        var content = ScimGroupHttpClient.ScimJsonContent(payload);
        var response = await Fixture.GroupClient.PatchAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        if (body.RootElement.TryGetProperty("members", out var membersEl))
        {
            var remaining = membersEl.EnumerateArray()
                .Select(m => m.GetProperty("value").GetString())
                .ToList();
            remaining.ShouldNotContain(userId);
        }
    }

    [Fact]
    public async Task PatchReplaceMembers()
    {
        await Fixture.InitializeAsync();

        var userId1 = await Fixture.CreateUserAsync("patch-replace-1");
        var userId2 = await Fixture.CreateUserAsync("patch-replace-2");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "PatchReplaceGroup", new[] { userId1 });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "replace", path = "members", value = new[] { new { value = userId2 } } }
            }
        };
        var response = await Fixture.GroupClient.PatchAsync(
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
    public async Task PatchAddNonexistentMemberSucceeds()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("PatchBadMember");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var nonExistentUserId = Guid.NewGuid().ToString();
        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "add", path = "members", value = new[] { new { value = nonExistentUserId } } }
            }
        };
        var response = await Fixture.GroupClient.PatchAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchReplaceWithNonexistentMemberSucceeds()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("patch-replace-existing");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "PatchReplaceBadMember", [userId]);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var nonExistentUserId = Guid.NewGuid().ToString();
        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "replace", path = "members", value = new[] { new { value = nonExistentUserId } } }
            }
        };
        var response = await Fixture.GroupClient.PatchAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchReplaceNoPath_with_members_replaces_membership()
    {
        await Fixture.InitializeAsync();

        var userId1 = await Fixture.CreateUserAsync("patch-nopath-replace-1");
        var userId2 = await Fixture.CreateUserAsync("patch-nopath-replace-2");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "PatchNoPathReplace", [userId1]);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        // replace with no path — members key should replace, not add
        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new
                {
                    op = "replace",
                    value = new { members = new[] { new { value = userId2 } } }
                }
            }
        };
        var response = await Fixture.GroupClient.PatchAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}", ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        var members = body.RootElement.GetProperty("members").EnumerateArray().ToList();
        // userId1 should be gone (replaced), only userId2 should remain
        members.Count.ShouldBe(1);
        members[0].GetProperty("value").GetString().ShouldBe(userId2);
    }

    [Fact]
    public async Task PatchNonexistentGroupReturns404()
    {
        await Fixture.InitializeAsync();

        var payload = new
        {
            schemas = new[] { ScimGroupHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "replace", path = "displayName", value = "X" }
            }
        };
        var response = await Fixture.GroupClient.PatchAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{Guid.NewGuid()}",
            ScimGroupHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
