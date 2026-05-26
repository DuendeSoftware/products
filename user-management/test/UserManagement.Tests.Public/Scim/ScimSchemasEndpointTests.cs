// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimSchemasEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private const string ListRoute = "/scim/Schemas";
    private const string UserSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:User";
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task list_includes_User_schema()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync(ListRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("totalResults").GetInt32().ShouldBeGreaterThan(0);
        var resources = body.RootElement.GetProperty("Resources").EnumerateArray().ToList();
        resources.ShouldContain(s => s.GetProperty("id").GetString() == UserSchemaUrn);
    }

    [Fact]
    public async Task get_User_schema_includes_userName_attribute()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ListRoute}/{UserSchemaUrn}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("id").GetString().ShouldBe(UserSchemaUrn);
        body.RootElement.GetProperty("name").GetString().ShouldBe("User");
        var attributes = body.RootElement.GetProperty("attributes").EnumerateArray().ToList();
        attributes.ShouldContain(a => a.GetProperty("name").GetString() == "userName");
    }

    [Fact]
    public async Task get_User_schema_userName_is_required_with_server_uniqueness()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ListRoute}/{UserSchemaUrn}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var userName = body.RootElement.GetProperty("attributes").EnumerateArray()
            .Single(a => a.GetProperty("name").GetString() == "userName");
        userName.GetProperty("required").GetBoolean().ShouldBeTrue();
        userName.GetProperty("uniqueness").GetString().ShouldBe("server");
    }

    [Fact]
    public async Task get_User_schema_includes_password_when_ChangePassword_enabled()
    {
        Fixture.ConfigureScimCapabilities += opts => opts.ChangePassword = true;
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ListRoute}/{UserSchemaUrn}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var password = body.RootElement.GetProperty("attributes").EnumerateArray()
            .Single(a => a.GetProperty("name").GetString() == "password");
        password.GetProperty("mutability").GetString().ShouldBe("writeOnly");
        password.GetProperty("returned").GetString().ShouldBe("never");
    }

    [Fact]
    public async Task get_User_schema_excludes_password_when_ChangePassword_disabled()
    {
        Fixture.ConfigureScimCapabilities += opts => opts.ChangePassword = false;
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ListRoute}/{UserSchemaUrn}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var attributes = body.RootElement.GetProperty("attributes").EnumerateArray().ToList();
        attributes.ShouldNotContain(a => a.GetProperty("name").GetString() == "password");
    }

    [Fact]
    public async Task get_User_schema_meta_has_location()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ListRoute}/{UserSchemaUrn}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var meta = body.RootElement.GetProperty("meta");
        meta.GetProperty("resourceType").GetString().ShouldBe("Schema");
        ShouldlyExtensions.ShouldContain(meta.GetProperty("location").GetString()!, UserSchemaUrn);
    }

    [Fact]
    public async Task get_unknown_schema_returns_404()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync($"{ListRoute}/urn:unknown:schema");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
