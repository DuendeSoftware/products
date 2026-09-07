// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Conformance.Host;

/// <summary>
/// Static client registrations for the OIDC Core and FAPI 2.0 conformance suites.
/// OidcCore clients use client_secret_basic/post.
/// FAPI 2.0 clients use private_key_jwt with fixed RSA key pairs embedded in the test plan config.
/// Logout plans register their own clients dynamically via DCR.
/// </summary>
internal static class ConformanceClients
{
    public const string ClientId = "conformance-suite";
    public const string ClientSecret = "conformance-secret";
    public const string Client2Id = "conformance-suite-post";
    public const string Client2Secret = "conformance-secret-post";
    public const string RedirectUri = "https://localhost:8443/test/a/duende-is-oidc-core/callback";

    // FAPI 2.0 client IDs — registered with fixed public JWKs matching the private keys
    // embedded in test-plan-config-fapi2.json.
    public const string Fapi2ClientId = "fapi2-conformance-client";
    public const string Fapi2Client2Id = "fapi2-conformance-client-2";
    public const string Fapi2RedirectUri = "https://localhost:8443/test/a/duende-is-fapi2/callback";

    // Public JWKs for FAPI 2.0 clients (private keys are in test-plan-config-fapi2.json).
    private const string Fapi2Client1PublicJwks =
        """
        {
            "keys":
            [
                {
                "kty": "RSA",
                "use": "sig",
                "alg": "PS256",
                "kid": "fapi2-client1-key",
                "n": "3gBOBxGMLk3nE0gmusi1yzl0zrP4__EnpREBPHlr-YWbH9HVkbJpuJkyF2JLvuYhDM-G_KC7836JuB2Sax5Q41t4l5f3p9R5CoopdwCoHXb470NXHj7Ks1LGYrI6fk1lvzh_y97zqSefYWaahp4JCt3shaCo60NEu0lHFcR_mcmZ3gL4a1dLqu7boA2fbGLobD_aEmbvrNmcjZvSgBSvivpLD--P8q2GNPzJEpY4_TCKsNMeynixIOa4YgSkRFDr_B1pzIrYFjbX3LzEMjRP55JmyHQwGb1tU1dOf-10eJJKs4Qz0mGFqNgMjVVNl00uF3YsslGCWnIMmDND-RiOJQ",
                "e": "AQAB"
                }
            ]
        }
        """;

    private const string Fapi2Client2PublicJwks =
        """
        {
            "keys":
            [
                {
                "kty": "RSA",
                "use": "sig",
                "alg": "PS256",
                "kid": "fapi2-client2-key",
                "n": "0TnW9FMwavgc6Vsnm3AZ4lA9xj4THuIGK9HNOw1IR1YyGU80YfxvtlJiP2FeiGq4byahn2-AjrbJxxHFuRdyh5jn6EdBNRtaCQIpYKEAc_93dnHy_QdpDMrpemCNCdhGG_PbB1EbDiwO7z6F8ODGtn-aW8zJnafnurEdOE2DCqmVwKtQUUie8XMlrqyrnys5mG90Bj_CbrV8nLY84BkCf_s-U6iAYsTyqA1WahOh_gtK65pE7wT26XNbdmkYLoO2UBo_z2nEv5Lj1zBVdurh6Vqqnv7SxUlk6eBMt4DFs_XH0Sp7A897L19Pnn7yyFsXOHN0N77Id1Fo74kAzt20Pw",
                "e": "AQAB"
                }
            ]
        }
        """;

    public static List<Client> Get() =>
    [
        // FAPI 2.0 client 1: private_key_jwt, PAR, DPoP
        new Client
        {
            ClientId = Fapi2ClientId,
            ClientName = "OIDF Conformance Suite FAPI2",
            ClientSecrets =
            {
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                    Value = Fapi2Client1PublicJwks,
                },
            },
            AllowedGrantTypes = GrantTypes.Code,
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                IdentityServerConstants.StandardScopes.Address,
                IdentityServerConstants.StandardScopes.Phone,
                IdentityServerConstants.StandardScopes.OfflineAccess,
            },
            RedirectUris = { Fapi2RedirectUri },
            RequirePkce = true,
            RequirePushedAuthorization = true,
            RequireDPoP = true,
            RequireConsent = false,
            AllowOfflineAccess = true,
            RequireClientSecret = true,
            // FAPI2 requires authorization codes to expire within 60 seconds (FAPI2-SP-ID2-5.3.1.1-11).
            AuthorizationCodeLifetime = 60,
        },

        // FAPI 2.0 client 2: private_key_jwt, PAR, DPoP (used for cross-client tests)
        new Client
        {
            ClientId = Fapi2Client2Id,
            ClientName = "OIDF Conformance Suite FAPI2 (2)",
            ClientSecrets =
            {
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.JsonWebKey,
                    Value = Fapi2Client2PublicJwks,
                },
            },
            AllowedGrantTypes = GrantTypes.Code,
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                IdentityServerConstants.StandardScopes.Address,
                IdentityServerConstants.StandardScopes.Phone,
                IdentityServerConstants.StandardScopes.OfflineAccess,
            },
            RedirectUris = { Fapi2RedirectUri },
            RequirePkce = true,
            RequirePushedAuthorization = true,
            RequireDPoP = true,
            RequireConsent = false,
            AllowOfflineAccess = true,
            RequireClientSecret = true,
            // FAPI2 requires authorization codes to expire within 60 seconds (FAPI2-SP-ID2-5.3.1.1-11).
            AuthorizationCodeLifetime = 60,
        },

        // Primary client: client_secret_basic
        new Client
        {
            ClientId = ClientId,
            ClientName = "OIDF Conformance Suite",
            ClientSecrets = { new Secret(ClientSecret.Sha256()) },
            AllowedGrantTypes = GrantTypes.Code,
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                IdentityServerConstants.StandardScopes.Address,
                IdentityServerConstants.StandardScopes.Phone,
                IdentityServerConstants.StandardScopes.OfflineAccess,
            },
            RedirectUris = { RedirectUri },
            PostLogoutRedirectUris = { RedirectUri },
            RequirePkce = false,
            RequireConsent = false,
            AllowOfflineAccess = true,
        },

        // Secondary client: client_secret_post
        new Client
        {
            ClientId = Client2Id,
            ClientName = "OIDF Conformance Suite (POST)",
            ClientSecrets = { new Secret(Client2Secret.Sha256()) },
            AllowedGrantTypes = GrantTypes.Code,
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                IdentityServerConstants.StandardScopes.Email,
                IdentityServerConstants.StandardScopes.Address,
                IdentityServerConstants.StandardScopes.Phone,
                IdentityServerConstants.StandardScopes.OfflineAccess,
            },
            RedirectUris = { RedirectUri },
            PostLogoutRedirectUris = { RedirectUri },
            RequirePkce = false,
            RequireConsent = false,
            AllowOfflineAccess = true,
        },
    ];
}
