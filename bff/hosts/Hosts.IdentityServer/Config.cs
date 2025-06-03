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
    private static Uri bffBlazorWebAssemblyUrl = ServiceDiscovery.ResolveService(AppHostServices.BffBlazorWebassembly);

    public static IEnumerable<Client> Clients =>
    [
                new Client
                {
                    ClientId = "bff",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },

                    RedirectUris = { $"{bffUrl}signin-oidc" },
                    FrontChannelLogoutUri = $"{bffUrl}signout-oidc",
                    PostLogoutRedirectUris = { $"{bffUrl}signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    RefreshTokenExpiration = TokenExpiration.Absolute,
                    AbsoluteRefreshTokenLifetime = 60,
                    AccessTokenLifetime = 15 // Force refresh
                },
                new Client
                {
                    ClientId = "bff.multi-frontend.default",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },
                    RedirectUris = { $"{bffMultiFrontendUrl}bff-signin" },
                    FrontChannelLogoutUri = $"{bffMultiFrontendUrl}signout-oidc",
                    PostLogoutRedirectUris = { $"{bffMultiFrontendUrl}signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    RefreshTokenExpiration = TokenExpiration.Absolute,
                    AbsoluteRefreshTokenLifetime = 60,
                    AccessTokenLifetime = 15 // Force refresh
                },
                new Client
                {
                    ClientId = "bff.multi-frontend.config",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },
                    RedirectUris = { $"{bffMultiFrontendUrl}from-config/bff-signin" },
                    FrontChannelLogoutUri = $"{bffMultiFrontendUrl}from-config/signout-oidc",
                    PostLogoutRedirectUris = { $"{bffMultiFrontendUrl}from-config/signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    RefreshTokenExpiration = TokenExpiration.Absolute,
                    AbsoluteRefreshTokenLifetime = 60,
                    AccessTokenLifetime = 15 // Force refresh
                },
                new Client
                {
                    ClientId = "bff.multi-frontend.with-path",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },
                    RedirectUris = { $"{bffMultiFrontendUrl}with-path/bff-signin" },
                    FrontChannelLogoutUri = $"{bffMultiFrontendUrl}signout-oidc",
                    PostLogoutRedirectUris = { $"{bffMultiFrontendUrl}signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    RefreshTokenExpiration = TokenExpiration.Absolute,
                    AbsoluteRefreshTokenLifetime = 60,
                    AccessTokenLifetime = 15 // Force refresh
                },
                new Client
                {
                    ClientId = "bff.multi-frontend.with-domain",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },
                    RedirectUris = { $"https://app1.localhost:5005/bff-signin" },
                    FrontChannelLogoutUri = $"https://app1.localhost:5005/signout-oidc",
                    PostLogoutRedirectUris = { $"https://app1.localhost:5005/signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    RefreshTokenExpiration = TokenExpiration.Absolute,
                    AbsoluteRefreshTokenLifetime = 60,
                    AccessTokenLifetime = 15 // Force refresh
                },
                new Client
                {
                    ClientId = "bff.dpop",
                    ClientSecrets = { new Secret("secret".Sha256()) },
                    RequireDPoP = true,

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },

                    RedirectUris = { $"{bffDPopUrl}signin-oidc" },
                    FrontChannelLogoutUri = $"{bffDPopUrl}signout-oidc",
                    PostLogoutRedirectUris = { $"{bffDPopUrl}signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    // Intentionally set lifetime short to see what happens when access and refresh tokens expire
                    AccessTokenLifetime = 15,
                    RefreshTokenExpiration = TokenExpiration.Absolute,
                    AbsoluteRefreshTokenLifetime = 60
                },
                new Client
                {
                    ClientId = "bff.ef",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },
                    RedirectUris = { $"{bffEfUrl}signin-oidc" },
                    FrontChannelLogoutUri = $"{bffEfUrl}signout-oidc",
                    BackChannelLogoutUri = $"{bffEfUrl}bff/backchannel",
                    PostLogoutRedirectUris = { $"{bffEfUrl}signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    // Intentionally set lifetime short to see what happens when access and refresh tokens expire
                    AccessTokenLifetime = 15,
                    RefreshTokenExpiration = TokenExpiration.Absolute,
                    AbsoluteRefreshTokenLifetime = 60
                },

                new Client
                {
                    ClientId = "blazor",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },

                    RedirectUris =
                    {
                        $"{bffBlazorWebAssemblyUrl}signin-oidc",
                        $"{bffBlazorPerComponentUrl}signin-oidc",
                        "https://localhost:7035/signin-oidc"
                    },
                    PostLogoutRedirectUris =
                    {
                        $"{bffBlazorWebAssemblyUrl}signout-callback-oidc", $"{bffBlazorPerComponentUrl}signout-callback-oidc"
                    },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    // Intentionally set lifetime short to see what happens when access and refresh tokens expire
                    AccessTokenLifetime = 15,
                    RefreshTokenExpiration = TokenExpiration.Absolute,
                    AbsoluteRefreshTokenLifetime = 60
                }
            ];


}
