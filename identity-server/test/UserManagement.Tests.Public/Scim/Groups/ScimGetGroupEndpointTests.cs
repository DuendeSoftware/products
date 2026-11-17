// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimGetGroupEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task GetGroupReturns200WithGroupResource()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("Engineering");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("id").GetString().ShouldBe(id);
        body.RootElement.GetProperty("displayName").GetString().ShouldBe("Engineering");
        var schemas = body.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        schemas.ShouldContain(ScimGroupHttpClient.GroupSchemaUrn);
        _ = body.RootElement.GetProperty("meta");
    }

    [Fact]
    public async Task GetGroupReturnsEtagHeader()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("Marketing");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag = ScimGroupHttpClient.GetETag(response);
        etag.ShouldNotBeNullOrEmpty();
        etag.ShouldStartWith("W/\"");
    }

    [Fact]
    public async Task GetGroupWithMatchingEtagReturns304()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("Finance");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var firstGet = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");
        firstGet.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag = firstGet.Headers.ETag!;

        var request = new HttpRequestMessage(HttpMethod.Get, $"{ScimGroupHttpClient.GroupsRoute}/{id}");
        request.Headers.IfNoneMatch.Add(etag);
        var secondGet = await Fixture.GroupClient.SendAsync(request);

        secondGet.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task GetNonexistentGroupReturns404()
    {
        await Fixture.InitializeAsync();

        var randomId = Guid.NewGuid();
        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{randomId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGroupWithInvalidIdReturns404()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/not-a-guid");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetGroupWithMembersReturnsMemberArray()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("alice");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("Team", new[] { userId });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var response = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var members = body.RootElement.GetProperty("members").EnumerateArray().ToList();
        members.Count.ShouldBeGreaterThanOrEqualTo(1);
        var member = members[0];
        member.GetProperty("value").GetString().ShouldBe(userId);
        member.GetProperty("$ref").GetString().ShouldNotBeNullOrEmpty();
        member.GetProperty("type").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetGroupWithExcludedMembersOmitsMembers()
    {
        await Fixture.InitializeAsync();

        var userId = await Fixture.CreateUserAsync("bob");
        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("TeamB", new[] { userId });
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
