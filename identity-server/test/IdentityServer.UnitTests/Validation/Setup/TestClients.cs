// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace UnitTests.Validation.Setup;

internal class TestClients
{
    public static IEnumerable<Client> Get() => new List<Client>
        {
            new Client
            {
                ClientName = "Code Client",
                Enabled = true,
                ClientId = "codeclient",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", "resource", "resource2", "scope1" },

                RequireConsent = false,
                RequirePkce = false,

                RedirectUris = new List<string>
                {
                    "https://server/cb"
                },

                AuthorizationCodeLifetime = 60
            },
            new Client
            {
                ClientName = "Code Client (allows plain text PKCE)",
                Enabled = true,
                ClientId = "codeclient.plain",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },
                AllowPlainTextPkce = true,

                RequireConsent = false,

                RedirectUris = new List<string>
                {
                    "https://server/cb"
                },

                AuthorizationCodeLifetime = 60
            },
            new Client
            {
                ClientName = "Code Client with PKCE",
                Enabled = true,
                ClientId = "codeclient.pkce",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },

                RequireConsent = false,

                RedirectUris = new List<string>
                {
                    "https://server/cb"
                },

                AuthorizationCodeLifetime = 60
            },
            new Client
            {
                ClientName = "Code Client with PKCE and plain allowed",
                Enabled = true,
                ClientId = "codeclient.pkce.plain",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                AllowPlainTextPkce = true,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },

                RequireConsent = false,

                RedirectUris = new List<string>
                {
                    "https://server/cb"
                },

                AuthorizationCodeLifetime = 60
            },
            new Client
            {
                ClientName = "Hybrid Client",
                Enabled = true,
                ClientId = "hybridclient",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Hybrid,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },
                AllowAccessTokensViaBrowser = true,

                RequireConsent = false,
                RequirePkce = false,

                RedirectUris = new List<string>
                {
                    "https://server/cb"
                },

                AuthorizationCodeLifetime = 60
            },
            new Client
            {
                ClientName = "Hybrid Client with PKCE",
                Enabled = true,
                ClientId = "hybridclient.pkce",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Hybrid,
                RequirePkce = true,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },
                AllowAccessTokensViaBrowser = true,

                RequireConsent = false,

                RedirectUris = new List<string>
                {
                    "https://server/cb"
                },

                AuthorizationCodeLifetime = 60
            },
            new Client
            {
                ClientName = "Hybrid Client",
                Enabled = true,
                ClientId = "hybridclient_no_aavb",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Hybrid,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },
                AllowAccessTokensViaBrowser = false,

                RequireConsent = false,
                RequirePkce = false,

                RedirectUris = new List<string>
                {
                    "https://server/cb"
                },

                AuthorizationCodeLifetime = 60
            },
            new Client
            {
                ClientName = "Implicit Client",
                ClientId = "implicitclient",

                AllowedGrantTypes = GrantTypes.Implicit,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },
                AllowAccessTokensViaBrowser = true,

                RequireConsent = false,

                RedirectUris = new List<string>
                {
                    "oob://implicit/cb"
                }
            },
            new Client
            {
                ClientName = "Implicit Client",
                ClientId = "implicitclient_no_aavb",

                AllowedGrantTypes = GrantTypes.Implicit,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },
                AllowAccessTokensViaBrowser = false,

                RequireConsent = false,

                RedirectUris = new List<string>
                {
                    "oob://implicit/cb"
                }
            },
            new Client
            {
                ClientName = "Implicit and Client Credentials Client",
                Enabled = true,
                ClientId = "implicit_and_client_creds_client",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ImplicitAndClientCredentials,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },
                RequireConsent = false,

                RedirectUris = new List<string>
                {
                    "oob://implicit/cb"
                }
            },
            new Client
            {
                ClientName = "Code Client with Scope Restrictions",
                Enabled = true,
                ClientId = "codeclient_restricted",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequireConsent = false,
                RequirePkce = false,

                AllowedScopes = new List<string>
                {
                    "openid"
                },

                RedirectUris = new List<string>
                {
                    "https://server/cb"
                }
            },
            new Client
            {
                ClientName = "Client Credentials Client",
                Enabled = true,
                ClientId = "client",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "openid", "profile", "resource", "resource2", "scope1" },

                AllowOfflineAccess = true,

                AccessTokenType = AccessTokenType.Jwt
            },
            new Client
            {
                ClientName = "Client Credentials Client (restricted)",
                Enabled = true,
                ClientId = "client_restricted",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,

                AllowedScopes = new List<string>
                {
                    "resource"
                }
            },
            new Client
            {
                ClientName = "Resource Owner Client",
                Enabled = true,
                ClientId = "roclient",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                AllowedScopes = { "openid", "profile", "resource", "resource2", "scope1" },
                AllowOfflineAccess = true
            },
            new Client
            {
                ClientName = "Resource Owner Client - Public",
                Enabled = true,
                ClientId = "roclient.public",
                RequireClientSecret = false,

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                AllowedScopes = { "openid", "profile", "resource", "resource2" }
            },
            new Client
            {
                ClientName = "Resource Owner Client",
                Enabled = true,
                ClientId = "roclient_absolute_refresh_expiration_one_time_only",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },

                RefreshTokenExpiration = TokenExpiration.Absolute,
                RefreshTokenUsage = TokenUsage.OneTimeOnly,
                AbsoluteRefreshTokenLifetime = 200
            },
            new Client
            {
                ClientName = "Resource Owner Client",
                Enabled = true,
                ClientId = "roclient_absolute_refresh_expiration_reuse",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },

                RefreshTokenExpiration = TokenExpiration.Absolute,
                RefreshTokenUsage = TokenUsage.ReUse,
                AbsoluteRefreshTokenLifetime = 200
            },
            new Client
            {
                ClientName = "Resource Owner Client",
                Enabled = true,
                ClientId = "roclient_sliding_refresh_expiration_one_time_only",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },

                RefreshTokenExpiration = TokenExpiration.Sliding,
                RefreshTokenUsage = TokenUsage.OneTimeOnly,
                AbsoluteRefreshTokenLifetime = 10,
                SlidingRefreshTokenLifetime = 4
            },
            new Client
            {
                ClientName = "Resource Owner Client",
                Enabled = true,
                ClientId = "roclient_sliding_refresh_expiration_reuse",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },

                RefreshTokenExpiration = TokenExpiration.Sliding,
                RefreshTokenUsage = TokenUsage.ReUse,
                AbsoluteRefreshTokenLifetime = 200,
                SlidingRefreshTokenLifetime = 100
            },
            new Client
            {
                ClientName = "Resource Owner Client (restricted)",
                Enabled = true,
                ClientId = "roclient_restricted",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,

                AllowedScopes = new List<string>
                {
                    "resource"
                }
            },
            new Client
            {
                ClientName = "Resource Owner Client (restricted with refresh)",
                Enabled = true,
                ClientId = "roclient_restricted_refresh",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,

                AllowOfflineAccess = true,
                AllowedScopes = new List<string>
                {
                    "resource"
                }
            },

            new Client
            {
                ClientName = "Custom Grant Client",
                Enabled = true,
                ClientId = "customgrantclient",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = { "custom_grant" },
                AllowedScopes = { "openid", "profile", "resource", "resource2" }
            },

            new Client
            {
                ClientName = "Disabled Client",
                Enabled = false,
                ClientId = "disabled",
                ClientSecrets = new List<Secret>
                {
                    new Secret("invalid".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "openid", "profile", "resource", "resource2" }
            },
            new Client
            {
                ClientName = "Reference Token Client",

                Enabled = true,
                ClientId = "referencetokenclient",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Implicit,
                RedirectUris = { "https://notused" },
                AllowedScopes = { "openid", "profile", "resource", "resource2" },

                AccessTokenType = AccessTokenType.Reference
            },
            new Client
            {
                ClientId = "wsfed",
                ClientName = "WS-Fed Client",
                ProtocolType = IdentityServerConstants.ProtocolTypes.WsFederation,
                AllowedGrantTypes = GrantTypes.Implicit,
                Enabled = true,
                AllowedScopes = { "openid", "profile", "resource", "resource2" },
                RedirectUris = { "http://wsfed/callback"  }
            },
            new Client
            {
                ClientId = "client.cred.wsfed",
                ClientName = "WS-Fed Client",
                ProtocolType = IdentityServerConstants.ProtocolTypes.WsFederation,
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("secret".Sha256()) },

                Enabled = true,
                AllowedScopes = { "openid", "profile", "resource", "resource2" }
            },
            new Client
            {
                ClientId = "client.implicit",
                ClientName = "Implicit Client",
                AllowedGrantTypes = GrantTypes.Implicit,
                RedirectUris = { "https://notused" },
                AllowedScopes = { "openid", "profile", "resource", "resource2" }
            },
            new Client
            {
                ClientId = "implicit_and_client_creds",
                AllowedGrantTypes = GrantTypes.ImplicitAndClientCredentials,
                RedirectUris = { "https://notused" },
                AllowedScopes = {"api1"}
            },
            new Client
            {
                ClientId = "device_flow",
                ClientName = "Device Flow Client",
                AllowedGrantTypes = GrantTypes.DeviceFlow,
                AllowedScopes = { "openid", "profile", "resource" },
                AllowOfflineAccess = true,
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },
            }
        };
}
