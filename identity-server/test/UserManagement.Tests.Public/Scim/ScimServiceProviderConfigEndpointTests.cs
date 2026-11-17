// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimServiceProviderConfigEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private const string Route = "/scim/ServiceProviderConfig";
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task returns_200_with_valid_config()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync(Route);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(e => e.GetString())
            .ShouldContain("urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig");
        body.RootElement.GetProperty("patch").GetProperty("supported").GetBoolean().ShouldBeTrue();
        var bulk = body.RootElement.GetProperty("bulk");
        bulk.GetProperty("supported").GetBoolean().ShouldBeTrue();
        bulk.GetProperty("maxOperations").GetInt32().ShouldBe(100);
        bulk.GetProperty("maxPayloadSize").GetInt32().ShouldBe(1_048_576);
        body.RootElement.GetProperty("filter").GetProperty("supported").GetBoolean().ShouldBeTrue();
        body.RootElement.GetProperty("sort").GetProperty("supported").GetBoolean().ShouldBeTrue();
        body.RootElement.GetProperty("etag").GetProperty("supported").GetBoolean().ShouldBeTrue();
        body.RootElement.GetProperty("authenticationSchemes").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task returns_meta_with_location()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync(Route);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var meta = body.RootElement.GetProperty("meta");
        meta.GetProperty("resourceType").GetString().ShouldBe("ServiceProviderConfig");
        ShouldlyExtensions.ShouldContain(meta.GetProperty("location").GetString()!, "/scim/ServiceProviderConfig");
    }

    [Fact]
    public async Task filter_max_results_defaults_to_200()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.GetAsync(Route);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("filter").GetProperty("maxResults").GetInt32().ShouldBe(200);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
