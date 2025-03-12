// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace UnitTests.Validation.Setup;

internal class TestScopes
{
    public static IEnumerable<IdentityResource> GetIdentity()
    {
        return new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        };
    }

    public static IEnumerable<ApiResource> GetApis()
    {
        return new ApiResource[]
        {
            new ApiResource
            {
                Name = "api",
                Scopes =  { "resource", "resource2" }
            },
            new ApiResource
            {
                Name = "urn:api1",
                Scopes =  { "scope1" }
            },
            new ApiResource
            {
                Name = "urn:api2",
                Scopes =  { "scope1" }
            },
            new ApiResource
            {
                Name = "urn:api3",
                Scopes =  { "scope1" }
            },
        };
    }

    public static IEnumerable<ApiScope> GetScopes()
    {
        return new ApiScope[]
        {
            new ApiScope
            {
                Name = "resource",
                Description = "resource scope"
            },
            new ApiScope
            {
                Name = "resource2",
                Description = "resource scope"
            },
            new ApiScope("scope1")
        };
    }
}
