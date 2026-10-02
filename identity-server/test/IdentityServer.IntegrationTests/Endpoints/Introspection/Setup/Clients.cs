// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Introspection.Setup;

internal class Clients
{
    public static IEnumerable<Client> Get() => new List<Client>
        {
            new Client
            {
                ClientId = "client1",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "api1", "api2", "api3-a", "api3-b", "introspection-telemetry-valid-active-token" },
                AccessTokenType = AccessTokenType.Reference
            },
            new Client
            {
                ClientId = "client2",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "api1", "api2", "api3-a", "api3-b" },
                AccessTokenType = AccessTokenType.Reference
            },
            new Client
            {
                ClientId = "client3",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "api1", "api2", "api3-a", "api3-b" },
                AccessTokenType = AccessTokenType.Reference
            },
            new Client
            {
                ClientId = "ro.client",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                AllowedScopes = { "api1", "api2", "api3-a", "api3-b", "roles", "address" },
                AllowOfflineAccess = true,
                AccessTokenType = AccessTokenType.Reference
            },
            new Client
            {
                ClientId = "ro.client2",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                AllowedScopes = { "api1", "api2", "api3-a", "api3-b" },
                AllowOfflineAccess = true,
                AccessTokenType = AccessTokenType.Reference
            },
            // Dedicated client for a telemetry test. This caller must not be shared with any other
            // test in the assembly: telemetry measurements are captured via a process-wide Meter, so
            // a caller value reused by another (possibly parallel) test would make the measurement
            // count for this test non-deterministic. See PR #3565 review discussion.
            new Client
            {
                ClientId = "introspection.telemetry.fails-validation",
                ClientSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },

                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "api1" },
                AccessTokenType = AccessTokenType.Reference
            },
        };
}
