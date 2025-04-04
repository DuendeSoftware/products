// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using IntegrationTests.Common;

namespace IntegrationTests.Clients.Setup;

internal class Clients
{
    public static IEnumerable<Client> Get() => new List<Client>
        {
            ///////////////////////////////////////////
            // Console Client Credentials Flow Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "client",
                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowOfflineAccess = true,

                AllowedScopes =
                {
                    "api1", "api2", "other_api"
                }
            },
            new Client
            {
                ClientId = "client.cnf",
                ClientSecrets =
                {
                    new Secret
                    {
                        Type = "confirmation.test",
                        Description = "Test for cnf claim",
                        Value = "foo"
                    }
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowOfflineAccess = true,

                AllowedScopes =
                {
                    "api1", "api2"
                }
            },
            new Client
            {
                ClientId = "client.and.ro",
                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPasswordAndClientCredentials,

                AllowedScopes =
                {
                    "openid",
                    "api1", "api2"
                }
            },
            new Client
            {
                ClientId = "client.identityscopes",
                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,

                AllowedScopes =
                {
                    "openid", "profile",
                    "api1", "api2"
                }
            },
            new Client
            {
                ClientId = "client.no_default_scopes",
                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials
            },
            new Client
            {
                ClientId = "client.no_secret",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                RequireClientSecret = false,
                AllowedScopes = { "api1" }
            },

            ///////////////////////////////////////////
            // Console Resource Owner Flow Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "roclient",
                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                RefreshTokenUsage = TokenUsage.OneTimeOnly,

                AllowOfflineAccess = true,
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Email,
                    IdentityServerConstants.StandardScopes.Address,
                    "roles",
                    "api1", "api2", "api4.with.roles"
                }
            },
            new Client
            {
                ClientId = "roclient.reuse",
                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,

                AllowOfflineAccess = true,
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Email,
                    IdentityServerConstants.StandardScopes.Address,
                    "roles",
                    "api1", "api2", "api4.with.roles"
                },

                RefreshTokenUsage = TokenUsage.ReUse
            },

            /////////////////////////////////////////
            // Console Custom Grant Flow Sample
            ////////////////////////////////////////
            new Client
            {
                ClientId = "client.custom",
                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = { "custom", "custom.nosubject" },

                AllowedScopes =
                {
                    "api1", "api2"
                },

                AllowOfflineAccess = true
            },
            new Client
            {
                ClientId = "client.dynamic",
                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = { "dynamic" },

                AllowedScopes =
                {
                    "api1", "api2"
                },

                AlwaysSendClientClaims = true
            },

            ///////////////////////////////////////////
            // Introspection Client Sample
            //////////////////////////////////////////
            new Client
            {
                ClientId = "roclient.reference",
                ClientSecrets =
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,

                AllowOfflineAccess = true,
                AllowedScopes =
                {
                    "api1", "api2"
                },

                AccessTokenType = AccessTokenType.Reference
            },

            new Client
            {
                ClientName = "Client with Base64 encoded X509 Certificate",
                ClientId = "certificate_base64_valid",
                Enabled = true,

                ClientSecrets =
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                        Value = Convert.ToBase64String(TestCert.Load().Export(X509ContentType.Cert))
                    }
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,

                AllowedScopes = new List<string>
                {
                    "api1", "api2"
                }
            },

            new Client
            {
                ClientId = "implicit",
                AllowedGrantTypes = GrantTypes.Implicit,
                AllowedScopes = {"api1"},
                RedirectUris = { "http://implicit" }
            },
            new Client
            {
                ClientId = "implicit_and_client_creds",
                AllowedGrantTypes = GrantTypes.ImplicitAndClientCredentials,
                AllowedScopes = {"api1"},
                RedirectUris = { "http://implicit_and_client_creds" }
            }
        };
}
