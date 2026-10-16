// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Http.Json;
using Duende.Spaces.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class SharedSpacesCacheTests
{
    private const string OldOrigin = "http://old.example.com";
    private const string NewOrigin = "http://new.example.com";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task space_update_on_one_node_is_seen_by_another_node_sharing_the_spaces_cache(bool cacheRegisteredBeforeSpaces)
    {
        // Sharing one HybridCache instance between the two nodes stands in for a cache shared across
        // nodes. A distributed L2 cache alone would not invalidate the other node's in-process L1,
        // so the nodes share the whole instance to model one cache with one invalidation path.
        await using var cacheServices = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
        var sharedCache = cacheServices.GetRequiredService<HybridCache>();
        var databaseName = $"SharedSpacesCache_{Guid.NewGuid():N}";

        await using var node1 = await StartNodeAsync(databaseName, sharedCache, cacheRegisteredBeforeSpaces);
        await using var node2 = await StartNodeAsync(databaseName, sharedCache, cacheRegisteredBeforeSpaces);

        var admin = node1.App.Services.GetRequiredService<ISpaceAdmin>();
        var space = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Shared Cache Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = OldOrigin }]
            },
            _ct);
        var spaceId = space.Id!.ToString();

        (await node2.GetSpaceInfoAsync(OldOrigin, _ct)).SpaceId.ShouldBe(spaceId);

        var current = await admin.GetAsync(space.Id!, _ct);
        var config = current.Item!;
        config.MatchPatterns = [new SpaceMatchPattern { Origin = NewOrigin }];
        (await admin.UpdateAsync(space.Id!, config, current.Version!, _ct)).IsSuccess.ShouldBeTrue();

        (await node2.GetSpaceInfoAsync(NewOrigin, _ct)).SpaceId.ShouldBe(spaceId);
        (await node2.GetSpaceInfoAsync(OldOrigin, _ct)).SpaceId.ShouldNotBe(spaceId);
    }

    private async Task<Node> StartNodeAsync(string databaseName, HybridCache sharedCache, bool cacheRegisteredBeforeSpaces)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        if (cacheRegisteredBeforeSpaces)
        {
            _ = builder.Services.AddKeyedSingleton(SpacesServiceProviderKeys.SpacesCache, sharedCache);
        }

        builder.Services.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(builder.Services);
        builder.Services.Configure<SpacesOptions>(opt => opt.FallbackToDefault = true);
        builder.Services.AddStorageInternal(b => b.AddSqliteInMemory(databaseName));

        if (!cacheRegisteredBeforeSpaces)
        {
            _ = builder.Services.AddKeyedSingleton(SpacesServiceProviderKeys.SpacesCache, sharedCache);
        }

        var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(_ct);

        app.UseSpaceResolution();
        app.UseRouting();
        app.MapFallback((HttpContext httpContext, ISpaceContextAccessor spaceContext) =>
            Results.Ok(new SpaceInfoResponse
            {
                SpaceId = spaceContext.GetSpaceId().Value.ToString(),
                Path = httpContext.Request.Path.ToString()
            }));

        await app.StartAsync(_ct);
        return new Node(app, app.GetTestClient());
    }

    private sealed class Node(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;

        public async Task<SpaceInfoResponse> GetSpaceInfoAsync(string origin, Ct ct)
        {
            var response = await client.GetAsync($"{origin}/api/info", ct);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<SpaceInfoResponse>(ct))!;
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await App.DisposeAsync();
        }
    }
}
