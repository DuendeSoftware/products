// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace IdentityServerHost.Configuration;

public static class ClientsWeb
{
    private static string[] allowedScopes =
    {
        IdentityServerConstants.StandardScopes.OpenId,
        IdentityServerConstants.StandardScopes.Profile,
        IdentityServerConstants.StandardScopes.Email,
        "resource1.scope1",
        "resource2.scope1",
        "transaction",
        "custom.profile"
    };

    public static IEnumerable<Client> Get()
    {
        return new List<Client>
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
            // MVC PAR Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "mvc.par",
                ClientName = "MVC PAR Client",

                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                RequireRequestObject = false,

                RequireConsent = true,
                AllowedGrantTypes = GrantTypes.Code,

                RequirePushedAuthorization = true,
                // RedirectUris = { "https://localhost:44305/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:44305/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:44305/signout-callback-oidc" },

                AllowOfflineAccess = true,

                AllowedScopes = allowedScopes
            },


            //////////////////////////////////////////////////////
            // MVC PAR Sample with JAR and Jwt Private Key Auth
            //////////////////////////////////////////////////////
            new Client
            {
                ClientId = "mvc.jar.par",
                ClientName = "MVC PAR Client with JAR",

                ClientSecrets =
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                        Value = """
                                {"e":"AQAB",
                                 "kid":"ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA",
                                 "kty":"RSA",
                                 "n":"wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw"
                                }
                                """
                    }
                },

                RequireRequestObject = true,

                RequireConsent = true,
                AllowedGrantTypes = GrantTypes.Code,

                RedirectUris = { "https://localhost:44306/signin-oidc" },
                FrontChannelLogoutUri = "https://localhost:44306/signout-oidc",
                PostLogoutRedirectUris = { "https://localhost:44306/signout-callback-oidc" },

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
        };
    }
}
