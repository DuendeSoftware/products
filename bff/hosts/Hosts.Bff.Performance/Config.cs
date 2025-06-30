// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Hosts.ServiceDefaults;

namespace IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new("api", ["name"]),
        new("scope-for-isolated-api", ["name"]),
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new("urn:isolated-api", "isolated api")
        {
            RequireResourceIndicator = true,
            Scopes = { "scope-for-isolated-api" }
        }
    ];
    // Get the BFF URL from the service discovery system. Then use this for building the redirect urls etc..
    private static Uri bffUrl = ServiceDiscovery.ResolveService(AppHostServices.Bff);
    private static Uri bffMultiFrontendUrl = ServiceDiscovery.ResolveService(AppHostServices.BffMultiFrontend);
    private static Uri bffDPopUrl = ServiceDiscovery.ResolveService(AppHostServices.BffDpop);
    private static Uri bffEfUrl = ServiceDiscovery.ResolveService(AppHostServices.BffEf);
    private static Uri bffBlazorPerComponentUrl = ServiceDiscovery.ResolveService(AppHostServices.BffBlazorPerComponent);
    private static Uri bffBlazorWebAssemblyUrl = ServiceDiscovery.ResolveService(AppHostServices.BffBlazorWebassembly); public static IEnumerable<Client> Clients =>
    [
        BuildClient("bff.perf",
            ServiceDiscovery.ResolveService(AppHostServices.BffPerf, "single"),
            ServiceDiscovery.ResolveService(AppHostServices.BffPerf, "multi"),
            new Uri("https://app1.localhost:6002")
        ),

        BuildClient("bff", client =>
        {
            client.AllowedScopes.Add("scope-for-isolated-api");
        }, bffUrl),

        BuildClient("bff.multi-frontend.default", client =>
        {
            client.AllowedScopes.Add("scope-for-isolated-api");
        }, bffMultiFrontendUrl),

        BuildClient("bff.multi-frontend.config", client =>
        {
            client.AllowedScopes.Add("scope-for-isolated-api");
        }, new Uri(bffMultiFrontendUrl, "from-config")),

        BuildClient("bff.multi-frontend.with-path", client =>
        {
            client.AllowedScopes.Add("scope-for-isolated-api");
        }, new Uri(bffMultiFrontendUrl, "with-path")),

        BuildClient("bff.multi-frontend.with-domain", client =>
        {
            client.AllowedScopes.Add("scope-for-isolated-api");
        }, new Uri("https://app1.localhost:5005")),

        BuildClient("bff.dpop", client =>
        {
            client.RequireDPoP = true;
            client.AllowedScopes.Add("scope-for-isolated-api");
        }, bffDPopUrl),

        BuildClient("bff.ef", client =>
        {
            client.BackChannelLogoutUri = $"{bffEfUrl}bff/backchannel";
            client.AllowedScopes.Add("scope-for-isolated-api");
        }, bffEfUrl),

        BuildClient("blazor", client =>
        {
            client.AllowedScopes.Add("scope-for-isolated-api");
        }, bffBlazorWebAssemblyUrl, bffBlazorPerComponentUrl, new Uri("https://localhost:7035"))
    ];


    private static Client BuildClient(string clientId, Action<Client> postConfigure, params Uri[] uris)
    {
        var client = BuildClient(clientId, uris);
        postConfigure(client);
        return client;
    }

    private static Client BuildClient(string clientId, params Uri[] uris) => new Client
    {
        ClientId = clientId,
        ClientSecrets = { new Secret("secret".Sha256()) },

        AllowedGrantTypes =
            {
                GrantType.AuthorizationCode,
                GrantType.ClientCredentials,
                OidcConstants.GrantTypes.TokenExchange
            },
        RedirectUris = uris.Select(u => new Uri(u, "signin-oidc").ToString()).ToList(),
        FrontChannelLogoutUri = new Uri(uris.First(), "signout-oidc").ToString(),
        PostLogoutRedirectUris = uris.Select(u => new Uri(u, "signout-callback-oidc").ToString()).ToList(),

        AllowOfflineAccess = true,
        AllowedScopes = { "openid", "profile", "api" },

        RefreshTokenExpiration = TokenExpiration.Absolute,
        AbsoluteRefreshTokenLifetime = 60,
        AccessTokenLifetime = 15 // Force refresh
    };

}
