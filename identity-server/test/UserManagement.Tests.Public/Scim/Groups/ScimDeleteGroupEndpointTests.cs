// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimDeleteGroupEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task DeleteGroupReturns204()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("DeleteMe");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var response = await Fixture.GroupClient.DeleteAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task DeleteNonexistentGroupReturns404()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.GroupClient.DeleteAsync(
            $"{ScimGroupHttpClient.GroupsRoute}/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAfterDeleteReturns404()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.GroupClient.CreateGroupAsync("DeleteThenGet");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimGroupHttpClient.GetGroupId(createBody);

        var deleteResponse = await Fixture.GroupClient.DeleteAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await Fixture.GroupClient.GetAsync($"{ScimGroupHttpClient.GroupsRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
