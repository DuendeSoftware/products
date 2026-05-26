// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimGroupMemberTruncationTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task GetGroupReturnsAllMembersWhenBelowLimit()
    {
        Fixture.ConfigureScimCapabilities = opts => opts.MaxGroupMembersInResponse = 10;
        await Fixture.InitializeAsync();

        var user1 = await Fixture.CreateUserAsync("trunc-below-1");
        var user2 = await Fixture.CreateUserAsync("trunc-below-2");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "SmallGroup", [user1, user2]);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var members = body.RootElement.GetProperty("members").EnumerateArray().ToList();
        members.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetGroupTruncatesMembersWhenAboveLimit()
    {
        Fixture.ConfigureScimCapabilities = opts => opts.MaxGroupMembersInResponse = 2;
        await Fixture.InitializeAsync();

        var user1 = await Fixture.CreateUserAsync("trunc-above-1");
        var user2 = await Fixture.CreateUserAsync("trunc-above-2");
        var user3 = await Fixture.CreateUserAsync("trunc-above-3");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "BigGroup", [user1, user2, user3]);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var members = body.RootElement.GetProperty("members").EnumerateArray().ToList();
        members.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetGroupReturnsExactlyLimitMembersWhenAtLimit()
    {
        Fixture.ConfigureScimCapabilities = opts => opts.MaxGroupMembersInResponse = 3;
        await Fixture.InitializeAsync();

        var user1 = await Fixture.CreateUserAsync("trunc-at-1");
        var user2 = await Fixture.CreateUserAsync("trunc-at-2");
        var user3 = await Fixture.CreateUserAsync("trunc-at-3");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "ExactGroup", [user1, user2, user3]);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var members = body.RootElement.GetProperty("members").EnumerateArray().ToList();
        members.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetGroupWithExcludedMembersSkipsTruncationLogic()
    {
        Fixture.ConfigureScimCapabilities = opts => opts.MaxGroupMembersInResponse = 1;
        await Fixture.InitializeAsync();

        var user1 = await Fixture.CreateUserAsync("trunc-excl-1");
        var user2 = await Fixture.CreateUserAsync("trunc-excl-2");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync(
            "ExcludedGroup", [user1, user2]);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var response = await Fixture.GroupClient.GetAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{id}?excludedAttributes=members");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.TryGetProperty("members", out _).ShouldBeFalse();
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
