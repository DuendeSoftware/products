// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;

public class IdentityServerTestHost(IScenarioConfigurator configurator,
    string name,
    Action<IIdentityServerBuilder>? configureIdentityServer = null,
    Action<IdentityServerOptions>? configureOptions = null,
    Action<IServiceCollection>? configureServices = null) : TestHost(configurator, name)
{
    private readonly ICollection<Client> _clients = new List<Client>();
    private IEnumerable<IdentityResource> _identityResources = [];
    private IEnumerable<ApiScope> _apiScopes = [];
    private IEnumerable<ApiResource> _apiResources = [];
    private List<TestUser> _testUsers = [];

    protected override WebApplication CreateApp(WebApplicationBuilder builder)
    {
        configureServices?.Invoke(builder.Services);

        var identityServerBuilder = builder.Services.AddIdentityServer(configureOptions ?? (_ => { }));

        identityServerBuilder.AddInMemoryClients(_clients);
        identityServerBuilder.AddInMemoryIdentityResources(_identityResources);
        identityServerBuilder.AddInMemoryApiScopes(_apiScopes);
        identityServerBuilder.AddInMemoryApiResources(_apiResources);

        identityServerBuilder.AddTestUsers(_testUsers);

        builder.ServeEmbeddedUi("IdentityServerHost");
        configureIdentityServer?.Invoke(identityServerBuilder);

        builder.Services.AddRazorPages()
            .WithRazorPagesRoot("/IdentityServerHost/Pages")
            .AddApplicationPart(typeof(IdentityServerTestHost).Assembly);

        var app = builder.Build();

        app.UseStaticFiles();
        app.UseRouting();
        app.UseIdentityServer();
        app.UseAuthorization();
        app.MapRazorPages();

        return app;
    }

    public void AddDefaultResources()
    {
        var address = new
        {
            street_address = "One Hacker Way",
            locality = "Heidelberg",
            postal_code = "69118",
            country = "Germany"
        };

        SetIdentityResources(
        [
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
            new IdentityResource("custom.profile",
                [JwtClaimTypes.Name, JwtClaimTypes.Email, "location", JwtClaimTypes.Address])
        ]);

        SetApiScopes(
        [
            new ApiScope("resource1.scope1"),
            new ApiScope("resource1.scope2"),
            new ApiScope("resource2.scope1"),
            new ApiScope("resource2.scope2")
        ]);

        SetApiResources(
        [
            new ApiResource("urn:resource1", "Resource 1")
            {
                Scopes = { "resource1.scope1", "resource1.scope2" }
            },
            new ApiResource("urn:resource2", "Resource 2")
            {
                UserClaims = { JwtClaimTypes.Name, JwtClaimTypes.Email },
                Scopes = { "resource2.scope1", "resource2.scope2" }
            }
        ]);
    }

    public void AddDefaultUsers()
    {
        var address = new
        {
            street_address = "One Hacker Way",
            locality = "Heidelberg",
            postal_code = "69118",
            country = "Germany"
        };

        SetTestUsers(
        [
            new TestUser
            {
                SubjectId = "1",
                Username = "alice",
                Password = "alice",
                Claims =
                {
                    new Claim(JwtClaimTypes.Name, "Alice Smith"),
                    new Claim(JwtClaimTypes.GivenName, "Alice"),
                    new Claim(JwtClaimTypes.FamilyName, "Smith"),
                    new Claim(JwtClaimTypes.Email, "AliceSmith@example.com"),
                    new Claim(JwtClaimTypes.EmailVerified, "true", ClaimValueTypes.Boolean),
                    new Claim(JwtClaimTypes.WebSite, "http://alice.example.com"),
                    new Claim(JwtClaimTypes.Address, JsonSerializer.Serialize(address),
                        IdentityServerConstants.ClaimValueTypes.Json)
                }
            },
            new TestUser
            {
                SubjectId = "2",
                Username = "bob",
                Password = "bob",
                Claims =
                {
                    new Claim(JwtClaimTypes.Name, "Bob Smith"),
                    new Claim(JwtClaimTypes.GivenName, "Bob"),
                    new Claim(JwtClaimTypes.FamilyName, "Smith"),
                    new Claim(JwtClaimTypes.Email, "BobSmith@example.com"),
                    new Claim(JwtClaimTypes.EmailVerified, "true", ClaimValueTypes.Boolean),
                    new Claim(JwtClaimTypes.WebSite, "http://bob.example.com"),
                    new Claim(JwtClaimTypes.Address, JsonSerializer.Serialize(address),
                        IdentityServerConstants.ClaimValueTypes.Json)
                }
            }
        ]);
    }

    public void AddClient(Client client) => _clients.Add(client);
    public void AddClient(TestHost testHost, Action<Client> configure)
    {
        var client = new Client()
        {
            ClientId = Name,
            ClientSecrets = [new Secret("secret".Sha256())],
            RedirectUris = [testHost.BuildUri("signin-oidc").ToString()]
        };
        configure(client);
        _clients.Add(client);
    }

    public void SetIdentityResources(IEnumerable<IdentityResource> resources) => _identityResources = resources;
    public void SetApiScopes(IEnumerable<ApiScope> scopes) => _apiScopes = scopes;
    public void SetApiResources(IEnumerable<ApiResource> resources) => _apiResources = resources;
    public void SetTestUsers(List<TestUser> users) => _testUsers = users;
}
