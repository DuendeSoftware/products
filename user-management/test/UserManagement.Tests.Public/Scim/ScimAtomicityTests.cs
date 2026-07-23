// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimAtomicityTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task create_user_with_duplicate_userName_fails_atomically()
    {
        await Fixture.InitializeAsync();

        var (firstResponse, _) = await Fixture.Client.CreateUserAsync("alice");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var (secondResponse, secondBody) = await Fixture.Client.CreateUserAsync("alice");
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        secondBody.RootElement.GetProperty("scimType").GetString().ShouldBe("uniqueness");

        // Verify no partial user was created: only 1 user exists with userName "alice"
        var ct = TestContext.Current.CancellationToken;
        var profile = await Fixture.UserProfileAdmin.TryGetAsync(
            AttributeCode.Create("username"),
            "alice",
            ct);
        _ = profile.ShouldNotBeNull();

        // Confirm there is exactly one user in total (no extra partial record)
        var allUsers = await Fixture.UserProfileAdmin.QueryAsync(
            Duende.Storage.Querying.QueryRequest.Create(),
            ct);
        allUsers.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task replace_user_with_duplicate_userName_fails_atomically()
    {
        await Fixture.InitializeAsync();

        var (rA, _) = await Fixture.Client.CreateUserAsync("alice");
        rA.StatusCode.ShouldBe(HttpStatusCode.Created);

        var (rB, bB) = await Fixture.Client.CreateUserAsync("bob");
        rB.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bobId = ScimHttpClient.GetUserId(bB);

        // Attempt to replace bob with alice's userName — must conflict
        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "alice" };
        var putResponse = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{bobId}", ScimHttpClient.ScimJsonContent(payload));

        putResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var putBody = await JsonDocument.ParseAsync(await putResponse.Content.ReadAsStreamAsync());
        putBody.RootElement.GetProperty("scimType").GetString().ShouldBe("uniqueness");

        // Verify bob's userName is still "bob" — no partial update applied
        var getResponse = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{bobId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var getBody = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        getBody.RootElement.GetProperty("userName").GetString().ShouldBe("bob");
    }

    [Fact]
    public async Task patch_user_with_duplicate_userName_fails_atomically()
    {
        await Fixture.InitializeAsync();

        var (rA, _) = await Fixture.Client.CreateUserAsync("alice");
        rA.StatusCode.ShouldBe(HttpStatusCode.Created);

        var (rB, bB) = await Fixture.Client.CreateUserAsync("bob");
        rB.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bobId = ScimHttpClient.GetUserId(bB);

        // Attempt to PATCH bob's userName to alice's — must conflict
        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "userName", value = "alice" } }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{bobId}", ScimHttpClient.ScimJsonContent(payload));

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var patchBody = await JsonDocument.ParseAsync(await patchResponse.Content.ReadAsStreamAsync());
        patchBody.RootElement.GetProperty("scimType").GetString().ShouldBe("uniqueness");

        // Verify bob's userName is still "bob" — no partial update applied
        var getResponse = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{bobId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var getBody = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        getBody.RootElement.GetProperty("userName").GetString().ShouldBe("bob");
    }

    [Fact]
    public async Task ReplaceUserWithWeakPassword_DoesNotUpdateProfile()
    {
        await Fixture.InitializeAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("atomic-pw-user");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        // Attempt to replace with a new userName AND a weak password — should fail
        var payload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "atomic-pw-user-renamed",
            password = "weak"
        };
        var putResponse = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        putResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Verify userName was NOT updated — profile is unchanged
        var getResponse = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var getBody = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        getBody.RootElement.GetProperty("userName").GetString().ShouldBe("atomic-pw-user");
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
