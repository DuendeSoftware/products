// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation;

public class ResourceValidation
{
    private const string Category = "Scope and Resource Validation";

    private List<IdentityResource> _identityResources = new List<IdentityResource>
    {
        new IdentityResource
        {
            Name = "openid",
            Required = true
        },
        new IdentityResource
        {
            Name = "email"
        }
    };

    private List<ApiResource> _apiResources = new List<ApiResource>
    {
        new ApiResource
        {
            Name = "resource1",
            Scopes = { "scope1", "scope2" }
        },
        new ApiResource
        {
            Name = "resource2",
            Scopes = { "disabled_scope" }
        },
        new ApiResource
        {
            Name = "resource3",
            Scopes = { "scope3" }
        },
        new ApiResource
        {
            Name = "isolated1",
            RequireResourceIndicator = true,
            Scopes = { "scope1" }
        },
    };

    private List<ApiScope> _scopes = new List<ApiScope> {
        new ApiScope
        {
            Name = "scope1",
            Required = true
        },
        new ApiScope
        {
            Name = "scope2"
        },
        new ApiScope
        {
            Name = "scope3"
        },
        new ApiScope
        {
            Name = "scope4"
        },
        new ApiScope
        {
            Name = "disabled_scope",
            Enabled = false,
        },
    };

    private Client _restrictedClient = new Client
    {
        ClientId = "restricted",

        AllowedScopes = new List<string>
        {
            "openid",
            "scope1",
            "disabled_scope"
        }
    };
    private Client _resourceClient = new Client
    {
        ClientId = "resource_client",

        AllowOfflineAccess = true,

        AllowedScopes = new List<string>
        {
            "scope1",
            "scope2",
            "scope3",
            "scope4",
        }
    };

    private IResourceStore _subject;
    private readonly CT _ct = TestContext.Current.CancellationToken;

    public ResourceValidation() => _subject = new InMemoryResourcesStore(_identityResources, _apiResources, _scopes);

    // scope validation

    [Fact]
    [Trait("Category", Category)]
    public async Task Only_Offline_Access_Requested()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _restrictedClient,
            Scopes = new[] { "offline_access" }
        }, _ct);

        result.Succeeded.ShouldBeFalse();
        result.InvalidScopes.ShouldContain("offline_access");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task All_Scopes_Valid()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _restrictedClient,
            Scopes = new[] { "openid", "scope1" }
        }, _ct);

        result.Succeeded.ShouldBeTrue();
        result.InvalidScopes.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Scope()
    {
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "openid", "email", "scope1", "unknown" }
            }, _ct);

            result.Succeeded.ShouldBeFalse();
            result.InvalidScopes.ShouldContain("unknown");
            result.InvalidScopes.ShouldContain("email");
        }
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "openid", "scope1", "scope2" }
            }, _ct);

            result.Succeeded.ShouldBeFalse();
            result.InvalidScopes.ShouldContain("scope2");
        }
        {
            var validator = Factory.CreateResourceValidator(_subject);
            var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
            {
                Client = _restrictedClient,
                Scopes = new[] { "openid", "email", "scope1" }
            }, _ct);

            result.Succeeded.ShouldBeFalse();
            result.InvalidScopes.ShouldContain("email");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Disabled_Scope()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _restrictedClient,
            Scopes = new[] { "openid", "scope1", "disabled_scope" }
        }, _ct);

        result.Succeeded.ShouldBeFalse();
        result.InvalidScopes.ShouldContain("disabled_scope");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task All_Scopes_Allowed_For_Restricted_Client()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _restrictedClient,
            Scopes = new[] { "openid", "scope1" }
        }, _ct);

        result.Succeeded.ShouldBeTrue();
        result.InvalidScopes.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Restricted_Scopes()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _restrictedClient,
            Scopes = new[] { "openid", "email", "scope1", "scope2" }
        }, _ct);

        result.Succeeded.ShouldBeFalse();
        result.InvalidScopes.ShouldContain("email");
        result.InvalidScopes.ShouldContain("scope2");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Contains_Resource_and_Identity_Scopes()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _restrictedClient,
            Scopes = new[] { "openid", "scope1" }
        }, _ct);

        result.Succeeded.ShouldBeTrue();
        result.Resources.IdentityResources.Select(x => x.Name).ShouldBe(["openid"]);
        result.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["scope1"]);
        result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["resource1"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Contains_Resource_Scopes_Only()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _restrictedClient,
            Scopes = new[] { "scope1" }
        }, _ct);

        result.Succeeded.ShouldBeTrue();
        result.Resources.IdentityResources.ShouldBeEmpty();
        result.Resources.ApiScopes.Select(x => x.Name).ShouldContain("scope1");
        result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["resource1"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Contains_Identity_Scopes_Only()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _restrictedClient,
            Scopes = new[] { "openid" }
        }, _ct);

        result.Succeeded.ShouldBeTrue();
        result.Resources.IdentityResources.Select(x => x.Name).ShouldContain("openid");
        result.Resources.ApiResources.ShouldBeEmpty();
        result.Resources.ApiResources.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Scope_matches_multipls_apis_should_succeed()
    {
        _apiResources.Clear();
        _apiResources.Add(new ApiResource { Name = "r1", Scopes = { "s" } });
        _apiResources.Add(new ApiResource { Name = "r2", Scopes = { "s" } });
        _apiResources.Add(new ApiResource { Name = "r3", Scopes = { "s" }, Enabled = false });
        _scopes.Clear();
        _scopes.Add(new ApiScope("s"));

        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = new Client { AllowedScopes = { "s" } },
            Scopes = new[] { "s" }
        }, _ct);

        result.Succeeded.ShouldBeTrue();
        result.Resources.ApiResources.Count.ShouldBe(2);
        result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["r1", "r2"]);
        result.RawScopeValues.Count().ShouldBe(1);
        result.RawScopeValues.ShouldBe(["s"]);
    }

    // resource indicators

    [Fact]
    [Trait("Category", Category)]
    public async Task should_include_all_resources_that_match_scope()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _resourceClient,
            Scopes = new[] { "scope1", "offline_access" },
            ResourceIndicators = new[] { "isolated1" },
        }, _ct);

        result.Succeeded.ShouldBeTrue();
        result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["resource1", "isolated1"]);
        result.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["scope1"]);
        result.Resources.OfflineAccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task no_resource_indicator_should_exclude_apis_marked_as_isolated()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _resourceClient,
            Scopes = new[] { "scope1" },
        }, _ct);

        result.Succeeded.ShouldBeTrue();
        result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["resource1"]);
        result.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["scope1"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_resource_should_fail()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _resourceClient,
            Scopes = new[] { "scope1" },
            ResourceIndicators = new[] { "invalid" }
        }, _ct);

        result.Succeeded.ShouldBeFalse();
        result.InvalidScopes.ShouldBeEmpty();
        result.InvalidResourceIndicators.ShouldBe(["invalid"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task resource_without_matching_scope_in_request_should_fail()
    {
        var validator = Factory.CreateResourceValidator(_subject);
        var result = await validator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = _resourceClient,
            Scopes = new[] { "scope1" },
            ResourceIndicators = new[] { "resource3" }
        }, _ct);

        result.Succeeded.ShouldBeFalse();
        result.InvalidScopes.ShouldBeEmpty();
        result.InvalidResourceIndicators.ShouldBe(["resource3"]);
    }


    // ResourceValidationResult FilterByResourceIndicator
    [Fact]
    [Trait("Category", Category)]
    public void FilterByResourceIndicator_should_filter_properly()
    {
        var resources = new Resources(
            new IdentityResource[]
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
            },
            new ApiResource[]
            {
                new ApiResource("resource1"){ Scopes = { "scope1", "scope2" } },
                new ApiResource("resource2"){ Scopes = { "scope1" } },
                new ApiResource("isolated1"){ Scopes = { "scope3", "scope2" }, RequireResourceIndicator = true },
                new ApiResource("isolated2"){ Scopes = { "scope3" }, RequireResourceIndicator = true },
            },
            new ApiScope[]
            {
                new ApiScope("scope1"),
                new ApiScope("scope2"),
                new ApiScope("scope3"),
            });

        var subject = new ResourceValidationResult(resources);

        {
            var result = subject.FilterByResourceIndicator(null);
            result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["resource1", "resource2"]);
            result.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["scope1", "scope2", "scope3"]);
            result.Resources.OfflineAccess.ShouldBeFalse();
        }
        {
            resources.OfflineAccess = true;
            var result = subject.FilterByResourceIndicator(null);
            result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["resource1", "resource2"]);
            result.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["scope1", "scope2", "scope3"]);
            result.Resources.OfflineAccess.ShouldBeTrue();
        }
        {
            var result = subject.FilterByResourceIndicator("resource1");
            result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["resource1"]);
            result.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["scope1", "scope2"]);
        }
        {
            var result = subject.FilterByResourceIndicator("resource2");
            result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["resource2"]);
            result.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["scope1"]);
        }
        {
            var result = subject.FilterByResourceIndicator("isolated1");
            result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["isolated1"]);
            result.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["scope2", "scope3"]);
        }
        {
            var result = subject.FilterByResourceIndicator("isolated2");
            result.Resources.ApiResources.Select(x => x.Name).ShouldBe(["isolated2"]);
            result.Resources.ApiScopes.Select(x => x.Name).ShouldBe(["scope3"]);
        }
    }
}
