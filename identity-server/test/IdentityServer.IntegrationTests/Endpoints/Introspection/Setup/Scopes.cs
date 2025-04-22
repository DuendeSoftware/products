// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace IntegrationTests.Endpoints.Introspection.Setup;

internal class Scopes
{
    public static IEnumerable<IdentityResource> GetIdentityScopes() => new IdentityResource[]
    {
        new IdentityResources.OpenId(),
        new IdentityResources.Email(),
        new IdentityResources.Address(),
        new IdentityResource("roles", new[] { "role" })
    };

    public static IEnumerable<ApiResource> GetApis() => new ApiResource[]
        {
            new ApiResource
            {
                Name = "api1",
                ApiSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },
                Scopes = { "api1" },
                UserClaims = { "role", "address" }
            },
            new ApiResource
            {
                Name = "api2",
                ApiSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },
                Scopes = { "api2" }
            },
            new ApiResource
            {
                Name = "api3",
                ApiSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },
                Scopes = { "api3-a", "api3-b" }
            }
        };
    public static IEnumerable<ApiScope> GetScopes() => new ApiScope[]
        {
            new ApiScope
            {
                Name = "api1"
            },
            new ApiScope
            {
                Name = "api2"
            },
            new ApiScope
            {
                Name = "api3-a"
            },
            new ApiScope
            {
                Name = "api3-b"
            }
        };
}
