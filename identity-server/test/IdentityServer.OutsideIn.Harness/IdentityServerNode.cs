// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.OutsideIn.Harness.Internal;
using Duende.IdentityServer.Test;
using Duende.Spaces;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// One in-process Kestrel host running IdentityServer on a dynamic loopback port (design doc section 05), behind its
/// topology's load balancer. Its store type and caching mode come from the permutation's store provider.
/// </summary>
public sealed class IdentityServerNode : IAsyncDisposable
{
    private readonly WebApplication _app;

    private IdentityServerNode(string id, WebApplication app, int port)
    {
        Id = id;
        _app = app;
        Port = port;
    }

    /// <summary>Identifies the node within its server, for example <c>sut-node-1</c>.</summary>
    public string Id { get; }

    internal IServiceProvider Services => _app.Services;

    /// <summary>The loopback port Kestrel bound to.</summary>
    public int Port { get; }

    /// <summary>The node's own direct URL, for tests that need to bypass the load balancer. Plain HTTP.</summary>
    public Uri DirectUrl => new($"http://127.0.0.1:{Port}");

    internal static async Task<IdentityServerNode> StartAsync(string id, NodeContext context, Ct cancellationToken)
    {
        using var step = context.Log.Measure($"node {id}");
        var configure = context.Log.Measure("create builder and configure services");
        var builder = HarnessHosting.CreateBuilder(context.Clock);
        builder.ListenOnLoopback(https: false);
        var services = builder.Services;

        context.DataProtection.Configure(services);

        // The load balancer terminates TLS and forwards the original scheme.
        services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.XForwardedProto);

        // Test users are kept in memory on every node whatever the store type (D20); the login pages provision into them.
        // With User Management on, users and sign-in go through it only (D20): test users are not registered at all.
        services.AddSingleton(context.TopologyStores);

        var identityServer = services.AddIdentityServer(options =>
            {
                // Automatic key management would write keys to the file system per node. Until the KeysModule
                // exists, both nodes sign with the topology's static key instead.
                options.KeyManagement.Enabled = false;
                options.UserInteraction.LoginUrl = LoginPages.LoginPath;
            })
            .AddSigningCredential(context.SigningKey, SecurityAlgorithms.RsaSha256);

        if (context.Users == UserStore.TestUsers)
        {
            identityServer.AddTestUsers(context.NodeStores.Users);
        }

        if (context.SpacesOn && context.Users == UserStore.TestUsers)
        {
            // Each space has its own users, so the store is chosen per request, in the space it resolved.
            services.RemoveAll<TestUserStore>();
            services.AddScoped(provider => new TestUserStore(context.NodeStores.UsersOf(provider.GetRequiredService<ISpaceContextAccessor>().GetSpaceIdOrDefault())));
        }

        // The store type from the permutation: in-memory, Entity Framework or Duende.Storage, each on its own databases.
        context.StoreProvider.ConfigureNode(identityServer, context.NodeIndex);

        // External providers (dynamic providers) reach remote IdPs through the harness, without DNS, on the test clock.
        services.ConfigureAll<OpenIdConnectOptions>(options => HarnessOidcClient.Apply(options, id, context.Clock, context.VirtualHosts));
        configure.Dispose();

        WebApplication app;
        using (context.Log.Measure("build"))
        {
            app = builder.Build();
        }

        // The first node prepares the topology's databases before any node takes traffic (D6).
        if (context.NodeIndex == 0)
        {
            using (context.Log.Measure("store initialization"))
            {
                await context.StoreProvider.InitializeAsync(app.Services, context.Log, cancellationToken);
            }
        }

        app.UseRequestNumbering(context.Log);
        app.UseForwardedHeaders();
        if (context.SpacesOn)
        {
            app.UseSpaceResolution();

            // Endpoint routing would otherwise match before the space rewrites the path, so harness pages under a space's path would not be found.
            app.UseRouting();
        }

        app.UseIdentityServer();
        context.LoginPage?.Invoke(app);

        using (context.Log.Measure("start"))
        {
            await app.StartAsync(cancellationToken);
        }

        return new IdentityServerNode(id, app, app.GetPort());
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
