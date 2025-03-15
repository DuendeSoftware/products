// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Stores;

public class InMemoryResourcesStoreTests
{
    [Fact]
    public void InMemoryResourcesStore_should_throw_if_contains_duplicate_names()
    {
        var identityResources = new List<IdentityResource>
        {
            new IdentityResource { Name = "A" },
            new IdentityResource { Name = "A" },
            new IdentityResource { Name = "C" }
        };

        var apiResources = new List<ApiResource>
        {
            new ApiResource { Name = "B" },
            new ApiResource { Name = "B" },
            new ApiResource { Name = "C" }
        };

        var scopes = new List<ApiScope>
        {
            new ApiScope { Name = "B" },
            new ApiScope { Name = "C" },
            new ApiScope { Name = "C" },
        };

        Action act = () => new InMemoryResourcesStore(identityResources, null, null);
        act.ShouldThrow<ArgumentException>();

        act = () => new InMemoryResourcesStore(null, apiResources, null);
        act.ShouldThrow<ArgumentException>();

        act = () => new InMemoryResourcesStore(null, null, scopes);
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void InMemoryResourcesStore_should_not_throw_if_does_not_contains_duplicate_names()
    {
        var identityResources = new List<IdentityResource>
        {
            new IdentityResource { Name = "A" },
            new IdentityResource { Name = "B" },
            new IdentityResource { Name = "C" }
        };

        var apiResources = new List<ApiResource>
        {
            new ApiResource { Name = "A" },
            new ApiResource { Name = "B" },
            new ApiResource { Name = "C" }
        };

        var apiScopes = new List<ApiScope>
        {
            new ApiScope { Name = "A" },
            new ApiScope { Name = "B" },
            new ApiScope { Name = "C" },
        };

        new InMemoryResourcesStore(identityResources, null, null);
        new InMemoryResourcesStore(null, apiResources, null);
        new InMemoryResourcesStore(null, null, apiScopes);
    }
}
