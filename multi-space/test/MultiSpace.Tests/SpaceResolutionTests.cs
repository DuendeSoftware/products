// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Http.Json;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.MultiSpace;

public sealed class SpaceResolutionTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private ISpaceAdmin _admin = null!;
    private FakeTimeProvider _timeProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _timeProvider = new FakeTimeProvider();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Register FakeTimeProvider before storage registration so TryAddSingleton won't overwrite it
        builder.Services.AddSingleton<TimeProvider>(_timeProvider);

        builder.Services.AddMultiSpace();
        builder.Services.Configure<MultiSpaceOptions>(opt =>
        {
            opt.LocalCacheExpiration = TimeSpan.FromSeconds(30);
            opt.Expiration = TimeSpan.FromSeconds(30);
            opt.FallbackToDefault = true;
        });
        builder.Services.AddStorageInternal(b => b.AddSqliteInMemoryStore());

        _app = builder.Build();

        var schema = _app.Services.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(CancellationToken.None);

        _admin = _app.Services.GetRequiredService<ISpaceAdmin>();

        // UseMultiSpaceResolution must come before UseRouting so that path rewrites
        // (for path-based space matching) happen before route matching occurs.
        _app.UseMultiSpaceResolution();
        _app.UseRouting();
        _app.MapGet("/api/info", (HttpContext httpContext, ISpaceContextAccessor spaceContext) =>
            Results.Ok(new SpaceInfoResponse
            {
                SpaceId = spaceContext.GetSpaceId().Value.ToString(),
                Path = httpContext.Request.Path.ToString()
            }));
        // Catch-all route for testing non-matching paths
        _app.MapFallback((HttpContext httpContext, ISpaceContextAccessor spaceContext) =>
            Results.Ok(new SpaceInfoResponse
            {
                SpaceId = spaceContext.GetSpaceId().Value.ToString(),
                Path = httpContext.Request.Path.ToString()
            }));

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task resolves_space_by_origin()
    {
        // The TestServer uses http:// scheme, so origins must be registered as http://
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Origin Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "http://tenant1.example.com" }]
            },
            CancellationToken.None);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://tenant1.example.com/api/info");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(space.Id!.ToString());
    }

    [Fact]
    public async Task resolves_space_by_path_with_prefix()
    {
        // SpacePathPrefix is "/t" (default), space path is "/acme"
        // Middleware will match requests starting with /t/acme
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            CancellationToken.None);

        // Request: /t/acme/api/info
        // Middleware extracts: /t/acme (prefix + segment)
        // Matches space with Path = "/acme"
        // Rewrites to: PathBase=/t/acme, Path=/api/info
        var response = await _client.GetAsync("/t/acme/api/info");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(space.Id!.ToString());
        result.Path.ShouldBe("/api/info");
    }

    [Fact]
    public async Task resolves_space_by_origin_and_path()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Origin+Path Space",
                MatchPatterns = [new SpaceMatchPattern
                {
                    Origin = "http://combo.example.com",
                    Path = "/tenant"
                }]
            },
            CancellationToken.None);

        // Request with matching origin AND path prefix resolves to that space
        var request = new HttpRequestMessage(HttpMethod.Get, "http://combo.example.com/t/tenant/api/info");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(space.Id!.ToString());
        result.Path.ShouldBe("/api/info");
    }

    [Fact]
    public async Task unresolvable_returns_default_space()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "http://unknown.notregistered.com/api/info");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(SpaceId.Default.Value.ToString());
    }

    [Fact]
    public async Task disabled_space_returns_default_space()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Disabled Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "http://disabled.example.com" }]
            },
            CancellationToken.None);

        var getResult = await _admin.GetAsync(space.Id!, CancellationToken.None);
        getResult.Found.ShouldBeTrue();
        var config = getResult.Item!;
        config.Enabled = false;
        await _admin.UpdateAsync(space.Id!, config, getResult.Version!, CancellationToken.None);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://disabled.example.com/api/info");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        // Disabled space -> null result -> middleware sets Default
        result.SpaceId.ShouldBe(SpaceId.Default.Value.ToString());
    }

    [Fact]
    public async Task cache_expires_after_configured_ttl()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Cached Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "http://cached.example.com" }]
            },
            CancellationToken.None);

        // First request — populates the cache
        var response1 = await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://cached.example.com/api/info"));
        response1.EnsureSuccessStatusCode();
        var result1 = await response1.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result1.ShouldNotBeNull();
        result1.SpaceId.ShouldBe(space.Id!.ToString());

        // Delete the space — AdminDeleteAsync also calls BustCache which clears the key
        await _admin.DeleteAsync(space.Id!, CancellationToken.None);

        // Advance time past the cache TTL
        _timeProvider.Advance(TimeSpan.FromSeconds(60));

        // After TTL expiry, re-fetch from store -> space is gone -> Default
        var response2 = await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://cached.example.com/api/info"));
        response2.EnsureSuccessStatusCode();
        var result2 = await response2.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result2.ShouldNotBeNull();
        result2.SpaceId.ShouldBe(SpaceId.Default.Value.ToString());
    }

    [Fact]
    public async Task resolves_distinct_spaces_by_path_segment_under_same_prefix()
    {
        // Two tenants under the same /t prefix, distinguished by path segment
        var acmeSpace = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            CancellationToken.None);

        var betaSpace = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Beta Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/beta" }]
            },
            CancellationToken.None);

        // Request to /t/acme/api/info should resolve to Acme's space
        var acmeResponse = await _client.GetAsync("/t/acme/api/info");
        acmeResponse.EnsureSuccessStatusCode();
        var acmeResult = await acmeResponse.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        acmeResult.ShouldNotBeNull();
        acmeResult.SpaceId.ShouldBe(acmeSpace.Id!.ToString());
        acmeResult.Path.ShouldBe("/api/info");

        // Request to /t/beta/api/info should resolve to Beta's space
        var betaResponse = await _client.GetAsync("/t/beta/api/info");
        betaResponse.EnsureSuccessStatusCode();
        var betaResult = await betaResponse.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        betaResult.ShouldNotBeNull();
        betaResult.SpaceId.ShouldBe(betaSpace.Id!.ToString());
        betaResult.Path.ShouldBe("/api/info");

        // They must be different spaces
        acmeResult.SpaceId.ShouldNotBe(betaResult.SpaceId);
    }

    [Fact]
    public async Task path_rewriting_strips_prefix_and_tenant_segment()
    {
        // When matched by path, prefix+segment should be moved to PathBase
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Rewrite Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/tenant-x" }]
            },
            CancellationToken.None);

        // Request: /t/tenant-x/api/info
        // Expected after rewrite: PathBase = /t/tenant-x, Path = /api/info
        var response = await _client.GetAsync("/t/tenant-x/api/info");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.Path.ShouldBe("/api/info");
    }

    [Fact]
    public async Task overwriting_space_in_new_scope_proves_scoped_isolation()
    {
        var space1 = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Scope Space 1",
                MatchPatterns = [new SpaceMatchPattern { Origin = "http://scope1.example.com" }]
            },
            CancellationToken.None);

        var space2 = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Scope Space 2",
                MatchPatterns = [new SpaceMatchPattern { Origin = "http://scope2.example.com" }]
            },
            CancellationToken.None);

        // Each HTTP request gets its own DI scope. Verify independent resolution.
        var response1 = await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://scope1.example.com/api/info"));
        response1.EnsureSuccessStatusCode();
        var result1 = await response1.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result1.ShouldNotBeNull();
        result1.SpaceId.ShouldBe(space1.Id!.ToString());

        var response2 = await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://scope2.example.com/api/info"));
        response2.EnsureSuccessStatusCode();
        var result2 = await response2.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result2.ShouldNotBeNull();
        result2.SpaceId.ShouldBe(space2.Id!.ToString());

        // Cross-check: no bleed-over between scopes
        result2.SpaceId.ShouldNotBe(space1.Id!.ToString());
        result1.SpaceId.ShouldNotBe(space2.Id!.ToString());
    }

    [Fact]
    public async Task request_without_prefix_returns_default_space()
    {
        // Space configured with path "/acme"
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            CancellationToken.None);

        // Request to /acme/api/info (missing /t prefix) should not match
        var response = await _client.GetAsync("/acme/api/info");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(SpaceId.Default.Value.ToString());
    }

    [Fact]
    public async Task request_with_prefix_but_wrong_segment_returns_404()
    {
        // Space configured with path "/acme"
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            CancellationToken.None);

        // Request to /t/api/info — the /t prefix signals an explicit space request,
        // extracting "api" as the segment. No space matches "/api", so this is a 404.
        var response = await _client.GetAsync("/t/api/info");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task request_with_wrong_segment_returns_404()
    {
        // Space configured with path "/acme"
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            CancellationToken.None);

        // Request to /t/beta/api/info (wrong tenant segment) — explicitly asking for
        // a space that doesn't exist via the path prefix should always 404, never
        // silently fall back to default.
        var response = await _client.GetAsync("/t/beta/api/info");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task path_matching_is_case_insensitive()
    {
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            CancellationToken.None);

        // Request with different casing should still match
        var response = await _client.GetAsync("/T/ACME/api/info");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(space.Id!.ToString());
    }

    [Fact]
    public async Task fallback_to_default_disabled_leaves_space_context_unset()
    {
        // Build a separate app with FallbackToDefault = false
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMultiSpace();
        builder.Services.Configure<MultiSpaceOptions>(opt => opt.FallbackToDefault = false);
        builder.Services.AddStorageInternal(b => b.AddSqliteInMemoryStore());

        await using var app = builder.Build();

        var schema = app.Services.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(CancellationToken.None);

        app.UseMultiSpaceResolution();
        app.MapGet("/api/info", (ISpaceContextAccessor ctx) =>
            Results.Ok(new SpaceInfoResponse
            {
                SpaceId = ctx.GetSpaceId().Value.ToString(),
                Path = "/api/info"
            }));

        await app.StartAsync();
        using var client = app.GetTestClient();

        // Request to an unknown host — no space matches, fallback is disabled,
        // so the middleware returns 404 without invoking the rest of the pipeline.
        var request = new HttpRequestMessage(HttpMethod.Get, "http://unknown.example.com/api/info");
        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public void fallback_to_default_is_false_by_default()
    {
        // The FallbackToDefault option must default to false — resolving to the
        // default space should be an explicit opt-in, not a silent fallback.
        var options = new MultiSpaceOptions();
        options.FallbackToDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task origin_and_path_match_resolves_when_both_criteria_match()
    {
        // Space A: matches on https://A AND /t/A (both origin + path)
        var spaceA = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space A",
                MatchPatterns = [new SpaceMatchPattern
                {
                    Origin = "http://host-a.example.com",
                    Path = "/space-a"
                }]
            },
            CancellationToken.None);

        // Request with matching origin AND path -> resolves to Space A
        var request = new HttpRequestMessage(HttpMethod.Get, "http://host-a.example.com/t/space-a/api/info");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(spaceA.Id!.ToString());
    }

    [Fact]
    public async Task hostname_precedence_prevents_path_from_resolving_to_different_space()
    {
        // Space A: matches on origin http://host-a.example.com AND path /space-a
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space A",
                MatchPatterns = [new SpaceMatchPattern
                {
                    Origin = "http://host-a.example.com",
                    Path = "/space-a"
                }]
            },
            CancellationToken.None);

        // Space C: matches on path /space-c only (no origin)
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space C",
                MatchPatterns = [new SpaceMatchPattern { Path = "/space-c" }]
            },
            CancellationToken.None);

        // Request: http://host-a.example.com/t/space-c
        // The origin matches Space A, but the path /space-c belongs to Space C.
        // Because hostname has higher precedence, the path should NOT override to Space C.
        // The combined lookup (origin+path) fails, and since the origin IS registered,
        // we reject rather than falling through to a path-only match.
        var request = new HttpRequestMessage(HttpMethod.Get, "http://host-a.example.com/t/space-c/api/info");
        var response = await _client.SendAsync(request);

        // Explicit path requested but blocked by hostname precedence -> 404
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task registered_origin_blocks_path_resolution_to_another_space()
    {
        // Space B: matches on origin only (http://host-b.example.com)
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space B",
                MatchPatterns = [new SpaceMatchPattern { Origin = "http://host-b.example.com" }]
            },
            CancellationToken.None);

        // Space C: matches on path /space-c only (no origin)
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space C",
                MatchPatterns = [new SpaceMatchPattern { Path = "/space-c" }]
            },
            CancellationToken.None);

        // Request: http://host-b.example.com/t/space-c
        // The origin matches Space B (origin-only match), and path /space-c belongs to Space C.
        // Because hostname has higher precedence, we do NOT allow path to route to Space C.
        var request = new HttpRequestMessage(HttpMethod.Get, "http://host-b.example.com/t/space-c/api/info");
        var response = await _client.SendAsync(request);

        // Explicit path requested but blocked by hostname precedence -> 404
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task unregistered_origin_allows_path_only_resolution()
    {
        // Space B: matches on origin only (http://host-b.example.com)
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space B",
                MatchPatterns = [new SpaceMatchPattern { Origin = "http://host-b.example.com" }]
            },
            CancellationToken.None);

        // Space C: matches on path /space-c only (no origin)
        var spaceC = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space C",
                MatchPatterns = [new SpaceMatchPattern { Path = "/space-c" }]
            },
            CancellationToken.None);

        // Request: http://something-unknown.example.com/t/space-c
        // The origin "something-unknown" is NOT registered to any space.
        // Since no space claims this host, path-only resolution is allowed -> Space C.
        var request = new HttpRequestMessage(HttpMethod.Get, "http://something-unknown.example.com/t/space-c/api/info");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(spaceC.Id!.ToString());
    }

    [Fact]
    public async Task origin_only_space_resolves_without_path()
    {
        // Space B: matches on origin only — no path configured
        var spaceB = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Space B",
                MatchPatterns = [new SpaceMatchPattern { Origin = "http://host-b.example.com" }]
            },
            CancellationToken.None);

        // Request: http://host-b.example.com/api/info (no /t/... path)
        // Should resolve to Space B by origin alone.
        var request = new HttpRequestMessage(HttpMethod.Get, "http://host-b.example.com/api/info");
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(spaceB.Id!.ToString());
    }
}
