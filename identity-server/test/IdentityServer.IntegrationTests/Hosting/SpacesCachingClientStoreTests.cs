// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage.Clients;
using Duende.Spaces;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

/// <summary>
/// Regression test: <see cref="CachingClientStore{T}"/> must observe the caller's space, not fall
/// back to Default, when HybridCache queues its factory to the thread pool (cancellable token).
/// </summary>
public sealed class SpacesCachingClientStoreTests(WebServerFixture webServerFixture) : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<KestrelBasedTestServer> CreateServerAsync(string name)
    {
        var dbName = $"msclientcache_{name}_{Guid.NewGuid():N}";
        var output = TestContext.Current.TestOutputHelper!;
        var hostAlias = $"ms{Guid.NewGuid():N}"[..10];

        var server = new KestrelBasedTestServer(
            hostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name),
            services =>
            {
                services.AddRouting();
                services.AddSpaces();
                SpacesTestLicense.RegisterEntitled(services);

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddStorage(storage => storage.AddSqliteInMemory(dbName))
                    .AddConfigurationStorage()
                    .AddOperationalStorage()
                    .AddInMemoryCaching()
                    .AddClientStoreCache<ClientStore>()
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp =>
            {
                webapp.UseSpaceResolution();
                webapp.UseIdentityServer();

                webapp.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.Value == "/test/client")
                    {
                        var clientStore = ctx.RequestServices.GetRequiredService<IClientStore>();
                        var client = await clientStore.FindClientByIdAsync("shared-client-id", ctx.RequestAborted);
                        await ctx.Response.WriteAsync(client?.ClientName ?? "null");
                        return;
                    }

                    await next(ctx);
                });
            });

        await server.StartAsync();

        var schema = server.GetRequiredService<Duende.Storage.Schema.IStorageInstanceSchema>();
        await schema.MigrateAsync(_ct);

        return server;
    }

    private async Task CreateClientInSpaceAsync(KestrelBasedTestServer server, SpaceId spaceId, string clientName)
    {
        using var scope = server.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var create = new CreateClient
            {
                ClientId = "shared-client-id",
                ClientName = clientName,
                RequireClientSecret = true,
                AllowedGrantTypes = ["client_credentials"],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
            };

            var result = await admin.CreateAsync(create, _ct);
            result.IsSuccess.ShouldBeTrue($"Create failed for space {spaceId}: {result}");
        }
    }

    [Fact]
    public async Task client_lookup_in_space_should_use_that_spaces_client_when_store_is_cached()
    {
        await using var server = await CreateServerAsync(
            nameof(client_lookup_in_space_should_use_that_spaces_client_when_store_is_cached));

        var spaceAdmin = server.GetRequiredService<ISpaceAdmin>();
        var createSpaceResult = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] },
            _ct);
        createSpaceResult.IsSuccess.ShouldBeTrue($"Create space failed: {createSpaceResult}");
        var spaceAId = createSpaceResult.Id!;

        // Seed the same client id in both spaces; if space context is lost, Default's client is returned.
        await CreateClientInSpaceAsync(server, SpaceId.Default, "default-client");
        await CreateClientInSpaceAsync(server, spaceAId, "space-a-client");

        using var client = server.CreateClient();

        var response = await client.GetAsync("/t/space-a/test/client", _ct);
        var body = await response.Content.ReadAsStringAsync(_ct);

        body.ShouldBe("space-a-client",
            "a client lookup issued under /t/space-a must use space A's own client");
    }
}
