// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.OutsideIn.Tests;

/// <summary>
/// Regression coverage for design doc D34 (distributed caching, D12): with <see cref="CachingMode.Distributed" />,
/// both of the system under test's nodes (D36) must read and write the same backing cache, not each keep its own.
/// HybridCache 10.0's <c>DefaultHybridCache</c> silently drops its distributed tier when the backend's concrete type
/// is <c>MemoryDistributedCache</c> and the local tier is a plain <c>MemoryCache</c> (see
/// <c>Internal.SharedDistributedCache</c> in the harness), which would make this cache per-node again without anyone
/// noticing: every permutation test would still pass, since each node answers its own requests out of its own cache.
/// Both tiers must also expire entries on the test clock (D15), not the wall clock.
/// </summary>
public sealed class CachingTests
{
    [Fact]
    public async Task Distributed_caching_is_shared_between_nodes()
    {
        // Only runs on Duende.Storage for now, not on every store type. The harness's in-memory store bypasses
        // AddInMemoryClients, so it has no ICorsPolicyService that reads clients' AllowedCorsOrigins, and this test's
        // CORS check can't pass there. Entity Framework could be added. Cover every store type once the product's
        // in-memory stores are writable at runtime and the harness uses them (#4059).
        var permutation = new Permutation(StoreType.DuendeStorageSchemasInMemory, CachingMode.Distributed, UserStore.TestUsers, SpacesMode.Off);
        await using var env = await TestEnvironment.StartAsync(permutation, env => env.SetupPerIssuer(issuer => issuer));

        const string origin = "https://cors-allowed.test";
        var clientId = $"cors-client-{Guid.NewGuid():N}";

        await env.SetupData(async (issuer, _) =>
        {
            await issuer.Storage.AddApiScopeAsync(new ApiScope("api1"));
            await issuer.Storage.AddClientAsync(new Client
            {
                ClientId = clientId,
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { "https://cors-client.test/callback" },
                AllowedScopes = { "api1" },
                AllowedCorsOrigins = { origin },
            });
        });

        using var http = new HttpClient();

        // Node 1: a request from the allowed origin against the CORS-enabled discovery endpoint populates its L1
        // cache, and the shared L2 cache if distributed caching (the thing this test checks) is really shared.
        (await IsOriginAllowedAsync(http, env.Nodes[0], origin)).ShouldBeTrue();

        // Remove the client directly, bypassing the cache (a write here doesn't invalidate a cached read, by design).
        // With a genuinely shared L2, node 2 still answers "allowed" from the stale entry node 1 populated, instead of
        // asking the client store directly, which would now say the client (and so the CORS origin) doesn't exist.
        await env.SetupData(async (issuer, _) => await issuer.Storage.RemoveClientAsync(clientId));

        (await IsOriginAllowedAsync(http, env.Nodes[1], origin)).ShouldBeTrue();

        // Cached entries expire on the test clock (D15), in both tiers: once it's past any cache lifetime, both nodes
        // ask the store again and see that the client is gone.
        env.Clock.Advance(TimeSpan.FromDays(1));

        (await IsOriginAllowedAsync(http, env.Nodes[0], origin)).ShouldBeFalse();
        (await IsOriginAllowedAsync(http, env.Nodes[1], origin)).ShouldBeFalse();
    }

    private static async Task<bool> IsOriginAllowedAsync(HttpClient http, IdentityServerNode node, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(node.DirectUrl, "/.well-known/openid-configuration"));
        request.Headers.Add("Origin", origin);
        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);
        return response.Headers.Contains("Access-Control-Allow-Origin");
    }
}
