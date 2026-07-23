// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimPasswordTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    // Satisfies default PasswordOptions: 2+ upper, 2+ lower, 2+ digits, 2+ symbols, 8+ length
    private const string ValidPassword = "ABcd12!@";
    private const string AltPassword = "XYzw34#$";
    private const string WeakPassword = "short";

    private readonly ScimFixture Fixture = new(output, serverFixture);

    // ── POST (create) ─────────────────────────────────────────────────────────

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

    // ── PUT password tests ────────────────────────────────────────────────────

    [Fact]
    public async Task ReplaceUserWithPassword_SetsPassword()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("alice-put-pw");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var subjectId = UserSubjectId.Create(id);
        var ct = TestContext.Current.CancellationToken;

        var payload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "alice-put-pw",
            password = ValidPassword
        };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var authenticators = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        _ = authenticators.ShouldNotBeNull();
        authenticators!.HasPassword.ShouldBeTrue();
    }

    [Fact]
    public async Task ReplaceUserWithPassword_DoesNotReturnPasswordInResponse()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("bob-put-pw");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "bob-put-pw",
            password = ValidPassword
        };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.TryGetProperty("password", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ReplaceUserWithoutPassword_PreservesExistingPassword()
    {
        await Fixture.InitializeAsync();
        var ct = TestContext.Current.CancellationToken;

        // Create user with a password
        var (createResponse, createBody) = await Fixture.Client.CreateUserWithPasswordAsync("carol-put-pw", ValidPassword);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var subjectId = UserSubjectId.Create(id);

        // Confirm password is set before the PUT
        var beforeAuth = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        _ = beforeAuth.ShouldNotBeNull();
        beforeAuth!.HasPassword.ShouldBe(true);

        // PUT without password field
        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "carol-put-pw" };
        var putResponse = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Password authenticator record should remain intact
        var afterAuth = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        _ = afterAuth.ShouldNotBeNull();
        afterAuth!.HasPassword.ShouldBe(true);
    }

    [Fact]
    public async Task ReplaceUserWithWeakPassword_Returns400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("dave-put-pw");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "dave-put-pw",
            password = WeakPassword
        };
        var response = await Fixture.Client.PutAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidValue");
        ShouldlyExtensions.ShouldContain(body.RootElement.GetProperty("detail").GetString()!, "password");
    }

    // ── PATCH password tests ──────────────────────────────────────────────────

    [Fact]
    public async Task PatchUserAddPassword_SetsPassword()
    {
        await Fixture.InitializeAsync();
        var ct = TestContext.Current.CancellationToken;
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("alice-patch-pw");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var subjectId = UserSubjectId.Create(id);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "add", path = "password", value = ValidPassword }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var authenticators = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        _ = authenticators.ShouldNotBeNull();
        authenticators!.HasPassword.ShouldBe(true);
    }

    [Fact]
    public async Task PatchUserReplacePassword_ChangesPassword()
    {
        await Fixture.InitializeAsync();
        var ct = TestContext.Current.CancellationToken;
        var (createResponse, createBody) = await Fixture.Client.CreateUserWithPasswordAsync("bob-patch-pw", ValidPassword);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var subjectId = UserSubjectId.Create(id);

        // Replace with a different valid password
        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "replace", path = "password", value = AltPassword }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Password should still be set (updated, not removed)
        var authenticators = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        _ = authenticators.ShouldNotBeNull();
        authenticators!.HasPassword.ShouldBe(true);
    }

    [Fact]
    public async Task PatchUserRemovePassword_ClearsPassword()
    {
        await Fixture.InitializeAsync();
        var ct = TestContext.Current.CancellationToken;
        var (createResponse, createBody) = await Fixture.Client.CreateUserWithPasswordAsync("carol-patch-pw", ValidPassword);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var subjectId = UserSubjectId.Create(id);

        // Confirm password exists before removal
        var beforeAuth = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        _ = beforeAuth.ShouldNotBeNull();
        beforeAuth!.HasPassword.ShouldBe(true);

        // PATCH remove password — must use raw JSON because anonymous type would serialize value=null
        var json = $"{{\"schemas\":[\"{ScimHttpClient.PatchOpSchemaUrn}\"],\"Operations\":[{{\"op\":\"remove\",\"path\":\"password\"}}]}}";
        var content = new StringContent(json, Encoding.UTF8, ScimHttpClient.ScimContentType);
        var response = await Fixture.Client.PatchAsync($"{ScimHttpClient.UsersRoute}/{id}", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterAuth = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        _ = afterAuth.ShouldNotBeNull();
        afterAuth!.HasPassword.ShouldBe(false);
    }

    [Fact]
    public async Task PatchUserPasswordViaValueObjectWithoutPath_SetsPassword()
    {
        await Fixture.InitializeAsync();
        var ct = TestContext.Current.CancellationToken;
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("dave-patch-pw");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var subjectId = UserSubjectId.Create(id);

        // PATCH with no path; value is an object containing "password" and other attributes
        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "replace", value = new { password = ValidPassword, userName = "dave-patch-pw" } }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var authenticators = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        _ = authenticators.ShouldNotBeNull();
        authenticators!.HasPassword.ShouldBe(true);
    }

    [Fact]
    public async Task PatchUserPasswordOnUserWithoutAuthenticators_CreatesAuthenticators()
    {
        await Fixture.InitializeAsync();
        var ct = TestContext.Current.CancellationToken;

        // Create user WITHOUT password — no authenticator record should exist initially
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("eve-patch-pw");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var subjectId = UserSubjectId.Create(id);

        // Confirm no authenticator record exists yet
        var beforeAuth = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        beforeAuth.ShouldBeNull();

        // PATCH add password — should create the authenticator record in the same batch
        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "add", path = "password", value = ValidPassword }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterAuth = await Fixture.AuthenticatorsAdmin.TryGetAsync(subjectId, ct);
        _ = afterAuth.ShouldNotBeNull();
        afterAuth!.HasPassword.ShouldBe(true);
    }

    [Fact]
    public async Task PatchUserWithWeakPassword_Returns400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("frank-patch-pw");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "add", path = "password", value = WeakPassword }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidValue");
        ShouldlyExtensions.ShouldContain(body.RootElement.GetProperty("detail").GetString()!, "password");
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
