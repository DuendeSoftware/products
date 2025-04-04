// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace IntegrationTests.Clients.Setup;

internal class Scopes
{
    public static IEnumerable<IdentityResource> GetIdentityScopes() => new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Email(),
            new IdentityResources.Address(),
            new IdentityResource("roles", new[] { "role" })
        };

    public static IEnumerable<ApiResource> GetApiResources() => new List<ApiResource>
        {
            new ApiResource
            {
                Name = "api",
                ApiSecrets =
                {
                    new Secret("secret".Sha256())
                },
                Scopes = { "api1", "api2", "api3", "api4.with.roles" }
            },
            new ApiResource("other_api")
            {
                Scopes = { "other_api" }
            }
        };

    public static IEnumerable<ApiScope> GetApiScopes() => new ApiScope[]
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
                Name = "api3"
            },
            new ApiScope
            {
                Name = "api4.with.roles",
                UserClaims = { "role" }
            },
            new ApiScope
            {
                Name = "other_api",
            },
        };
}
