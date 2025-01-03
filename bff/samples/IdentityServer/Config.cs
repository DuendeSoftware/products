// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityModel;

namespace IdentityServerHost
{
    public static class Config
    {
        public static IEnumerable<IdentityResource> IdentityResources =>
            [
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
            ];

        public static IEnumerable<ApiScope> ApiScopes =>
            [
                new ApiScope("api", ["name"]),
                new ApiScope("scope-for-isolated-api", ["name"]),
            ];

        public static IEnumerable<ApiResource> ApiResources =>
            [
                new ApiResource("urn:isolated-api", "isolated api")
                {
                    RequireResourceIndicator = true,
                    Scopes = { "scope-for-isolated-api" }
                }
            ];

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

                    RedirectUris = { "https://localhost:5002/signin-oidc" },
                    FrontChannelLogoutUri = "https://localhost:5002/signout-oidc",
                    PostLogoutRedirectUris = { "https://localhost:5002/signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    AccessTokenLifetime = 75 // Force refresh
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

                    RedirectUris = { "https://localhost:5003/signin-oidc" },
                    FrontChannelLogoutUri = "https://localhost:5003/signout-oidc",
                    PostLogoutRedirectUris = { "https://localhost:5003/signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    AccessTokenLifetime = 75 // Force refresh
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
                    RedirectUris = { "https://localhost:5004/signin-oidc" },
                    FrontChannelLogoutUri = "https://localhost:5004/signout-oidc",
                    BackChannelLogoutUri = "https://localhost:5004/bff/backchannel",
                    PostLogoutRedirectUris = { "https://localhost:5004/signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    AccessTokenLifetime = 75 // Force refresh
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

                    RedirectUris = { "https://localhost:5005/signin-oidc" },
                    PostLogoutRedirectUris = { "https://localhost:5005/signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    AccessTokenLifetime = 75
                 }
            ];
    }
}