// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace IdentityServerHost.Configuration;

public static class ClientsWeb
{
    private static readonly string[] allowedScopes =
    [
        IdentityServerConstants.StandardScopes.OpenId,
        IdentityServerConstants.StandardScopes.Profile,
        IdentityServerConstants.StandardScopes.Email,
        "resource1.scope1",
        "resource2.scope1",
        "transaction",
        "custom.profile"
    ];

    public static IEnumerable<Client> Get() => new List<Client>
        {
            ///////////////////////////////////////////
            // JS OIDC Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "js_oidc",
                ClientName = "JavaScript OIDC Client",
                ClientUri = "http://identityserver.io",

                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = false,

                RedirectUris =
                {
                    "https://localhost:44300/index.html",
                    "https://localhost:44300/callback.html",
                    "https://localhost:44300/silent.html",
                    "https://localhost:44300/popup.html"
                },

                PostLogoutRedirectUris = { "https://localhost:44300/index.html" },
                AllowedCorsOrigins = { "https://localhost:44300" },

                AllowedScopes = allowedScopes
            },

            ///////////////////////////////////////////
            // MVC Automatic Token Management Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "mvc.tokenmanagement",

                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,

                AccessTokenLifetime = 75,

                RedirectUris = { "https://localhost:44301/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:44301/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:44301/signout-callback-oidc" },

                AllowOfflineAccess = true,

                AllowedScopes = allowedScopes
            },

            ///////////////////////////////////////////
            // MVC Code Flow Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "mvc.code",
                ClientName = "MVC Code Flow",
                ClientUri = "http://identityserver.io",

                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                RequireConsent = false,
                AllowedGrantTypes = GrantTypes.Code,

                RedirectUris = { "https://localhost:44302/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:44302/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:44302/signout-callback-oidc" },

                AllowOfflineAccess = true,

                AllowedScopes = allowedScopes,
                InitiateLoginUri = "https://localhost:44302/Home/Secure"
            },

            ///////////////////////////////////////////
            // MVC DPoP Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "mvc.dpop",
                ClientName = "MVC DPoP Client",
                ClientUri = "http://identityserver.io",

                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                RequireConsent = false,
                AllowedGrantTypes = GrantTypes.Code,

                RequireDPoP = true,

                RedirectUris = { "https://localhost:44310/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:44310/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:44310/signout-callback-oidc" },

                AllowOfflineAccess = true,

                AllowedScopes = allowedScopes
            },

            ///////////////////////////////////////////
            // MVC Hybrid Flow Sample (Back Channel logout)
            //////////////////////////////////////////
            new Client
            {
                ClientId = "mvc.hybrid.backchannel",
                ClientName = "MVC Hybrid (with BackChannel logout)",
                ClientUri = "http://identityserver.io",

                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.Hybrid,
                RequirePkce = false,

                RedirectUris = { "https://localhost:44303/signin-oidc" },
                BackChannelLogoutUri = "https://localhost:44303/logout",
                PostLogoutRedirectUris = { "https://localhost:44303/signout-callback-oidc" },

                AllowOfflineAccess = true,

                AllowedScopes = allowedScopes
            },

            ///////////////////////////////////////////
            // MVC Code Flow with JAR/JWT Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "mvc.jar.jwt",
                ClientName = "MVC Code Flow with JAR/JWT",

                ClientSecrets =
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                        Value =
                        """
                        {
                            "e":"AQAB",
                            "kid":"ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA",
                            "kty":"RSA",
                            "n":"wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw"
                        }
                        """
                    }
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequireRequestObject = true,

                RedirectUris = { "https://localhost:44304/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:44304/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:44304/signout-callback-oidc" },

                AllowOfflineAccess = true,

                AllowedScopes = allowedScopes
            },

            ///////////////////////////////////////////
            // MVC Code Flow with JAR URI/JWT Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "mvc.jar-uri.jwt",
                ClientName = "MVC Code Flow with JAR/JWT",

                ClientSecrets =
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                        Value =
                            """
                            {
                                "e":"AQAB",
                                "kid":"ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA",
                                "kty":"RSA",
                                "n":"wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw"
                            }
                            """
                    }
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequireRequestObject = true,

                RedirectUris = { "https://localhost:44305/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:44305/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:44305/signout-callback-oidc" },

                AllowOfflineAccess = true,

                AllowedScopes = allowedScopes
            },

            ///////////////////////////////////////////
            // MVC Code Flow with DPoP, JAR, PAR, Private Key JWT, Back Channel Logout
            //////////////////////////////////////////
            new Client
            {
                ClientId = "web",
                ClientName = "Web Security Baseline",

                ClientSecrets =
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                        Value =
                            """
                            {
                                "kty": "RSA",
                                "e": "AQAB",
                                "use": "sig",
                                "kid": "web-0001",
                                "alg": "PS256",
                                "n": "oTAx8S7xFwQ7gFixieULyMG9JIeNLzLkXdw7rRCRjKhJy67jPjHkbT51uDTntWc_rx7S6GoKBjJCCau1JnBS9Z9UX7d84Ado0aeLCYjZPOMRm1u0OB6kxOa46bB4-uke7fnWTQN8motNycvyXFd7kENqtkk2hmxB1wvr1WPSnJ037JqJ3-j9ZEM016GCj98_R_aJtJQg2jhv9rGJMIRdr2JhzAjKFg4m6W_MRSdxzrEtF3mNGGIpIPRw8_bH5uvQG6dIUfpOWr1IPbmIzbk5JOAwrtthG_1v8J-8QJ8Md5IJgMvKBTow5pX2YTE632vHVZedL3lhopehDQJzqpRo-w"
                            }
                            """
                    }
                },

                AllowedGrantTypes = GrantTypes.Code,

                RedirectUris = { "https://localhost:44306/signin-oidc" },
                BackChannelLogoutUri = "https://localhost:44306/BackChannelLogout",
                PostLogoutRedirectUris = { "https://localhost:44306/signout-callback-oidc" },

                RequireDPoP = true,
                RequireRequestObject = true,
                RequirePushedAuthorization = true,

                AllowOfflineAccess = true,

                AllowedScopes = allowedScopes
            },
        };
}
