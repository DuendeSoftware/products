// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Duende.Spaces.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class RouteRequestsHowToTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private ISpaceAdmin _admin = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _app = await CreateAppAsync(spaces =>
        {
            // begin-snippet: route-requests-fallback-to-default
            // With no path segment in the request, an unresolved space falls back to
            // SpaceId.Default instead of returning 404.
            spaces.FallbackToDefault = true;
            // end-snippet
        });
        _admin = _app.Services.GetRequiredService<ISpaceAdmin>();
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private static async Task<WebApplication> CreateAppAsync(
        Action<SpacesOptions> configure,
        Action<WebApplication>? mapExtraEndpoints = null,
        Action<WebApplication>? beforeSpaceResolution = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(builder.Services);
        builder.Services.Configure(configure);
        builder.Services.AddStorageInternal(b => b.AddSqliteInMemory());

        var app = builder.Build();

        var storageInstanceSchema = app.Services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(CancellationToken.None);

        beforeSpaceResolution?.Invoke(app);

        // UseSpaceResolution must come before UseRouting so that path rewrites
        // (for path-based space matching) happen before route matching occurs.
        app.UseSpaceResolution();
        app.UseRouting();
        app.MapGet("/api/info", (HttpContext httpContext, ISpaceContextAccessor spaceContext) =>
            Results.Ok(new SpaceInfoResponse
            {
                SpaceId = spaceContext.GetSpaceId().Value.ToString(),
                Path = httpContext.Request.Path.ToString(),
                PathBase = httpContext.Request.PathBase.ToString()
            }));
        app.MapFallback((HttpContext httpContext, ISpaceContextAccessor spaceContext) =>
            Results.Ok(new SpaceInfoResponse
            {
                SpaceId = spaceContext.GetSpaceId().Value.ToString(),
                Path = httpContext.Request.Path.ToString(),
                PathBase = httpContext.Request.PathBase.ToString()
            }));
        mapExtraEndpoints?.Invoke(app);

        await app.StartAsync();
        return app;
    }

    private sealed class SpaceInfoResponse
    {
        public string SpaceId { get; set; } = "";
        public string Path { get; set; } = "";
        public string PathBase { get; set; } = "";
    }

    private static CreateSpaceConfiguration OriginOnlySpace(string name, string origin)
    {
        // begin-snippet: route-requests-origin-pattern
        // The TestServer uses http:// for its origins. Use https:// for the same pattern
        // against a real host.
        var config = new CreateSpaceConfiguration
        {
            Name = name,
            MatchPatterns = [new SpaceMatchPattern { Origin = origin }]
        };
        // end-snippet
        return config;
    }

    [Fact]
    public async Task resolves_space_by_origin_only()
    {
        var created = await _admin.CreateAsync(
            OriginOnlySpace("Acme", "http://acme.example.com"), _ct);

        var response = await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://acme.example.com/api/info"));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(created.Id!.ToString());
    }

    private static CreateSpaceConfiguration PathOnlySpace(string name, string path)
    {
        // begin-snippet: route-requests-path-pattern
        var config = new CreateSpaceConfiguration
        {
            Name = name,
            MatchPatterns = [new SpaceMatchPattern { Path = path }]
        };
        // end-snippet
        return config;
    }

    [Fact]
    public async Task resolves_space_by_path_with_default_prefix()
    {
        var created = await _admin.CreateAsync(PathOnlySpace("Acme", "/acme"), _ct);

        var response = await _client.GetAsync("/t/acme/x", _ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(created.Id!.ToString());
    }

    [Fact]
    public async Task path_match_rewrites_path_base_and_path()
    {
        await _admin.CreateAsync(PathOnlySpace("Acme", "/acme"), _ct);

        var response = await _client.GetAsync("/t/acme/x", _ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.PathBase.ShouldBe("/t/acme");
        result.Path.ShouldBe("/x");
    }

    [Fact]
    public async Task path_matching_is_case_insensitive()
    {
        var created = await _admin.CreateAsync(PathOnlySpace("Acme", "/acme"), _ct);

        var response = await _client.GetAsync("/T/ACME/x", _ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(created.Id!.ToString());
    }

    [Fact]
    public async Task resolves_space_by_origin_and_path_combined()
    {
        // begin-snippet: route-requests-origin-and-path-pattern
        var config = new CreateSpaceConfiguration
        {
            Name = "Combo",
            MatchPatterns = [new SpaceMatchPattern { Origin = "http://combo.example.com", Path = "/tenant" }]
        };
        // end-snippet

        var created = await _admin.CreateAsync(config, _ct);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://combo.example.com/t/tenant/x");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(created.Id!.ToString());
    }

    [Fact]
    public async Task claimed_origin_blocks_path_only_resolution_to_another_space()
    {
        // Space A claims an origin only.
        await _admin.CreateAsync(OriginOnlySpace("Space A", "http://host-a.example.com"), _ct);
        // Space B is path-only, no origin claim.
        await _admin.CreateAsync(PathOnlySpace("Space B", "/space-b"), _ct);

        // host-a.example.com is claimed by Space A, so the path segment "/space-b" cannot
        // route this request to Space B. Hostname precedence rejects it instead of falling
        // through to a path-only match.
        var request = new HttpRequestMessage(HttpMethod.Get, "http://host-a.example.com/t/space-b/x");
        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task unclaimed_origin_allows_path_only_resolution()
    {
        await _admin.CreateAsync(OriginOnlySpace("Space A", "http://host-a.example.com"), _ct);
        var spaceB = await _admin.CreateAsync(PathOnlySpace("Space B", "/space-b"), _ct);

        // unclaimed.example.com isn't claimed by any space, so the path segment can resolve.
        var request = new HttpRequestMessage(HttpMethod.Get, "http://unclaimed.example.com/t/space-b/x");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(spaceB.Id!.ToString());
    }

    [Fact]
    public async Task unknown_segment_returns_404_even_with_fallback_enabled()
    {
        await _admin.CreateAsync(PathOnlySpace("Acme", "/acme"), _ct);

        // FallbackToDefault is true for this app, but an explicit path segment that matches
        // no space is always rejected: the caller asked for a specific space, not the default.
        var response = await _client.GetAsync("/t/unknown/x", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task no_segment_with_fallback_resolves_default_space()
    {
        var response = await _client.GetAsync("/api/info", _ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(SpaceId.Default.Value.ToString());
    }

    [Fact]
    public void default_path_prefix_is_t()
    {
        var options = new SpacesOptions();
        options.SpacePathPrefix?.Value.ShouldBe("/t");
    }

    [Fact]
    public async Task custom_prefix_is_applied_via_options()
    {
        await using var app = await CreateAppAsync(spaces =>
        {
            spaces.FallbackToDefault = true;
            // begin-snippet: route-requests-path-prefix-option
            spaces.SpacePathPrefix = "/tenants";
            // end-snippet
        });
        var admin = app.Services.GetRequiredService<ISpaceAdmin>();
        using var client = app.GetTestClient();

        var created = await admin.CreateAsync(PathOnlySpace("Acme", "/acme"), _ct);

        var response = await client.GetAsync("/tenants/acme/x", _ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(created.Id!.ToString());
    }

    [Fact]
    public async Task no_path_prefix_matches_bare_first_segment()
    {
        await using var app = await CreateAppAsync(spaces =>
        {
            spaces.FallbackToDefault = true;
            // begin-snippet: route-requests-no-path-prefix
            // With no prefix, the middleware matches the bare first path segment instead of
            // requiring a reserved segment such as /t first.
            spaces.SpacePathPrefix = null;
            // end-snippet
        });
        var admin = app.Services.GetRequiredService<ISpaceAdmin>();
        using var client = app.GetTestClient();

        var created = await admin.CreateAsync(PathOnlySpace("Acme", "/acme"), _ct);

        var response = await client.GetAsync("/acme/x", _ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(created.Id!.ToString());
        result.PathBase.ShouldBe("/acme");
        result.Path.ShouldBe("/x");
    }

    [Fact]
    public void fallback_to_default_is_false_by_default()
    {
        var options = new SpacesOptions();
        options.FallbackToDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task no_fallback_and_no_match_returns_404()
    {
        await using var app = await CreateAppAsync(opt => opt.FallbackToDefault = false);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/info", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task read_path_base_endpoint_reports_space_and_paths()
    {
        await using var app = await CreateAppAsync(
            opt => opt.FallbackToDefault = true,
            mapExtraEndpoints: app =>
            {
                // begin-snippet: route-requests-read-path-base
                app.MapGet("/custom/info", (HttpContext httpContext, ISpaceContextAccessor spaceContext) =>
                    // PathBase holds the matched prefix and segment (e.g. "/t/acme"); Path holds the
                    // rest of the request, with the space segment already stripped.
                    Results.Ok(new
                    {
                        SpaceId = spaceContext.GetSpaceId().Value.ToString(),
                        PathBase = httpContext.Request.PathBase.ToString(),
                        Path = httpContext.Request.Path.ToString()
                    }));
                // end-snippet
            });
        var admin = app.Services.GetRequiredService<ISpaceAdmin>();
        using var client = app.GetTestClient();

        var created = await admin.CreateAsync(PathOnlySpace("Acme", "/acme"), _ct);

        var response = await client.GetAsync("/t/acme/custom/info", _ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("spaceId").GetString().ShouldBe(created.Id!.ToString());
        body.GetProperty("pathBase").GetString().ShouldBe("/t/acme");
        body.GetProperty("path").GetString().ShouldBe("/custom/info");
    }

    private static void UsePinnedAdminSpace(WebApplication app, SpaceId adminSpaceId)
    {
        // begin-snippet: route-requests-pin-route-to-space
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/admin"))
            {
                var accessor = context.RequestServices.GetRequiredService<ISpaceContextAccessor>();
                using (accessor.SetSpace(adminSpaceId))
                {
                    await next(context);
                }

                return;
            }

            await next(context);
        });
        // end-snippet
    }

    [Fact]
    public async Task pinned_route_runs_in_fixed_space_regardless_of_origin_or_path()
    {
        var adminSpaceId = SpaceId.New();

        await using var app = await CreateAppAsync(
            opt => opt.FallbackToDefault = true,
            beforeSpaceResolution: a => UsePinnedAdminSpace(a, adminSpaceId));
        var admin = app.Services.GetRequiredService<ISpaceAdmin>();
        using var client = app.GetTestClient();

        // Space B claims a different origin and path; the pinned route must not resolve to it.
        var spaceB = await admin.CreateAsync(OriginOnlySpace("Space B", "http://host-b.example.com"), _ct);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://host-b.example.com/admin/x");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(adminSpaceId.ToString());
        result.SpaceId.ShouldNotBe(spaceB.Id!.ToString());

        // Without the pinning middleware, this same origin would resolve to Space B.
        var unpinnedRequest = new HttpRequestMessage(HttpMethod.Get, "http://host-b.example.com/api/info");
        var unpinnedResponse = await client.SendAsync(unpinnedRequest);
        unpinnedResponse.EnsureSuccessStatusCode();
        var unpinnedResult = await unpinnedResponse.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        unpinnedResult.ShouldNotBeNull();
        unpinnedResult.SpaceId.ShouldBe(spaceB.Id!.ToString());
    }

    [Fact]
    public async Task pinned_route_does_not_rewrite_path_base()
    {
        var adminSpaceId = SpaceId.New();

        await using var app = await CreateAppAsync(
            opt => opt.FallbackToDefault = true,
            beforeSpaceResolution: a => UsePinnedAdminSpace(a, adminSpaceId));
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/admin/x", _ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.PathBase.ShouldBe("");
        result.Path.ShouldBe("/admin/x");
    }

    [Fact]
    public async Task pinned_route_is_not_rejected_as_unknown_segment()
    {
        var adminSpaceId = SpaceId.New();

        await using var app = await CreateAppAsync(
            // FallbackToDefault = false, and nothing claims "/admin" as a space path. Without
            // the pinning middleware, this would 404 like any other unrecognized segment.
            opt => opt.FallbackToDefault = false,
            beforeSpaceResolution: a => UsePinnedAdminSpace(a, adminSpaceId));
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/admin/x", _ct);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task other_routes_still_resolve_normally_alongside_a_pinned_route()
    {
        var adminSpaceId = SpaceId.New();

        await using var app = await CreateAppAsync(
            opt => opt.FallbackToDefault = true,
            beforeSpaceResolution: a => UsePinnedAdminSpace(a, adminSpaceId));
        var admin = app.Services.GetRequiredService<ISpaceAdmin>();
        using var client = app.GetTestClient();

        var tenantSpace = await admin.CreateAsync(PathOnlySpace("Acme", "/acme"), _ct);

        var response = await client.GetAsync("/t/acme/x", _ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(_ct);
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(tenantSpace.Id!.ToString());
    }

    [Fact]
    public async Task completion_callback_of_a_pinned_request_runs_in_the_pinned_space()
    {
        var adminSpaceId = SpaceId.New();
        var spaceInCallback = new TaskCompletionSource<SpaceId?>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var app = await CreateAppAsync(
            opt => opt.FallbackToDefault = true,
            mapExtraEndpoints: a => a.MapGet("/admin/x", (HttpContext httpContext, ISpaceContextAccessor accessor) =>
            {
                // OnCompleted runs once the response has been written, after the pinning
                // middleware's `using` scope around `next(context)` has already been disposed.
                // The callback still runs in the space that was active when it was registered.
                httpContext.Response.OnCompleted(() =>
                {
                    _ = spaceInCallback.TrySetResult(accessor.IsSpaceIdConfigured() ? accessor.GetSpaceId() : null);
                    return Task.CompletedTask;
                });
                return Results.Ok();
            }),
            beforeSpaceResolution: a => UsePinnedAdminSpace(a, adminSpaceId));
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/admin/x", _ct);

        response.EnsureSuccessStatusCode();
        var spaceSeenByCallback = await spaceInCallback.Task.WaitAsync(TimeSpan.FromSeconds(5), _ct);
        spaceSeenByCallback.ShouldBe(adminSpaceId);
    }
}
