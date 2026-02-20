// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DefaultClaimsServiceTests
{
    private DefaultClaimsService _subject;
    private MockProfileService _mockMockProfileService = new MockProfileService();
    private readonly CT _ct = TestContext.Current.CancellationToken;

    private ClaimsPrincipal _user;
    private Client _client;
    private ValidatedRequest _validatedRequest;
    private Resources _resources = new Resources();

    public ResourceValidationResult ResourceValidationResult => new ResourceValidationResult(_resources);

    public DefaultClaimsServiceTests()
    {
        _client = new Client
        {
            ClientId = "client",
            Claims = { new ClientClaim("some_claim", "some_claim_value") }
        };

        _user = new IdentityServerUser("bob")
        {
            IdentityProvider = "idp",
            AuthenticationMethods = { OidcConstants.AuthenticationMethods.Password },
            AuthenticationTime = new System.DateTime(2000, 1, 1),
            AdditionalClaims =
            {
                new Claim("foo", "foo1"),
                new Claim("foo", "foo2"),
                new Claim("bar", "bar1"),
                new Claim("bar", "bar2"),
                new Claim(JwtClaimTypes.AuthenticationContextClassReference, "acr1")
            }
        }.CreatePrincipal();

        _subject = new DefaultClaimsService(_mockMockProfileService, TestLogger.Create<DefaultClaimsService>());

        _validatedRequest = new ValidatedRequest();
        _validatedRequest.Options = new IdentityServerOptions();
        _validatedRequest.SetClient(_client);
    }

    [Fact]
    public async Task GetIdentityTokenClaimsAsync_should_return_standard_user_claims()
    {
        var claims = await _subject.GetIdentityTokenClaimsAsync(_user, ResourceValidationResult, false, _validatedRequest, _ct);

        var types = claims.Select(x => x.Type);
        types.ShouldContain(JwtClaimTypes.Subject);
        types.ShouldContain(JwtClaimTypes.AuthenticationTime);
        types.ShouldContain(JwtClaimTypes.IdentityProvider);
        types.ShouldContain(JwtClaimTypes.AuthenticationMethod);
        types.ShouldContain(JwtClaimTypes.AuthenticationContextClassReference);
    }

    [Fact]
    public async Task GetIdentityTokenClaimsAsync_should_return_minimal_claims_when_includeAllIdentityClaims_is_false()
    {
        _resources.IdentityResources.Add(new IdentityResource("id_scope", new[] { "foo" }));

        var claims = await _subject.GetIdentityTokenClaimsAsync(_user, ResourceValidationResult, false, _validatedRequest, _ct);

        _mockMockProfileService.GetProfileWasCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task GetIdentityTokenClaimsAsync_should_return_all_claims_when_includeAllIdentityClaims_is_true()
    {
        _resources.IdentityResources.Add(new IdentityResource("id_scope", new[] { "foo" }));
        _mockMockProfileService.ProfileClaims.Add(new Claim("foo", "foo1"));

        var claims = await _subject.GetIdentityTokenClaimsAsync(_user, ResourceValidationResult, true, _validatedRequest, _ct);

        _mockMockProfileService.GetProfileWasCalled.ShouldBeTrue();
        _mockMockProfileService.ProfileContext.RequestedClaimTypes.ShouldContain("foo");
    }

    [Fact]
    public async Task GetIdentityTokenClaimsAsync_should_return_all_claims_when_client_configured_for_always_include_all_claims_in_id_token()
    {
        _client.AlwaysIncludeUserClaimsInIdToken = true;

        _resources.IdentityResources.Add(new IdentityResource("id_scope", new[] { "foo" }));
        _mockMockProfileService.ProfileClaims.Add(new Claim("foo", "foo1"));

        var claims = await _subject.GetIdentityTokenClaimsAsync(_user, ResourceValidationResult, false, _validatedRequest, _ct);

        _mockMockProfileService.GetProfileWasCalled.ShouldBeTrue();
        _mockMockProfileService.ProfileContext.RequestedClaimTypes.ShouldContain("foo");
    }

    [Fact]
    public async Task GetIdentityTokenClaimsAsync_should_filter_protocol_claims_from_profile_service()
    {
        _resources.IdentityResources.Add(new IdentityResource("id_scope", new[] { "foo" }));
        _mockMockProfileService.ProfileClaims.Add(new Claim("aud", "bar"));

        var claims = await _subject.GetIdentityTokenClaimsAsync(_user, ResourceValidationResult, true, _validatedRequest, _ct);

        claims.Count(x => x.Type == "aud" && x.Value == "bar").ShouldBe(0);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_contain_client_id()
    {
        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        claims.Count(x => x.Type == JwtClaimTypes.ClientId && x.Value == _client.ClientId).ShouldBe(1);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_client_claims_should_be_prefixed_with_default_value()
    {
        var claims = await _subject.GetAccessTokenClaimsAsync(null, ResourceValidationResult, _validatedRequest, _ct);

        claims.Count(x => x.Type == "client_some_claim" && x.Value == "some_claim_value").ShouldBe(1);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_client_claims_should_be_prefixed_with_custom_value()
    {
        _validatedRequest.Client.ClientClaimsPrefix = "custom_prefix_";
        var claims = await _subject.GetAccessTokenClaimsAsync(null, ResourceValidationResult, _validatedRequest, _ct);

        claims.Count(x => x.Type == "custom_prefix_some_claim" && x.Value == "some_claim_value").ShouldBe(1);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_contain_client_claims_when_no_subject()
    {
        _validatedRequest.Client.ClientClaimsPrefix = null;
        var claims = await _subject.GetAccessTokenClaimsAsync(null, ResourceValidationResult, _validatedRequest, _ct);

        claims.Count(x => x.Type == "some_claim" && x.Value == "some_claim_value").ShouldBe(1);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_contain_client_claims_when_configured_to_send_client_claims()
    {
        _validatedRequest.Client.ClientClaimsPrefix = null;
        _validatedRequest.Client.AlwaysSendClientClaims = true;

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        claims.Count(x => x.Type == "some_claim" && x.Value == "some_claim_value").ShouldBe(1);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_contain_scopes()
    {
        _resources.IdentityResources.Add(new IdentityResource("id1", new[] { "foo" }));
        _resources.IdentityResources.Add(new IdentityResource("id2", new[] { "bar" }));
        _resources.ApiScopes.Add(new ApiScope("api1"));
        _resources.ApiScopes.Add(new ApiScope("api2"));

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        var scopes = claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
        scopes.Count().ShouldBe(4);
        scopes.ToArray().ShouldBe(["api1", "api2", "id1", "id2"], true);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_contain_parameterized_scope_values()
    {
        _resources.ApiScopes.Add(new ApiScope("api"));
        var resourceResult = new ResourceValidationResult()
        {
            Resources = _resources,
            ParsedScopes = { new ParsedScopeValue("api:123", "api", "123") }
        };

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, resourceResult, _validatedRequest, _ct);

        var scopes = claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
        scopes.Count().ShouldBe(1);
        scopes.ToArray().ShouldBe(new string[] { "api:123" });
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_when_no_ApiScopes_should_not_contain_scopes()
    {
        _resources.ApiResources.Add(new ApiResource("api1"));

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        var scopes = claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
        scopes.Count().ShouldBe(0);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_only_consider_parsed_scope_values_and_not_ApiScope()
    {
        // arguably, if this situation arises, then the ResourceValidationResult was not populated properly
        // with ParsedScopes matching ApiScopes
        _resources.ApiScopes.Add(new ApiScope("api1"));
        var resourceResult = new ResourceValidationResult()
        {
            Resources = _resources,
            ParsedScopes = { new ParsedScopeValue("api2") }
        };

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, resourceResult, _validatedRequest, _ct);

        var scopes = claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
        scopes.Count().ShouldBe(1);
        scopes.ToArray().ShouldBe(new string[] { "api2" });
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_when_multiple_resources_with_same_scope_should_contain_scope_once()
    {
        _resources.OfflineAccess = false;
        _resources.IdentityResources.Clear();
        _resources.ApiResources.Clear();
        _resources.ApiScopes.Clear();

        _resources.ApiResources.Add(new ApiResource { Name = "api1", Scopes = { "resource" } });
        _resources.ApiResources.Add(new ApiResource { Name = "api2", Scopes = { "resource" } });
        _resources.ApiResources.Add(new ApiResource { Name = "api3", Scopes = { "resource" } });
        _resources.ApiScopes.Add(new ApiScope("resource"));

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        var scopes = claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
        scopes.Count().ShouldBe(1);
        scopes.ToArray().ShouldBe(new string[] { "resource" });
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_contain_offline_scope()
    {
        _resources.IdentityResources.Add(new IdentityResource("id1", new[] { "foo" }));
        _resources.IdentityResources.Add(new IdentityResource("id2", new[] { "bar" }));
        _resources.ApiResources.Add(new ApiResource("api1"));
        _resources.ApiResources.Add(new ApiResource("api2"));
        _resources.OfflineAccess = true;

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        var scopes = claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
        scopes.ShouldContain(IdentityServerConstants.StandardScopes.OfflineAccess);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_not_contain_offline_scope_if_no_user()
    {
        _resources.IdentityResources.Add(new IdentityResource("id1", new[] { "foo" }));
        _resources.IdentityResources.Add(new IdentityResource("id2", new[] { "bar" }));
        _resources.ApiResources.Add(new ApiResource("api1"));
        _resources.ApiResources.Add(new ApiResource("api2"));
        _resources.OfflineAccess = true;

        var claims = await _subject.GetAccessTokenClaimsAsync(null, ResourceValidationResult, _validatedRequest, _ct);

        var scopes = claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
        scopes.ShouldNotContain(IdentityServerConstants.StandardScopes.OfflineAccess);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_return_standard_user_claims()
    {
        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        var types = claims.Select(x => x.Type);
        types.ShouldContain(JwtClaimTypes.Subject);
        types.ShouldContain(JwtClaimTypes.AuthenticationTime);
        types.ShouldContain(JwtClaimTypes.IdentityProvider);
        types.ShouldContain(JwtClaimTypes.AuthenticationMethod);
        types.ShouldContain(JwtClaimTypes.AuthenticationContextClassReference);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_only_contain_api_claims()
    {
        _resources.IdentityResources.Add(new IdentityResource("id1", new[] { "foo" }));
        _resources.ApiResources.Add(new ApiResource("api1", new string[] { "bar" }));

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        _mockMockProfileService.GetProfileWasCalled.ShouldBeTrue();
        _mockMockProfileService.ProfileContext.RequestedClaimTypes.ShouldNotContain("foo");
        _mockMockProfileService.ProfileContext.RequestedClaimTypes.ShouldContain("bar");
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_filter_protocol_claims_from_profile_service()
    {
        _resources.ApiResources.Add(new ApiResource("api1", new[] { "foo" }));
        _mockMockProfileService.ProfileClaims.Add(new Claim("aud", "bar"));

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        claims.Count(x => x.Type == "aud" && x.Value == "bar").ShouldBe(0);
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_request_api_claims()
    {
        _resources.ApiResources.Add(new ApiResource("api1", new[] { "foo" }));

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        _mockMockProfileService.ProfileContext.RequestedClaimTypes.ShouldContain("foo");
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_request_api_scope_claims()
    {
        _resources.ApiResources.Add(
            new ApiResource("api")
            {
                Scopes = { "api1" }
            }
        );
        _resources.ApiScopes.Add(
            new ApiScope("api1")
            {
                UserClaims = { "foo" }
            }
        );

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        _mockMockProfileService.ProfileContext.RequestedClaimTypes.ShouldContain("foo");
    }

    [Fact]
    public async Task GetAccessTokenClaimsAsync_should_request_both_api_and_api_scope_claims()
    {
        _resources.ApiResources.Add(
            new ApiResource("api")
            {
                UserClaims = { "foo" },
                Scopes = { "api1" }
            }
        );
        _resources.ApiScopes.Add(
            new ApiScope("api1")
            {
                UserClaims = { "bar" }
            }
        );

        var claims = await _subject.GetAccessTokenClaimsAsync(_user, ResourceValidationResult, _validatedRequest, _ct);

        _mockMockProfileService.ProfileContext.RequestedClaimTypes.ShouldContain("foo");
        _mockMockProfileService.ProfileContext.RequestedClaimTypes.ShouldContain("bar");
    }
}
