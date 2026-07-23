// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.IdentityProviders;

/// <summary>
/// End-to-end federation tests verifying that identity providers configured via
/// <see cref="IIdentityProviderAdmin"/> are resolved at runtime by the dynamic provider
/// infrastructure and correctly redirect to the upstream IdP.
/// </summary>
public sealed class IdentityProviderAdminIntegrationTests(WebServerFixture webApp) : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private const string FederationScheme = "upstream_idp";

    private KestrelBasedTestServer _upstreamIdp = null!;
    private KestrelBasedTestServer _identityServer = null!;
    private IServiceScope? _adminScope;

    private IIdentityProviderAdmin Admin =>
        _adminScope!.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

    public async ValueTask InitializeAsync()
    {
        var output = TestContext.Current.TestOutputHelper!;

        // 2. Main IdentityServer — storage-backed configuration with dynamic providers
        // (constructed first so we can reference its base address in the upstream IdP's client config)
        var dbName = $"federation_{Guid.NewGuid():N}";
        _identityServer = new KestrelBasedTestServer(
            "identity-server",
            webApp,
            new PrefixedTestOutputHelper(output, "identity-server"),
            services =>
            {
                services.AddRouting();

                services.AddStorageInternal(storage =>
                    storage.AddSqliteStore(opt =>
                        opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

                services.AddIdentityServer(options =>
                    {
                        options.EmitStaticAudienceClaim = true;
                    })
                    .AddConfigurationStorage()
                    .AddOperationalStorage()
                    .AddInMemoryApiScopes([new ApiScope("scope1")])
                    .AddInMemoryIdentityResources([new Models.IdentityResources.OpenId()]);
            },
            webapp =>
            {
                webapp.UseIdentityServer();

                // Expose a /challenge endpoint that triggers external auth
                webapp.MapGet("/challenge", async ctx =>
                {
                    var scheme = ctx.Request.Query["scheme"].ToString();
                    await ctx.ChallengeAsync(scheme,
                        new AuthenticationProperties { RedirectUri = "/callback" });
                });
            });

        // 1. Upstream IdP — a minimal IdentityServer that serves OIDC discovery
        // The federation redirect URI uses the identity server's base address
        var federationRedirectUri = _identityServer.BuildUrl($"/federation/{FederationScheme}/signin").ToString();
        _upstreamIdp = new KestrelBasedTestServer(
            "upstream-idp",
            webApp,
            new PrefixedTestOutputHelper(output, "upstream-idp"),
            services =>
            {
                services.AddRouting();
                services.AddIdentityServer()
                    .AddInMemoryClients([
                        new Client
                        {
                            ClientId = "federation-client",
                            ClientSecrets = [new Secret("federation-secret".Sha256())],
                            AllowedGrantTypes = GrantTypes.Code,
                            RedirectUris = [federationRedirectUri],
                            AllowedScopes = ["openid"],
                            RequirePkce = true
                        }
                    ])
                    .AddInMemoryIdentityResources([new Models.IdentityResources.OpenId()])
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp =>
            {
                webapp.UseIdentityServer();
            });

        await _upstreamIdp.StartAsync();
        await _identityServer.StartAsync();

        // Run schema migration
        var schema = _identityServer.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        _adminScope = _identityServer.Services.CreateScope();
    }

    [Fact]
    public async Task challenge_against_admin_created_provider_redirects_to_upstream_idp()
    {
        // Arrange: create a dynamic OIDC provider via the admin API pointing to the upstream IdP
        var createResult = await Admin.CreateAsync(
            new IdentityProviderConfiguration
            {
                Scheme = FederationScheme,
                Type = "oidc",
                Enabled = true,
                DisplayName = "Upstream IdP",
                Properties = new Dictionary<string, string>
                {
                    ["Authority"] = _upstreamIdp.BaseAddress.ToString().TrimEnd('/'),
                    ["ClientId"] = "federation-client",
                    ["ClientSecret"] = "federation-secret",
                    ["ResponseType"] = "code"
                }
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        // Act: challenge against the dynamically created scheme
        var client = _identityServer.CreateClient(allowAutoRedirect: false);
        var response = await client.GetAsync($"/challenge?scheme={FederationScheme}", _ct);

        // Assert: should redirect to the upstream IdP's authorize endpoint
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain(_upstreamIdp.BaseAddress.Host);
        location.ShouldContain("/connect/authorize");
    }

    [Fact]
    public async Task challenge_against_nonexistent_scheme_returns_error()
    {
        var client = _identityServer.CreateClient(allowAutoRedirect: false);
        var response = await client.GetAsync("/challenge?scheme=nonexistent_scheme", _ct);

        // When no handler is found for a scheme, ASP.NET Core returns 500
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task get_all_scheme_names_includes_admin_created_provider()
    {
        var scheme = $"list_{Guid.NewGuid():N}";
        var createResult = await Admin.CreateAsync(
            new IdentityProviderConfiguration
            {
                Scheme = scheme,
                Type = "oidc",
                Enabled = true,
                DisplayName = "Listed Provider",
                Properties = new Dictionary<string, string>
                {
                    ["Authority"] = _upstreamIdp.BaseAddress.ToString().TrimEnd('/'),
                    ["ClientId"] = "federation-client"
                }
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue($"CreateAsync failed: {createResult}");

        // Resolve IIdentityProviderStore from a fresh scope
        using var scope = _identityServer.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdentityProviderStore>();

        var names = await store.GetAllSchemeNamesAsync(_ct);
        names.ShouldContain(n => n.Scheme == scheme);
    }

    public async ValueTask DisposeAsync()
    {
        _adminScope?.Dispose();
        _adminScope = null;

        await _identityServer.DisposeAsync();
        await _upstreamIdp.DisposeAsync();
    }
}
