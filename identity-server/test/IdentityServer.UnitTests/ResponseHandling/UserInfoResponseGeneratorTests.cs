// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Shouldly;
using UnitTests.Common;
using Xunit;

namespace UnitTests.ResponseHandling;

public class UserInfoResponseGeneratorTests
{
    private UserInfoResponseGenerator _subject;
    private MockProfileService _mockProfileService = new MockProfileService();
    private ClaimsPrincipal _user;
    private Client _client;

    private InMemoryResourcesStore _resourceStore;
    private List<IdentityResource> _identityResources = new List<IdentityResource>();
    private List<ApiResource> _apiResources = new List<ApiResource>();
    private List<ApiScope> _apiScopes = new List<ApiScope>();

    public UserInfoResponseGeneratorTests()
    {
        _client = new Client
        {
            ClientId = "client"
        };

        _user = new IdentityServerUser("bob")
        {
            AdditionalClaims =
            {
                new Claim("foo", "foo1"),
                new Claim("foo", "foo2"),
                new Claim("bar", "bar1"),
                new Claim("bar", "bar2")
            }
        }.CreatePrincipal();

        _resourceStore = new InMemoryResourcesStore(_identityResources, _apiResources, _apiScopes);
        _subject = new UserInfoResponseGenerator(_mockProfileService, _resourceStore, TestLogger.Create<UserInfoResponseGenerator>());
    }

    [Fact]
    public async Task GetRequestedClaimTypesAsync_when_no_scopes_requested_should_return_empty_claim_types()
    {
        var resources = await _subject.GetRequestedResourcesAsync(null);
        var claims = await _subject.GetRequestedClaimTypesAsync(resources);
        claims.ShouldBe(new string[] { });
    }

    [Fact]
    public async Task GetRequestedClaimTypesAsync_should_return_correct_identity_claims()
    {
        _identityResources.Add(new IdentityResource("id1", new[] { "c1", "c2" }));
        _identityResources.Add(new IdentityResource("id2", new[] { "c2", "c3" }));

        var resources = await _subject.GetRequestedResourcesAsync(new[] { "id1", "id2", "id3" });
        var claims = await _subject.GetRequestedClaimTypesAsync(resources);
        claims.ShouldBe(["c1", "c2", "c3"]);
    }

    [Fact]
    public async Task GetRequestedClaimTypesAsync_should_only_return_enabled_identity_claims()
    {
        _identityResources.Add(new IdentityResource("id1", new[] { "c1", "c2" }) { Enabled = false });
        _identityResources.Add(new IdentityResource("id2", new[] { "c2", "c3" }));

        var resources = await _subject.GetRequestedResourcesAsync(new[] { "id1", "id2", "id3" });
        var claims = await _subject.GetRequestedClaimTypesAsync(resources);
        claims.ShouldBe(["c2", "c3"]);
    }

    [Fact]
    public async Task ProcessAsync_should_call_profile_service_with_requested_claim_types()
    {
        _identityResources.Add(new IdentityResource("id1", new[] { "foo" }));
        _identityResources.Add(new IdentityResource("id2", new[] { "bar" }));

        var result = new UserInfoRequestValidationResult
        {
            Subject = _user,
            TokenValidationResult = new TokenValidationResult
            {
                Claims = new List<Claim>
                {
                    { new Claim("scope", "id1") },
                    { new Claim("scope", "id2") },
                    { new Claim("scope", "id3") }
                },
                Client = _client
            }
        };

        var claims = await _subject.ProcessAsync(result);

        _mockProfileService.GetProfileWasCalled.ShouldBeTrue();
        _mockProfileService.ProfileContext.RequestedClaimTypes.ShouldBe(new[] { "foo", "bar" });
    }

    [Fact]
    public async Task ProcessAsync_should_return_claims_issued_by_profile_service()
    {
        _identityResources.Add(new IdentityResource("id1", new[] { "foo" }));
        _identityResources.Add(new IdentityResource("id2", new[] { "bar" }));
            
        var address = new
        {
            street_address = "One Hacker Way",
            locality = "Heidelberg",
            postal_code = 69118,
            country = "Germany"
        };
            
        _mockProfileService.ProfileClaims = new[]
        {
            new Claim("email", "fred@gmail.com"),
            new Claim("name", "fred jones"),
            new Claim("address", @"{ 'street_address': 'One Hacker Way', 'locality': 'Heidelberg', 'postal_code': 69118, 'country': 'Germany' }", IdentityServerConstants.ClaimValueTypes.Json),
            new Claim("address2", JsonSerializer.Serialize(address), IdentityServerConstants.ClaimValueTypes.Json)
        };

        var result = new UserInfoRequestValidationResult
        {
            Subject = _user,
            TokenValidationResult = new TokenValidationResult
            {
                Claims = new List<Claim>
                {
                    { new Claim("scope", "id1") },
                    { new Claim("scope", "id2") },
                    { new Claim("scope", "id3") }
                },
                Client = _client
            }
        };

        var claims = await _subject.ProcessAsync(result);

        claims.ShouldContainKey("email");
        claims["email"].ShouldBe("fred@gmail.com");
        claims.ShouldContainKey("name");
        claims["name"].ShouldBe("fred jones");
            
        // this will be treated as a string because this is not valid JSON from the System.Text library point of view
        claims.ShouldContainKey("address");
        claims["address"].ShouldBe("{ 'street_address': 'One Hacker Way', 'locality': 'Heidelberg', 'postal_code': 69118, 'country': 'Germany' }");
            
        // this is a JsonElement
        claims.ShouldContainKey("address2");
        claims["address2"].ToString().ShouldBe("{\"street_address\":\"One Hacker Way\",\"locality\":\"Heidelberg\",\"postal_code\":69118,\"country\":\"Germany\"}");
    }

    [Fact]
    public async Task ProcessAsync_should_return_sub_from_user()
    {
        _identityResources.Add(new IdentityResource("id1", new[] { "foo" }));
        _identityResources.Add(new IdentityResource("id2", new[] { "bar" }));

        var result = new UserInfoRequestValidationResult
        {
            Subject = _user,
            TokenValidationResult = new TokenValidationResult
            {
                Claims = new List<Claim>
                {
                    { new Claim("scope", "id1") },
                    { new Claim("scope", "id2") },
                    { new Claim("scope", "id3") }
                },
                Client = _client
            }
        };

        var claims = await _subject.ProcessAsync(result);

        claims.ShouldContainKey("sub");
        claims["sub"].ShouldBe("bob");
    }

    [Fact]
    public async Task ProcessAsync_should_throw_if_incorrect_sub_issued_by_profile_service()
    {
        _identityResources.Add(new IdentityResource("id1", new[] { "foo" }));
        _identityResources.Add(new IdentityResource("id2", new[] { "bar" }));
        _mockProfileService.ProfileClaims = new[]
        {
            new Claim("sub", "fred")
        };

        var result = new UserInfoRequestValidationResult
        {
            Subject = _user,
            TokenValidationResult = new TokenValidationResult
            {
                Claims = new List<Claim>
                {
                    { new Claim("scope", "id1") },
                    { new Claim("scope", "id2") },
                    { new Claim("scope", "id3") }
                },
                Client = _client
            }
        };

        Func<Task> act = () => _subject.ProcessAsync(result);

        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldMatch(".*subject.*");
    }
}