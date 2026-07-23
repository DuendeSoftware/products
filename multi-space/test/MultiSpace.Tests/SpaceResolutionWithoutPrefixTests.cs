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

/// <summary>
/// Tests for space resolution when SpacePathPrefix is not configured (null).
/// </summary>
public sealed class SpaceResolutionWithoutPrefixTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private ISpaceAdmin _admin = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddMultiSpace();
        builder.Services.Configure<MultiSpaceOptions>(opt =>
        {
            // Disable path prefix — spaces match directly on first segment
            opt.SpacePathPrefix = null;
            opt.FallbackToDefault = true;
        });
        builder.Services.AddStorageInternal(b => b.AddSqliteInMemoryStore());

        _app = builder.Build();

        var schema = _app.Services.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(CancellationToken.None);

        _admin = _app.Services.GetRequiredService<ISpaceAdmin>();

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
    public async Task resolves_space_by_first_segment_without_prefix()
    {
        // No prefix configured, space path is "/acme"
        var space = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            CancellationToken.None);

        // Request: /acme/api/info
        // Middleware extracts: /acme (first segment)
        // Matches space with Path = "/acme"
        // Rewrites to: PathBase=/acme, Path=/api/info
        var response = await _client.GetAsync("/acme/api/info");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(space.Id!.ToString());
        result.Path.ShouldBe("/api/info");
    }

    [Fact]
    public async Task resolves_distinct_spaces_by_first_segment()
    {
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

        // Request to /acme/api/info
        var acmeResponse = await _client.GetAsync("/acme/api/info");
        acmeResponse.EnsureSuccessStatusCode();
        var acmeResult = await acmeResponse.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        acmeResult.ShouldNotBeNull();
        acmeResult.SpaceId.ShouldBe(acmeSpace.Id!.ToString());
        acmeResult.Path.ShouldBe("/api/info");

        // Request to /beta/api/info
        var betaResponse = await _client.GetAsync("/beta/api/info");
        betaResponse.EnsureSuccessStatusCode();
        var betaResult = await betaResponse.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        betaResult.ShouldNotBeNull();
        betaResult.SpaceId.ShouldBe(betaSpace.Id!.ToString());
        betaResult.Path.ShouldBe("/api/info");

        // They must be different spaces
        acmeResult.SpaceId.ShouldNotBe(betaResult.SpaceId);
    }

    [Fact]
    public async Task request_with_wrong_segment_returns_404()
    {
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            CancellationToken.None);

        // Request to /beta/api/info (wrong segment) — explicitly asking for a space
        // that doesn't exist should always 404.
        var response = await _client.GetAsync("/beta/api/info");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task request_without_segment_returns_404()
    {
        await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme Space",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            CancellationToken.None);

        // Request to /api/info — when no prefix is configured, "api" is extracted
        // as the space segment. No space matches "/api", so this is a 404.
        var response = await _client.GetAsync("/api/info");
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
        var response = await _client.GetAsync("/ACME/api/info");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpaceInfoResponse>();
        result.ShouldNotBeNull();
        result.SpaceId.ShouldBe(space.Id!.ToString());
    }
}
