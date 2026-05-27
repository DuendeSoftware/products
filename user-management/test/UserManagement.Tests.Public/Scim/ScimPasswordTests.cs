// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimPasswordTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable, IAsyncLifetime
{
    // Satisfies default PasswordOptions: 2+ upper, 2+ lower, 2+ digits, 2+ symbols, 8+ length
    private const string ValidPassword = "ABcd12!@";
    private const string WeakPassword = "short";

    private readonly ScimFixture Fixture = new(output, serverFixture);

    public async ValueTask InitializeAsync() => await Task.CompletedTask; // modules registered unconditionally

    [Fact]
    public async Task CreateUserWithPassword_ReturnsCreated()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.CreateUserWithPasswordAsync("alice", ValidPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        ScimHttpClient.GetUserId(body).ShouldNotBeNullOrEmpty();
        body.RootElement.GetProperty("userName").GetString().ShouldBe("alice");
    }

    [Fact]
    public async Task CreateUserWithPassword_DoesNotReturnPasswordInResponse()
    {
        await Fixture.InitializeAsync();

        var (_, body) = await Fixture.Client.CreateUserWithPasswordAsync("bob", ValidPassword);

        body.RootElement.TryGetProperty("password", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateUserWithPassword_PasswordNotReturnedInGetResponse()
    {
        await Fixture.InitializeAsync();

        var (_, createBody) = await Fixture.Client.CreateUserWithPasswordAsync("carol", ValidPassword);
        var id = ScimHttpClient.GetUserId(createBody);

        var getResponse = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var getBody = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        getBody.RootElement.TryGetProperty("password", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateUserWithWeakPassword_Returns400()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.CreateUserWithPasswordAsync("dave", WeakPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidValue");
        ShouldlyExtensions.ShouldContain(body.RootElement.GetProperty("detail").GetString()!, "password");
    }

    [Fact]
    public async Task CreateDuplicateUserWithPassword_Returns409()
    {
        await Fixture.InitializeAsync();

        var (first, _) = await Fixture.Client.CreateUserWithPasswordAsync("frank", ValidPassword);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var (second, body) = await Fixture.Client.CreateUserWithPasswordAsync("frank", ValidPassword);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("uniqueness");
    }

    [Fact]
    public async Task CreateUserWithoutPassword_WhenAuthEnabled_ReturnsCreated()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.CreateUserAsync("grace");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        ScimHttpClient.GetUserId(body).ShouldNotBeNullOrEmpty();
        body.RootElement.TryGetProperty("password", out _).ShouldBeFalse();
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }

}
