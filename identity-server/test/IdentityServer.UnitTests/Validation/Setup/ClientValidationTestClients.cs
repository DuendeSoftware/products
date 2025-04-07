// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using UnitTests.Common;

namespace UnitTests.Validation.Setup;

internal static class ClientValidationTestClients
{
    public static List<Client> Get() => new List<Client>
        {
            new Client
            {
                ClientName = "Disabled client",
                ClientId = "disabled_client",
                Enabled = false,

                ClientSecrets = new List<Secret>
                {
                    new Secret("secret")
                }
            },

            new Client
            {
                ClientName = "Client with no secret set",
                ClientId = "no_secret_client",
                Enabled = true
            },

            new Client
            {
                ClientName = "Client with null secret set",
                ClientId = "null_secret_client",
                Enabled = true,
                ClientSecrets = { new Secret(null) }
            },

            new Client
            {
                ClientName = "Client with single secret, no protection, no expiration",
                ClientId = "single_secret_no_protection_no_expiration",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    new Secret("secret")
                }
            },

            new Client
            {
                ClientName = "Client with X509 Certificate",
                ClientId = "certificate_valid",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint,
                        Value = TestCert.Load().Thumbprint
                    }
                }
            },

            new Client
            {
                ClientName = "Client with X509 Certificate",
                ClientId = "certificate_invalid",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint,
                        Value = "invalid"
                    }
                }
            },

            new Client
            {
                ClientName = "Client with Base64 encoded X509 Certificate",
                ClientId = "certificate_base64_valid",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                        Value = Convert.ToBase64String(TestCert.Load().Export(X509ContentType.Cert))
                    }
                }
            },

            new Client
            {
                ClientName = "Client with Base64 encoded X509 Certificate",
                ClientId = "certificate_base64_invalid",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    new Secret
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                        Value = "invalid"
                    }
                }
            },

            new Client
            {
                ClientName = "Client with single secret, hashed, no expiration",
                ClientId = "single_secret_hashed_no_expiration",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    // secret
                    new Secret("secret".Sha256())
                }
            },

            new Client
            {
                ClientName = "Client with multiple secrets, no protection",
                ClientId = "multiple_secrets_no_protection",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    new Secret("secret"),
                    new Secret("foobar", "some description"),
                    new Secret("quux"),
                    new Secret("notexpired", DateTime.UtcNow.AddDays(1)),
                    new Secret("expired", DateTime.UtcNow.AddDays(-1))
                }
            },

            new Client
            {
                ClientName = "Client with multiple secrets, hashed",
                ClientId = "multiple_secrets_hashed",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    // secret
                    new Secret("secret".Sha256()),
                    // foobar
                    new Secret("foobar".Sha256(), "some description"),
                    // quux
                    new Secret("quux".Sha512()),
                    // notexpired
                    new Secret("notexpired".Sha256(), DateTime.UtcNow.AddDays(1)),
                    // expired
                    new Secret("expired".Sha512(), DateTime.UtcNow.AddDays(-1))
                },
            },

            new Client
            {
                ClientName = "MTLS Client with invalid secrets",
                ClientId = "mtls_client_invalid",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    new Secret(@"CN=invalid", "mtls.test")
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateName
                    },
                    new Secret("invalid", "mtls.test")
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint
                    },
                }
            },

            new Client
            {
                ClientName = "MTLS Client with valid secrets",
                ClientId = "mtls_client_valid",
                Enabled = true,

                ClientSecrets = new List<Secret>
                {
                    new Secret(@"CN=identityserver_testing", "mtls.test")
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateName
                    },
                    new Secret("4B5FE072C7AD8A9B5DCFDD1A20608BB54DE0954F", "mtls.test")
                    {
                        Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint
                    },
                }
            }
        };
}
